//
//  GameScene.swift
//  tenGO
//

import SpriteKit
import GameplayKit
import StoreKit

class GameScene: SKScene {

    // Kept for GameViewController compatibility
    var entities = [GKEntity]()
    var graphs = [String: GKGraph]()

    // MARK: - Init

    enum Mode { case normal, daily, demo }

    private let mode: Mode
    private var dailyToday: DailyChallenge.Today?
    private var obstacleNodes: [SKNode] = []

    // MARK: - Démo (auto-player, capture vidéo marketing)
    /// Graine de la grille en mode démo (grilles déterministes, reproductibles).
    private var demoSeed: UInt64 = 0
    /// Multiplicateur de vitesse du tracé en démo (>1 = plus rapide / énergique).
    private var demoSpeed: Double = 1.0
    /// Compteur de grilles enchaînées : décale la graine à chaque relance.
    private var demoRound = 0

    private var savedState: GameState?
    private var homeBubbleNode: SKNode!

    init(size: CGSize, savedState: GameState? = nil) {
        self.savedState = savedState
        self.mode = .normal
        super.init(size: size)
    }

    /// Mode Défi du jour : grille déterministe + twist, identique pour tous.
    init(size: CGSize, daily: DailyChallenge.Today) {
        self.mode = .daily
        self.dailyToday = daily
        super.init(size: size)
    }

    /// Mode démo : grille déterministe (graine) jouée automatiquement par le
    /// solveur, pour produire une capture vidéo de gameplay (marketing).
    init(size: CGSize, demoSeed: UInt64, demoSpeed: Double = 1.0) {
        self.mode = .demo
        self.demoSeed = demoSeed
        self.demoSpeed = max(0.25, demoSpeed)
        super.init(size: size)
    }

    required init?(coder aDecoder: NSCoder) {
        self.mode = .normal
        super.init(coder: aDecoder)
    }

    // MARK: - Layout constants

    private static let cellSize: CGFloat = 84

    private var gridOriginX: CGFloat {
        -(CGFloat(GridModel.cols) * GameScene.cellSize) / 2 + GameScene.cellSize / 2
    }
    private var gridOriginY: CGFloat {
        -(CGFloat(GridModel.rows) * GameScene.cellSize) / 2 + GameScene.cellSize / 2
    }
    private var gridTop: CGFloat {
        gridOriginY + CGFloat(GridModel.rows - 1) * GameScene.cellSize
    }
    private var gridBottom: CGFloat { gridOriginY }

    // MARK: - State

    private var gridModel = GridModel()
    private var bubbleNodes = [[BubbleNode?]](
        repeating: [BubbleNode?](repeating: nil, count: GridModel.cols),
        count: GridModel.rows
    )

    private var currentPath: [(row: Int, col: Int)] = []
    private var pathLineNode: SKNode?
    private var isAnimating = false

    // MARK: - Score

    private var score = 0
    private var gameOverPanel: SKNode?
    private var scoreBubbleNode: SKNode!
    private var restartBubbleNode: SKNode!
    private var settingsBubbleNode: SKNode!
    private var settingsOverlay: SettingsOverlay?

    // MARK: - Stats partie
    private var combosCreated = 0
    private var longestChain = 0
    private var isWinState = false
    /// Pièces créditées à la fin de cette partie (affiché dans le panel game-over).
    private var coinsEarnedThisGame = 0

    // MARK: - Boosters
    private var boosterBar: SKNode?
    private var boosterButtons: [(booster: Booster, node: SKNode)] = []
    private var boosterCountLabels: [Booster: SKLabelNode] = [:]
    /// Marteau armé : le prochain tap sur une bulle la détruit.
    private var hammerArmed = false

    // MARK: - Haptics
    // Haptics via HapticManager (toggle dans les paramètres)

    // MARK: - UI nodes

    private var scoreLabel: SKLabelNode!
    private var sumLabel: SKLabelNode? // unused — somme masquée volontairement
    private var messageLabel: SKLabelNode!
    private var restartButton: SKLabelNode!

    // MARK: - Setup

    override func sceneDidLoad() {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        removeAllChildren()
        backgroundColor = ThemeManager.shared.active.background
        setupBackground()
        setupUI()
        if mode == .daily, let today = dailyToday {
            gridModel = today.grid
        } else if mode == .demo {
            var generator = SeededGenerator(seed: demoSeed)
            gridModel = GridModel(using: &generator)
        } else if let state = savedState {
            gridModel = GridModel(from: state)
            score = state.score
            scoreLabel.text = "\(score)"
        }
        setupGrid()

        // En démo : pas d'effets de bord (séries, pièces, notifications),
        // on masque les boutons parasites et on lance l'auto-player.
        guard mode != .demo else {
            homeBubbleNode.isHidden = true
            restartBubbleNode.isHidden = true
            startDemo()
            return
        }

        StreakManager.shared.registerPlay()
        CoinManager.shared.awardStreakMilestones(currentStreak: StreakManager.shared.current)
        NotificationManager.shared.registerGameAndMaybeRequest()
        setupBoosters()
    }

    override func didMove(to view: SKView) {
        repositionBottomRow(in: view)
        setupSettingsButton(in: view)
        HapticManager.prepare()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": false])
    }

    private func setupSettingsButton(in view: SKView) {
        guard mode != .demo else { return }   // pas de chrome parasite en capture vidéo
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleW = view.bounds.width / scale
        let visibleH = view.bounds.height / scale
        let safeTop = view.safeAreaInsets.top / scale
        let safeRight = view.safeAreaInsets.right / scale
        let rightEdge = visibleW / 2 - safeRight
        let topEdge = visibleH / 2 - safeTop

        let container = SKNode()
        container.name = "settingsBtn"
        container.position = CGPoint(x: rightEdge - 44, y: topEdge - 44)
        container.zPosition = 10

        let bg = SKShapeNode(circleOfRadius: 26)
        bg.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        bg.lineWidth = 1.2
        container.addChild(bg)

        // Trois points dessinés manuellement pour un centrage parfait
        let dotColor = UIColor(white: 0.45, alpha: 1)
        let dotRadius: CGFloat = 3
        let dotSpacing: CGFloat = 9
        for i in -1...1 {
            let dot = SKShapeNode(circleOfRadius: dotRadius)
            dot.fillColor = dotColor
            dot.strokeColor = .clear
            dot.position = CGPoint(x: CGFloat(i) * dotSpacing, y: 0)
            container.addChild(dot)
        }

        addChild(container)
        settingsBubbleNode = container
    }

    private func presentSettings() {
        if settingsOverlay?.parent != nil { return }
        let presenter = view?.window?.rootViewController
        let overlay = SettingsOverlay(sceneSize: size, presenter: presenter)
        overlay.onAction = { [weak self] action in
            guard let self = self else { return }
            switch action {
            case .replayTutorial:
                // Depuis le jeu : on sauvegarde l'état et on bascule au tuto
                let tutorial = TutorialScene(size: self.size)
                tutorial.scaleMode = .aspectFill
                self.view?.presentScene(tutorial, transition: SKTransition.fade(withDuration: 0.3))
            }
        }
        overlay.present(in: self)
        settingsOverlay = overlay
    }

    /// Recalcule la mise en page basse quand la zone de jeu change (ex. apparition
    /// de la bannière qui réduit la SKView). Appelé par le GameViewController.
    func relayoutForViewChange() {
        guard let view = view else { return }
        repositionBottomRow(in: view)
    }

    private func repositionBottomRow(in view: SKView) {
        // Calcule la hauteur de scène réellement visible selon l'écran (aspectFill)
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleBottom = -(view.bounds.height / scale) / 2

        let gridEdgeBottom = gridBottom - BubbleNode.bubbleRadius
        let available = gridEdgeBottom - visibleBottom

        // Rangée de contrôle (⌂ / score / ↺) : position d'origine, centrée dans la
        // bande sous la grille — dégage la bannière AdMob sur iPhone.
        let rowY = visibleBottom + available / 2

        homeBubbleNode.position.y  = rowY
        scoreBubbleNode.position.y = rowY
        restartBubbleNode.position.y = rowY

        // Barre de boosters : centrée dans l'écart entre le bas de la grille et le
        // haut de la rangée de contrôle. Écart large sur iPhone (placement net),
        // réduit sur iPad (centrage au mieux, sans empiéter sur la bannière).
        if let bar = boosterBar {
            let controlTop = rowY + 44   // rayon de la bulle de contrôle
            bar.position.y = (gridEdgeBottom + controlTop) / 2
        }
    }

    // MARK: - Background

    private var bubbleColors: [UIColor] { ThemeManager.shared.active.bubbles }

    private func setupBackground() {
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
    }

    private func setupUI() {
        let logoY = gridTop + 95

        let ten = SKLabelNode(text: "TEN")
        ten.fontName = "AvenirNext-Heavy"
        ten.fontSize = 48
        ten.fontColor = ThemeManager.shared.active.logo
        ten.verticalAlignmentMode = .center
        ten.horizontalAlignmentMode = .right
        ten.position = CGPoint(x: -14, y: logoY)
        addChild(ten)

        let dot = SKShapeNode(circleOfRadius: 11)
        dot.fillColor = bubbleColors[Int.random(in: 0..<bubbleColors.count)]
        dot.strokeColor = .clear
        dot.position = CGPoint(x: 0, y: logoY + 3)
        dot.zPosition = 1
        addChild(dot)
        dot.run(SKAction.repeatForever(SKAction.sequence([
            SKAction.scale(to: 1.15, duration: 1.4),
            SKAction.scale(to: 0.90, duration: 1.4)
        ])))

        let go = SKLabelNode(text: "GO")
        go.fontName = "AvenirNext-Heavy"
        go.fontSize = 48
        go.fontColor = ThemeManager.shared.active.logo
        go.verticalAlignmentMode = .center
        go.horizontalAlignmentMode = .left
        go.position = CGPoint(x: 14, y: logoY)
        addChild(go)

        messageLabel = SKLabelNode(text: "")
        messageLabel.fontName = "AvenirNext-Heavy"
        messageLabel.fontSize = 28
        messageLabel.fontColor = UIColor(white: 0.35, alpha: 1)
        messageLabel.position = CGPoint(x: 0, y: gridTop + 50)
        messageLabel.isHidden = true
        addChild(messageLabel)

        // Deux bulles côte à côte, centrées ensemble dans l'espace sous la grille
        let sceneBottom = -size.height / 2
        let gridEdgeBottom = gridBottom - BubbleNode.bubbleRadius
        let availableHeight = gridEdgeBottom - sceneBottom
        let rowY = gridEdgeBottom - availableHeight / 2

        let scoreR: CGFloat = 58
        let sideR: CGFloat = 44
        let gap: CGFloat = 18
        let sideCenterX = scoreR + gap + sideR   // = 120

        // Bulle home — à gauche du score
        let homeBubble = SKNode()
        homeBubble.position = CGPoint(x: -sideCenterX, y: rowY)
        addChild(homeBubble)
        homeBubbleNode = homeBubble

        let homeCircle = SKShapeNode(circleOfRadius: sideR)
        homeCircle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        homeCircle.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        homeCircle.lineWidth = 1.5
        homeBubble.addChild(homeCircle)

        let homeIcon = SKLabelNode(text: "⌂")
        homeIcon.fontName = "AvenirNext-Medium"
        homeIcon.fontSize = 28
        homeIcon.fontColor = UIColor(white: 0.45, alpha: 1)
        homeIcon.verticalAlignmentMode = .center
        homeIcon.horizontalAlignmentMode = .center
        homeBubble.addChild(homeIcon)

        // Bulle score — toujours centrée
        let bubble = SKNode()
        bubble.position = CGPoint(x: 0, y: rowY)
        addChild(bubble)
        scoreBubbleNode = bubble

        let circle = SKShapeNode(circleOfRadius: scoreR)
        circle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        circle.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        circle.lineWidth = 1.5
        bubble.addChild(circle)

        let ptsLabel = SKLabelNode(text: String(localized: "game.points_label"))
        ptsLabel.fontName = "AvenirNext-UltraLight"
        ptsLabel.fontSize = 13
        ptsLabel.fontColor = UIColor(white: 0.58, alpha: 1)
        ptsLabel.verticalAlignmentMode = .center
        ptsLabel.position = CGPoint(x: 0, y: 22)
        bubble.addChild(ptsLabel)

        scoreLabel = SKLabelNode(text: "0")
        scoreLabel.fontName = "AvenirNext-Medium"
        scoreLabel.fontSize = 32
        scoreLabel.fontColor = UIColor(white: 0.32, alpha: 1)
        scoreLabel.verticalAlignmentMode = .center
        scoreLabel.position = CGPoint(x: 0, y: -8)
        bubble.addChild(scoreLabel)

        // Bulle restart — à droite de la bulle score
        let restartBubble = SKNode()
        restartBubble.position = CGPoint(x: sideCenterX, y: rowY)
        addChild(restartBubble)
        restartBubbleNode = restartBubble

        let restartCircle = SKShapeNode(circleOfRadius: sideR)
        restartCircle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        restartCircle.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        restartCircle.lineWidth = 1.5
        restartBubble.addChild(restartCircle)

        restartButton = SKLabelNode(text: "↺")
        restartButton.fontName = "AvenirNext-Medium"
        restartButton.fontSize = 30
        restartButton.fontColor = UIColor(white: 0.45, alpha: 1)
        restartButton.verticalAlignmentMode = .center
        restartButton.horizontalAlignmentMode = .center
        restartButton.name = "restartButton"
        restartButton.position = .zero
        restartBubble.addChild(restartButton)

        // Mode Défi : pas de rejeu (une seule partie par jour) → bouton masqué.
        restartBubble.isHidden = (mode == .daily)
    }

    private func setupGrid() {
        renderObstacles()
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let model = gridModel.cells[row][col] else { continue }
                let node = BubbleNode(value: model.value)
                node.position = scenePos(row: row, col: col)
                if model.isAnchored { node.setAnchored(true) }
                if model.isFrozen { node.setFrozen(true) }
                addChild(node)
                bubbleNodes[row][col] = node
            }
        }
    }

    /// Place les pierres-obstacles du Défi du jour (cases bloquées, statiques).
    private func renderObstacles() {
        obstacleNodes.forEach { $0.removeFromParent() }
        obstacleNodes.removeAll()
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols where gridModel.blocked[row][col] {
                let stone = BubbleNode.makeObstacle(cellSize: GameScene.cellSize)
                stone.position = scenePos(row: row, col: col)
                stone.zPosition = 0
                addChild(stone)
                obstacleNodes.append(stone)
            }
        }
    }

    // MARK: - Coordinate helpers

    private func scenePos(row: Int, col: Int) -> CGPoint {
        CGPoint(
            x: gridOriginX + CGFloat(col) * GameScene.cellSize,
            y: gridOriginY + CGFloat(row) * GameScene.cellSize
        )
    }

    private func gridCoord(for point: CGPoint) -> (row: Int, col: Int)? {
        let col = Int((point.x - gridOriginX + GameScene.cellSize / 2) / GameScene.cellSize)
        let row = Int((point.y - gridOriginY + GameScene.cellSize / 2) / GameScene.cellSize)
        guard row >= 0 && row < GridModel.rows && col >= 0 && col < GridModel.cols else { return nil }
        guard gridModel.cells[row][col] != nil else { return nil }
        return (row, col)
    }

    // MARK: - Touch handling

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard mode != .demo else { return }   // démo pilotée par l'auto-player
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        // Overlay paramètres prioritaire si ouvert
        if let overlay = settingsOverlay, overlay.parent != nil {
            overlay.handleTouch(at: point)
            if overlay.parent == nil { settingsOverlay = nil }
            return
        }

        // Bouton paramètres en haut à droite
        if let settingsBubble = settingsBubbleNode,
           hypot(point.x - settingsBubble.position.x, point.y - settingsBubble.position.y) < 34 {
            settingsBubble.run(SKAction.sequence([
                SKAction.scale(to: 0.88, duration: 0.08),
                SKAction.scale(to: 1.0, duration: 0.12)
            ]))
            presentSettings()
            return
        }

        // Bouton home — retour au menu
        if hypot(point.x - homeBubbleNode.position.x, point.y - homeBubbleNode.position.y) < 34 {
            goBackToMenu()
            return
        }

        // Boutons dans le panel game over
        if gameOverPanel != nil {
            for node in nodes(at: point) {
                let name = node.name ?? node.parent?.name
                if name == "replayBtn" {
                    node.parent?.run(SKAction.sequence([
                        SKAction.scale(to: 0.93, duration: 0.07),
                        SKAction.scale(to: 1.0, duration: 0.1)
                    ]))
                    run(SKAction.wait(forDuration: 0.12)) { [weak self] in
                        guard let self else { return }
                        let rootVC = self.view?.window?.rootViewController ?? UIViewController()
                        InterstitialAdManager.shared.maybeShow(trigger: .replay, from: rootVC) { [weak self] in
                            self?.resetGame()
                        }
                    }
                    return
                }
                if name == "dailyLeaderboardBtn" {
                    node.parent?.run(SKAction.sequence([
                        SKAction.scale(to: 0.93, duration: 0.07),
                        SKAction.scale(to: 1.0, duration: 0.1)
                    ]))
                    NotificationCenter.default.post(name: .tenGOShowGameCenter, object: nil,
                                                    userInfo: ["leaderboardID": AppConfig.gameCenterDailyLeaderboardID])
                    return
                }
                if name == "homePanelBtn" {
                    run(SKAction.wait(forDuration: 0.08)) { [weak self] in self?.goBackToMenu() }
                    return
                }
            }
            return  // bloquer le reste du touch si panel visible
        }

        // Bulle restart — réactive uniquement en mode normal (pas de rejeu en Défi)
        let dist = hypot(point.x - restartBubbleNode.position.x, point.y - restartBubbleNode.position.y)
        if mode == .normal && dist < 55 {
            restartBubbleNode.run(SKAction.sequence([
                SKAction.scale(to: 0.88, duration: 0.08),
                SKAction.scale(to: 1.0, duration: 0.12)
            ]))
            resetGame()
            return
        }

        // Boutons boosters (mode normal uniquement)
        if let bar = boosterBar, bar.parent != nil, !bar.isHidden, !isAnimating {
            for (booster, node) in boosterButtons {
                let p = bar.convert(node.position, to: self)
                if hypot(point.x - p.x, point.y - p.y) < 30 {
                    node.run(SKAction.sequence([
                        SKAction.scale(to: 0.88, duration: 0.08),
                        SKAction.scale(to: 1.0, duration: 0.12)
                    ]))
                    handleBoosterTap(booster)
                    return
                }
            }
        }

        // Marteau armé : le prochain tap sur une bulle la détruit.
        if hammerArmed {
            hammerArmed = false
            messageLabel.isHidden = true
            if !isAnimating, let coord = gridCoord(for: point),
               gridModel.cells[coord.row][coord.col]?.isFrozen != true {
                if BoosterManager.shared.consume(.hammer)
                    || (BoosterManager.shared.purchase(.hammer) && BoosterManager.shared.consume(.hammer)) {
                    refreshBoosterButtons()
                    performHammer(at: coord)
                }
            }
            return
        }

        guard !isAnimating else { return }
        guard let coord = gridCoord(for: point) else { return }
        // Une bulle gelée est intraversable tant que le givre n'a pas fondu.
        if gridModel.cells[coord.row][coord.col]?.isFrozen == true { return }

        currentPath = [coord]
        bubbleNodes[coord.row][coord.col]?.setSelected(true)
        let selectValue = gridModel.cells[coord.row][coord.col]?.value ?? 1
        SoundManager.shared.playSelect(bubbleValue: selectValue)
        updateSumLabel()
        updatePathLine()
    }

    override func touchesMoved(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard !isAnimating, !currentPath.isEmpty, let touch = touches.first else { return }
        let point = touch.location(in: self)
        guard let target = gridCoord(for: point) else { return }
        let last = currentPath.last!

        // Même cellule — ne rien faire
        if target.row == last.row && target.col == last.col { return }

        // Backtrack étendu : si la cible est déjà dans le chemin, tronquer jusqu'à elle.
        // Permet de revenir en arrière fluide même sur plusieurs cases (boucles, U-turn).
        if let idx = currentPath.firstIndex(where: { $0.row == target.row && $0.col == target.col }),
           idx < currentPath.count - 1 {
            while currentPath.count > idx + 1 {
                let removed = currentPath.removeLast()
                bubbleNodes[removed.row][removed.col]?.setSelected(false)
            }
            SoundManager.shared.playBacktrack()
            updateSumLabel()
            updatePathLine()
            return
        }

        // Sinon, étendre le chemin par pas king-move successifs vers la cible.
        // Fix les sauts de cellule en swipe diagonal rapide (interpolation 60Hz vs vitesse du doigt).
        walkPath(toward: target)
    }

    /// Avance le chemin un pas king-move à la fois vers `target`, jusqu'à atteindre
    /// la cible ou rencontrer une cellule invalide (hors grille, vide, déjà dans le
    /// chemin, ou somme dépassée).
    private func walkPath(toward target: (row: Int, col: Int)) {
        while let last = currentPath.last,
              !(last.row == target.row && last.col == target.col) {
            let dr = (target.row - last.row).signum()
            let dc = (target.col - last.col).signum()
            let next = (row: last.row + dr, col: last.col + dc)
            if !tryAppendCell(next) { break }
        }
    }

    /// Tente d'ajouter `coord` au chemin courant. Retourne `true` si ajouté.
    @discardableResult
    private func tryAppendCell(_ coord: (row: Int, col: Int)) -> Bool {
        // Bornes
        guard coord.row >= 0, coord.row < GridModel.rows,
              coord.col >= 0, coord.col < GridModel.cols else { return false }
        // Déjà dans le chemin
        if currentPath.contains(where: { $0.row == coord.row && $0.col == coord.col }) { return false }
        // Cellule non vide et non gelée
        guard let bubble = gridModel.cells[coord.row][coord.col], !bubble.isFrozen else { return false }
        // Contrainte de somme
        guard gridModel.pathSum(currentPath) + bubble.value <= 10 else { return false }

        currentPath.append(coord)
        bubbleNodes[coord.row][coord.col]?.setSelected(true)
        SoundManager.shared.playConnect(bubbleValue: bubble.value)
        updateSumLabel()
        updatePathLine()
        return true
    }

    override func touchesEnded(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard !isAnimating, !currentPath.isEmpty else { return }
        if gridModel.pathSum(currentPath) == 10 {
            HapticManager.medium()
            commitPath()
        } else {
            cancelPath()
        }
    }

    override func touchesCancelled(_ touches: Set<UITouch>, with event: UIEvent?) {
        cancelPath()
    }

    // MARK: - Path

    private func updateSumLabel() { /* somme masquée — l'utilisateur calcule lui-même */ }

    private func updatePathLine() {
        pathLineNode?.removeFromParent()
        pathLineNode = nil
        guard currentPath.count >= 2 else { return }

        let cgPath = CGMutablePath()
        cgPath.move(to: scenePos(row: currentPath[0].row, col: currentPath[0].col))
        for coord in currentPath.dropFirst() {
            cgPath.addLine(to: scenePos(row: coord.row, col: coord.col))
        }

        let container = makeTrailNode(path: cgPath)
        container.zPosition = 5
        addChild(container)
        pathLineNode = container
    }

    /// Construit la ligne de tracé selon le style cosmétique actif (purement visuel).
    private func makeTrailNode(path: CGMutablePath) -> SKNode {
        TrailRenderer.make(path: path,
                           style: CosmeticManager.shared.activeTrail,
                           accent: ThemeManager.shared.active.accent)
    }

    private func cancelPath() {
        for coord in currentPath {
            bubbleNodes[coord.row][coord.col]?.setSelected(false)
        }
        currentPath = []
        pathLineNode?.removeFromParent()
        pathLineNode = nil
        SoundManager.shared.cancelPath()
    }

    // MARK: - Scoring

    private func scoreForPath(length: Int) -> Int {
        switch length {
        case 2: return 10
        case 3: return 30
        case 4: return 100
        default: return 100 + 50 * (length - 4)
        }
    }

    private func updateScoreLabel() {
        scoreLabel.text = "\(score)"
        scoreBubbleNode.run(SKAction.sequence([
            SKAction.scale(to: 1.18, duration: 0.07),
            SKAction.scale(to: 1.0, duration: 0.12)
        ]))
    }

    private func showScorePopup(points: Int, at bubblePos: CGPoint) {
        let popup = SKLabelNode(text: "+\(points)")
        let isBigCombo = points >= 100
        popup.fontName = isBigCombo ? "AvenirNext-Heavy" : "AvenirNext-Medium"
        popup.fontSize = isBigCombo ? 40 : 28
        popup.fontColor = isBigCombo
            ? UIColor(red: 0.92, green: 0.62, blue: 0.18, alpha: 1)
            : UIColor(white: 0.3, alpha: 0.85)
        // Appear above the bubble
        popup.position = CGPoint(x: bubblePos.x, y: bubblePos.y + 30)
        popup.setScale(0.3)
        popup.zPosition = 20
        addChild(popup)

        popup.run(SKAction.sequence([
            // Pop in
            SKAction.scale(to: 1.2, duration: 0.1),
            SKAction.scale(to: 1.0, duration: 0.08),
            // Float up and fade
            SKAction.group([
                SKAction.moveBy(x: 0, y: 70, duration: 0.7),
                SKAction.sequence([
                    SKAction.wait(forDuration: 0.25),
                    SKAction.fadeOut(withDuration: 0.45)
                ])
            ]),
            SKAction.removeFromParent()
        ]))
    }

    // MARK: - Commit sequence

    private func commitPath() {
        isAnimating = true
        pathLineNode?.removeFromParent()
        pathLineNode = nil

        let points = scoreForPath(length: currentPath.count)
        let lastCoord = currentPath.last!
        let popupOrigin = scenePos(row: lastCoord.row, col: lastCoord.col)
        let pathCopy = currentPath
        combosCreated += 1
        longestChain = max(longestChain, currentPath.count)
        currentPath = []

        score += points
        updateScoreLabel()
        showScorePopup(points: points, at: popupOrigin)

        SoundManager.shared.playCombo()
        for coord in pathCopy {
            bubbleNodes[coord.row][coord.col]?.playPopAnimation(completion: {})
            bubbleNodes[coord.row][coord.col] = nil
        }
        gridModel.removeBubbles(at: pathCopy)

        // Défi du jour : un chemin adjacent fait fondre le givre des bulles gelées.
        let thawed = gridModel.thawFrozenBubbles(adjacentTo: pathCopy)
        for pos in thawed { bubbleNodes[pos.row][pos.col]?.thaw() }

        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.26),
            SKAction.run { [weak self] in self?.afterPop() }
        ]))
    }

    private func afterPop() {
        if gridModel.isGridEmpty() {
            if mode == .demo { demoReseed() } else { triggerWin() }
            return
        }

        let falls = gridModel.applyGravity()

        // Collect node references before mutating bubbleNodes
        var movements: [(node: BubbleNode, fromRow: Int, toRow: Int, col: Int)] = []
        for fall in falls {
            if let node = bubbleNodes[fall.fromRow][fall.col] {
                movements.append((node: node, fromRow: fall.fromRow, toRow: fall.toRow, col: fall.col))
            }
        }
        for m in movements { bubbleNodes[m.fromRow][m.col] = nil }
        for m in movements { bubbleNodes[m.toRow][m.col] = m.node }

        var maxDuration: TimeInterval = 0
        for m in movements {
            let rowsDropped = m.fromRow - m.toRow
            let duration = min(0.08 + 0.04 * Double(rowsDropped), 0.4)
            maxDuration = max(maxDuration, duration)
            m.node.playFallAnimation(toY: gridOriginY + CGFloat(m.toRow) * GameScene.cellSize, duration: duration, completion: {})
        }

        if movements.isEmpty {
            afterGravity()
        } else {
            run(SKAction.sequence([
                SKAction.wait(forDuration: maxDuration + 0.05),
                SKAction.run { [weak self] in self?.afterGravity() }
            ]))
        }
    }

    private func afterGravity() {
        if !gridModel.hasValidMove() {
            if mode == .demo { demoReseed() } else { triggerLose() }
        } else {
            isAnimating = false
        }
    }

    // MARK: - Boosters

    static let boosterIcons: [Booster: String] = [
        .hint: "💡", .shuffle: "🔀", .hammer: "🔨"
    ]

    /// Construit la barre de 3 boosters sous la grille (mode normal uniquement).
    private func setupBoosters() {
        guard mode == .normal else { return }
        let bar = SKNode()
        bar.position = CGPoint(x: 0, y: gridBottom - 62)
        bar.zPosition = 8
        addChild(bar)
        boosterBar = bar

        let order: [Booster] = [.hint, .shuffle, .hammer]
        let spacing: CGFloat = 92
        for (i, booster) in order.enumerated() {
            let node = SKNode()
            node.position = CGPoint(x: CGFloat(i - 1) * spacing, y: 0)
            bar.addChild(node)

            let circle = SKShapeNode(circleOfRadius: 26)
            circle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
            circle.strokeColor = UIColor(white: 0.70, alpha: 0.55)
            circle.lineWidth = 1.5
            node.addChild(circle)

            let icon = SKLabelNode(text: GameScene.boosterIcons[booster])
            icon.fontSize = 24
            icon.verticalAlignmentMode = .center
            icon.horizontalAlignmentMode = .center
            node.addChild(icon)

            let badge = SKLabelNode(text: "")
            badge.fontName = "AvenirNext-Bold"
            badge.fontSize = 12
            badge.verticalAlignmentMode = .center
            badge.horizontalAlignmentMode = .center
            badge.position = CGPoint(x: 19, y: -19)
            badge.zPosition = 1
            node.addChild(badge)
            boosterCountLabels[booster] = badge

            boosterButtons.append((booster, node))
        }
        refreshBoosterButtons()
    }

    /// Met à jour les badges (quantité possédée, sinon prix) et l'opacité.
    private func refreshBoosterButtons() {
        for (booster, node) in boosterButtons {
            let count = BoosterManager.shared.count(booster)
            let badge = boosterCountLabels[booster]
            if count > 0 {
                badge?.text = "×\(count)"
                badge?.fontColor = UIColor(white: 0.30, alpha: 1)
                node.alpha = 1.0
            } else {
                badge?.text = "🪙\(booster.price)"
                badge?.fontColor = UIColor(red: 0.85, green: 0.60, blue: 0.15, alpha: 1)
                node.alpha = CoinManager.shared.balance >= booster.price ? 1.0 : 0.45
            }
        }
    }

    private func handleBoosterTap(_ booster: Booster) {
        guard !isAnimating else { return }
        switch booster {
        case .hammer:
            // Arme le ciblage si disponible (consommation différée au tap sur la bulle).
            if BoosterManager.shared.count(booster) > 0 || CoinManager.shared.balance >= booster.price {
                hammerArmed = true
                messageLabel.text = String(localized: "booster.hammer_prompt",
                                           defaultValue: "Touche une bulle à détruire")
                messageLabel.isHidden = false
            } else { denyBooster(booster) }
        case .hint:
            disarmHammer()
            if acquireBooster(booster) { showHint() } else { denyBooster(booster) }
        case .shuffle:
            disarmHammer()
            if acquireBooster(booster) { performShuffle() } else { denyBooster(booster) }
        }
    }

    private func disarmHammer() {
        guard hammerArmed else { return }
        hammerArmed = false
        messageLabel.isHidden = true
    }

    /// Consomme une unité (depuis l'inventaire, sinon achat immédiat). Met à jour les badges.
    private func acquireBooster(_ booster: Booster) -> Bool {
        if BoosterManager.shared.consume(booster) {
            refreshBoosterButtons()
            return true
        }
        if BoosterManager.shared.purchase(booster), BoosterManager.shared.consume(booster) {
            refreshBoosterButtons()
            return true
        }
        return false
    }

    /// Feedback « solde insuffisant » : secousse du bouton.
    private func denyBooster(_ booster: Booster) {
        guard let node = boosterButtons.first(where: { $0.booster == booster })?.node else { return }
        node.run(SKAction.sequence([
            SKAction.moveBy(x: -6, y: 0, duration: 0.05),
            SKAction.moveBy(x: 12, y: 0, duration: 0.05),
            SKAction.moveBy(x: -6, y: 0, duration: 0.05)
        ]))
    }

    /// Indice : met en valeur un chemin valide (somme 10) sans rien consommer de la grille.
    private func showHint() {
        guard let path = gridModel.showcasePath() else { return }
        isAnimating = true
        let pulse = SKAction.sequence([
            SKAction.scale(to: 1.28, duration: 0.24),
            SKAction.scale(to: 1.0, duration: 0.24)
        ])
        for coord in path {
            bubbleNodes[coord.row][coord.col]?.run(SKAction.repeat(pulse, count: 2))
        }
        run(SKAction.sequence([
            SKAction.wait(forDuration: 1.05),
            SKAction.run { [weak self] in self?.isAnimating = false }
        ]))
    }

    /// Mélange : réattribue les valeurs de la grille (toujours jouable) et reconstruit les bulles.
    private func performShuffle() {
        cancelPath()
        isAnimating = true
        gridModel.reshuffleValues()
        rebuildGridNodes()
        HapticManager.medium()
        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.25),
            SKAction.run { [weak self] in self?.isAnimating = false }
        ]))
    }

    /// Reconstruit tous les nœuds de bulles à partir du modèle (après un mélange).
    private func rebuildGridNodes() {
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                bubbleNodes[row][col]?.removeFromParent()
                bubbleNodes[row][col] = nil
            }
        }
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let model = gridModel.cells[row][col] else { continue }
                let node = BubbleNode(value: model.value)
                node.position = scenePos(row: row, col: col)
                if model.isAnchored { node.setAnchored(true) }
                if model.isFrozen { node.setFrozen(true) }
                node.setScale(0.2)
                addChild(node)
                node.run(SKAction.scale(to: 1.0, duration: 0.2))
                bubbleNodes[row][col] = node
            }
        }
    }

    /// Marteau : détruit une bulle puis applique la gravité (réutilise le flux post-pop).
    private func performHammer(at coord: (row: Int, col: Int)) {
        guard gridModel.cells[coord.row][coord.col] != nil else { return }
        isAnimating = true
        bubbleNodes[coord.row][coord.col]?.playPopAnimation(completion: {})
        bubbleNodes[coord.row][coord.col] = nil
        gridModel.removeBubbles(at: [coord])
        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.26),
            SKAction.run { [weak self] in self?.afterPop() }
        ]))
    }

    // MARK: - Win / Lose

    /// Enregistre le score de fin de partie selon le mode.
    private func recordScore() {
        switch mode {
        case .normal:
            GameState.addScore(score)
            GameCenterManager.shared.submitScore(score)
            GameState.clear()
            coinsEarnedThisGame = CoinManager.shared.awardForScore(score)
            AnalyticsService.levelEnd(mode: "normal", score: score, won: isWinState)
        case .daily:
            // Pièces offertes une seule fois, à la première complétion du jour.
            if !DailyChallenge.isCompletedToday() {
                CoinManager.shared.awardDailyChallenge()
            }
            DailyChallenge.markCompleted()
            GameCenterManager.shared.submitDailyScore(score)
            AnalyticsService.levelEnd(mode: "daily", score: score, won: isWinState)
            AnalyticsService.dailyChallengePlayed(score: score)
        case .demo:
            break   // démo : aucun score enregistré ni soumis
        }
    }

    private func triggerWin() {
        SoundManager.shared.playWin()
        InterstitialAdManager.shared.markGameCompleted()
        isWinState = true
        let bonus = 1000
        score += bonus
        updateScoreLabel()
        showScorePopup(points: bonus, at: CGPoint(x: 0, y: 50))
        recordScore()
        run(SKAction.wait(forDuration: 0.5)) { [weak self] in
            self?.showGameOverPanel()
        }
        // Une grille vidée est un moment de satisfaction : on sollicite un avis
        // (iOS plafonne lui-même la fréquence à ~3×/an).
        run(SKAction.wait(forDuration: 1.8)) { [weak self] in
            self?.maybeRequestReview()
        }
    }

    /// Demande un avis App Store après un moment positif (victoire), pour les
    /// joueurs déjà engagés (tutoriel vu) et hors démo. iOS gère le throttling.
    private func maybeRequestReview() {
        guard mode != .demo else { return }
        guard UserDefaults.standard.bool(forKey: AppConfig.UserDefaultsKey.hasSeenTutorial) else { return }
        guard let scene = view?.window?.windowScene else { return }
        if #available(iOS 14.0, *) {
            SKStoreReviewController.requestReview(in: scene)
        } else {
            SKStoreReviewController.requestReview()
        }
    }

    private func triggerLose() {
        SoundManager.shared.playLose()
        InterstitialAdManager.shared.markGameCompleted()
        isWinState = false
        let shake = SKAction.sequence([
            SKAction.moveBy(x: 8,  y: 0, duration: 0.05),
            SKAction.moveBy(x: -16, y: 0, duration: 0.05),
            SKAction.moveBy(x: 16,  y: 0, duration: 0.05),
            SKAction.moveBy(x: -8,  y: 0, duration: 0.05)
        ])
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols { bubbleNodes[row][col]?.run(shake) }
        }
        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.35),
            SKAction.run { [weak self] in
                guard let self = self else { return }
                for row in 0..<GridModel.rows {
                    for col in 0..<GridModel.cols {
                        self.bubbleNodes[row][col]?.run(SKAction.fadeAlpha(to: 0.12, duration: 0.4))
                    }
                }
                self.recordScore()
            },
            SKAction.wait(forDuration: 0.5),
            SKAction.run { [weak self] in self?.showGameOverPanel() }
        ]))
    }

    private func showGameOverPanel() {
        // Boosters indisponibles pendant l'écran de fin.
        hammerArmed = false
        messageLabel.isHidden = true
        boosterBar?.isHidden = true

        let panelW: CGFloat = 500
        let panelH: CGFloat = 480
        let cornerR: CGFloat = 36

        // Overlay plein écran pour noyer la grille
        let overlay = SKShapeNode(rectOf: CGSize(width: size.width * 2, height: size.height * 2))
        overlay.fillColor = UIColor(white: 0.0, alpha: 0.32)
        overlay.strokeColor = .clear
        overlay.position = .zero
        overlay.zPosition = 14
        overlay.alpha = 0
        addChild(overlay)
        overlay.run(SKAction.fadeAlpha(to: 1, duration: 0.3))

        let panel = SKNode()
        panel.position = CGPoint(x: 0, y: 0)
        panel.zPosition = 15
        panel.alpha = 0
        gameOverPanel = panel
        addChild(panel)

        // Ombre simulée
        let shadow = SKShapeNode(rectOf: CGSize(width: panelW + 6, height: panelH + 6), cornerRadius: cornerR)
        shadow.fillColor = UIColor(white: 0.0, alpha: 0.14)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 3, y: -8)
        shadow.zPosition = -1
        panel.addChild(shadow)

        // Carte principale
        let bg = SKShapeNode(rectOf: CGSize(width: panelW, height: panelH), cornerRadius: cornerR)
        bg.fillColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 0.99)
        bg.strokeColor = UIColor(white: 0.72, alpha: 0.30)
        bg.lineWidth = 1
        panel.addChild(bg)

        // Titre
        let titleText = isWinState
            ? String(localized: "game_over.perfect_title")
            : String(localized: "game_over.game_over_title")
        let title = SKLabelNode(text: titleText)
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 44
        title.fontColor = UIColor(white: 0.24, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: 178)
        panel.addChild(title)

        // Séparateur haut
        let sep = SKShapeNode(rectOf: CGSize(width: 300, height: 1))
        sep.fillColor = UIColor(white: 0.78, alpha: 0.55)
        sep.strokeColor = .clear
        sep.position = CGPoint(x: 0, y: 140)
        panel.addChild(sep)

        // Score animé (count-up)
        let ptsLabel = String(localized: "game.points_label")
        let scoreDisplay = SKLabelNode(text: "0 \(ptsLabel)")
        scoreDisplay.fontName = "AvenirNext-Heavy"
        scoreDisplay.fontSize = 64
        scoreDisplay.fontColor = UIColor(white: 0.26, alpha: 1)
        scoreDisplay.verticalAlignmentMode = .center
        scoreDisplay.position = CGPoint(x: 0, y: 80)
        panel.addChild(scoreDisplay)
        animateScoreCountUp(label: scoreDisplay, target: score)

        // Pièces gagnées cette partie (mode normal).
        if coinsEarnedThisGame > 0 {
            let coinLine = SKLabelNode(text: "🪙 +\(coinsEarnedThisGame)")
            coinLine.fontName = "AvenirNext-Bold"
            coinLine.fontSize = 22
            coinLine.fontColor = UIColor(red: 0.85, green: 0.60, blue: 0.15, alpha: 1)
            coinLine.verticalAlignmentMode = .center
            coinLine.position = CGPoint(x: 0, y: 118)
            panel.addChild(coinLine)
        }

        // Record
        let scores = GameState.highScores()
        let isNewRecord = scores.first == score && score > 0

        if isNewRecord {
            let record = SKLabelNode(text: String(localized: "game_over.new_record"))
            record.fontName = "AvenirNext-Bold"
            record.fontSize = 20
            record.fontColor = UIColor(red: 0.92, green: 0.65, blue: 0.20, alpha: 1)
            record.verticalAlignmentMode = .center
            record.position = CGPoint(x: 0, y: 30)
            panel.addChild(record)
        } else if let best = scores.first {
            let bestLabel = SKLabelNode(text: String(format: String(localized: "game_over.best_score"), best))
            bestLabel.fontName = "AvenirNext-UltraLight"
            bestLabel.fontSize = 19
            bestLabel.fontColor = UIColor(white: 0.38, alpha: 1)
            bestLabel.verticalAlignmentMode = .center
            bestLabel.position = CGPoint(x: 0, y: 30)
            panel.addChild(bestLabel)
        }

        // Stats
        let statsLine1 = SKLabelNode(text: String(format: String(localized: "game_over.longest_chain"), longestChain))
        statsLine1.fontName = "AvenirNext-UltraLight"
        statsLine1.fontSize = 17
        statsLine1.fontColor = UIColor(white: 0.38, alpha: 1)
        statsLine1.verticalAlignmentMode = .center
        statsLine1.position = CGPoint(x: 0, y: -12)
        panel.addChild(statsLine1)

        let statsLine2 = SKLabelNode(text: String(format: String(localized: "game_over.combos_created"), combosCreated))
        statsLine2.fontName = "AvenirNext-UltraLight"
        statsLine2.fontSize = 17
        statsLine2.fontColor = UIColor(white: 0.38, alpha: 1)
        statsLine2.verticalAlignmentMode = .center
        statsLine2.position = CGPoint(x: 0, y: -40)
        panel.addChild(statsLine2)

        // Séparateur bas
        let sep2 = SKShapeNode(rectOf: CGSize(width: 300, height: 1))
        sep2.fillColor = UIColor(white: 0.78, alpha: 0.55)
        sep2.strokeColor = .clear
        sep2.position = CGPoint(x: 0, y: -76)
        panel.addChild(sep2)

        // Bouton Rejouer — masqué en mode Défi (une seule partie par jour).
        if mode == .normal {
            let replayBtn = SKNode()
            replayBtn.name = "replayBtn"
            replayBtn.position = CGPoint(x: 0, y: -140)
            panel.addChild(replayBtn)

            let replayBg = SKShapeNode(rectOf: CGSize(width: 320, height: 68), cornerRadius: 34)
            replayBg.fillColor = UIColor(red: 0.82, green: 0.95, blue: 0.88, alpha: 1)
            replayBg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
            replayBg.lineWidth = 1
            replayBtn.addChild(replayBg)

            let replayLabel = SKLabelNode(text: String(localized: "game_over.replay"))
            replayLabel.fontName = "AvenirNext-Medium"
            replayLabel.fontSize = 24
            replayLabel.fontColor = UIColor(white: 0.26, alpha: 1)
            replayLabel.verticalAlignmentMode = .center
            replayBtn.addChild(replayLabel)
        } else {
            // Mode Défi : à la place du rejeu, accès au classement du jour.
            let lbBtn = SKNode()
            lbBtn.name = "dailyLeaderboardBtn"
            lbBtn.position = CGPoint(x: 0, y: -140)
            panel.addChild(lbBtn)

            let lbBg = SKShapeNode(rectOf: CGSize(width: 320, height: 68), cornerRadius: 34)
            lbBg.fillColor = UIColor(red: 0.86, green: 0.82, blue: 0.97, alpha: 1) // lavande (défi)
            lbBg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
            lbBg.lineWidth = 1
            lbBtn.addChild(lbBg)

            let lbLabel = SKLabelNode(text: String(localized: "game_over.daily_leaderboard", defaultValue: "Classement du jour"))
            lbLabel.fontName = "AvenirNext-Medium"
            lbLabel.fontSize = 22
            lbLabel.fontColor = UIColor(white: 0.26, alpha: 1)
            lbLabel.verticalAlignmentMode = .center
            lbBtn.addChild(lbLabel)
        }

        // Bouton Accueil.
        let homeBtn = SKNode()
        homeBtn.name = "homePanelBtn"
        homeBtn.position = CGPoint(x: 0, y: -204)
        panel.addChild(homeBtn)

        let homeBg = SKShapeNode(rectOf: CGSize(width: 200, height: 52), cornerRadius: 26)
        homeBg.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        homeBg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
        homeBg.lineWidth = 1
        homeBtn.addChild(homeBg)

        let homeLabel = SKLabelNode(text: String(localized: "game_over.home"))
        homeLabel.fontName = "AvenirNext-UltraLight"
        homeLabel.fontSize = 19
        homeLabel.fontColor = UIColor(white: 0.42, alpha: 1)
        homeLabel.verticalAlignmentMode = .center
        homeBtn.addChild(homeLabel)

        panel.setScale(0.88)
        panel.run(SKAction.group([
            SKAction.fadeIn(withDuration: 0.38),
            SKAction.scale(to: 1.0, duration: 0.38)
        ]))

        // Interstitielle automatique (1 game over sur 3, dès la 2e partie de la session)
        run(SKAction.sequence([
            SKAction.wait(forDuration: 1.5),
            SKAction.run { [weak self] in
                guard let self, self.gameOverPanel != nil else { return }
                let rootVC = self.view?.window?.rootViewController ?? UIViewController()
                InterstitialAdManager.shared.maybeShow(trigger: .gameOverAuto, from: rootVC) { }
            }
        ]))
    }

    private func animateScoreCountUp(label: SKLabelNode, target: Int) {
        let pts = String(localized: "game.points_label")
        let steps = 24
        let duration = 0.55
        let stepDuration = duration / Double(steps)
        var actions: [SKAction] = []
        for i in 1...steps {
            let value = Int(Double(target) * Double(i) / Double(steps))
            actions.append(SKAction.run { label.text = "\(value) \(pts)" })
            actions.append(SKAction.wait(forDuration: stepDuration))
        }
        actions.append(SKAction.run { label.text = "\(target) \(pts)" })
        label.run(SKAction.sequence(actions))
    }

    private func resetGame() {
        guard !isAnimating || !messageLabel.isHidden else {
            // Allow reset when game-over message is showing
            performReset()
            return
        }
        guard !isAnimating else { return }
        performReset()
    }

    private func performReset() {
        isAnimating = true
        messageLabel.isHidden = true
        backgroundColor = ThemeManager.shared.active.background
        score = 0
        combosCreated = 0
        longestChain = 0
        isWinState = false
        scoreLabel.text = "0"

        // Retirer le panneau game-over et l'overlay
        children.filter { $0.zPosition == 14 }.forEach { $0.removeFromParent() } // overlay
        if let panel = gameOverPanel {
            panel.run(SKAction.sequence([
                SKAction.fadeOut(withDuration: 0.18),
                SKAction.removeFromParent()
            ]))
            gameOverPanel = nil
        }

        let fadeOut = SKAction.sequence([
            SKAction.fadeOut(withDuration: 0.2),
            SKAction.removeFromParent()
        ])
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                bubbleNodes[row][col]?.run(fadeOut)
            }
        }

        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.25),
            SKAction.run { [weak self] in
                guard let self = self else { return }
                // Défi du jour : on rejoue la MÊME grille (déterministe), pas une nouvelle.
                if self.mode == .daily, let today = self.dailyToday {
                    self.gridModel = today.grid
                } else {
                    self.gridModel = GridModel()
                }
                self.bubbleNodes = [[BubbleNode?]](
                    repeating: [BubbleNode?](repeating: nil, count: GridModel.cols),
                    count: GridModel.rows
                )
                self.setupGridAnimated()
                self.coinsEarnedThisGame = 0
                self.hammerArmed = false
                self.boosterBar?.isHidden = false
                self.refreshBoosterButtons()
            }
        ]))
    }

    private func setupGridAnimated() {
        renderObstacles()
        var delay: TimeInterval = 0
        let stagger: TimeInterval = 0.012

        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let model = gridModel.cells[row][col] else { continue }
                let node = BubbleNode(value: model.value)
                node.position = scenePos(row: row, col: col)
                if model.isAnchored { node.setAnchored(true) }
                if model.isFrozen { node.setFrozen(true) }
                node.setScale(0)
                node.alpha = 0
                addChild(node)
                bubbleNodes[row][col] = node

                node.run(SKAction.sequence([
                    SKAction.wait(forDuration: delay),
                    SKAction.group([
                        SKAction.scale(to: 1.0, duration: 0.15),
                        SKAction.fadeIn(withDuration: 0.15)
                    ])
                ]))
                delay += stagger
            }
        }

        run(SKAction.sequence([
            SKAction.wait(forDuration: delay + 0.15),
            SKAction.run { [weak self] in self?.isAnimating = false }
        ]))
    }

    // MARK: - Démo / auto-player

    /// Démarre la boucle d'auto-jeu après un court délai (laisse la grille s'installer).
    private func startDemo() {
        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.6),
            SKAction.run { [weak self] in self?.autoPlayNext() }
        ]), withKey: "demoLoop")
    }

    /// Joue le prochain coup : attend la fin des animations, demande un chemin au
    /// solveur, l'anime « comme un doigt », puis se rappelle. Relance une grille
    /// quand il n'y a plus de coup.
    private func autoPlayNext() {
        guard mode == .demo else { return }
        if isAnimating {
            run(SKAction.sequence([
                SKAction.wait(forDuration: 0.04),
                SKAction.run { [weak self] in self?.autoPlayNext() }
            ]), withKey: "demoLoop")
            return
        }
        guard let path = gridModel.showcasePath(maxLen: 5), path.count >= 2 else {
            demoReseed()
            return
        }
        animateDemoPath(path)
    }

    /// Anime le tracé du chemin cellule par cellule (sélection + trail + sons),
    /// valide, puis enchaîne le coup suivant.
    private func animateDemoPath(_ path: [(row: Int, col: Int)]) {
        let step = 0.085 / demoSpeed
        let commitPause = 0.14 / demoSpeed

        var actions: [SKAction] = [
            SKAction.run { [weak self] in self?.demoBeginPath(at: path[0]) }
        ]
        for coord in path.dropFirst() {
            actions.append(SKAction.wait(forDuration: step))
            actions.append(SKAction.run { [weak self] in self?.tryAppendCell(coord) })
        }
        actions.append(SKAction.wait(forDuration: commitPause))
        actions.append(SKAction.run { [weak self] in
            guard let self else { return }
            if self.gridModel.pathSum(self.currentPath) == 10 {
                HapticManager.medium()
                self.commitPath()
            } else {
                self.cancelPath()
            }
        })
        // commitPath passe isAnimating à true ; autoPlayNext attendra la fin.
        actions.append(SKAction.run { [weak self] in self?.autoPlayNext() })
        run(SKAction.sequence(actions), withKey: "demoLoop")
    }

    /// Amorce un chemin sur `coord` (équivalent d'un touchesBegan).
    private func demoBeginPath(at coord: (row: Int, col: Int)) {
        currentPath = [coord]
        bubbleNodes[coord.row][coord.col]?.setSelected(true)
        let value = gridModel.cells[coord.row][coord.col]?.value ?? 1
        SoundManager.shared.playSelect(bubbleValue: value)
        updatePathLine()
    }

    /// Vide la grille et en regénère une nouvelle (graine décalée) pour enchaîner
    /// les parties en boucle. Le score continue de grimper (effet « ça monte »).
    private func demoReseed() {
        guard mode == .demo else { return }
        isAnimating = true
        demoRound += 1

        let fadeOut = SKAction.sequence([
            SKAction.fadeOut(withDuration: 0.18 / demoSpeed),
            SKAction.removeFromParent()
        ])
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols { bubbleNodes[row][col]?.run(fadeOut) }
        }

        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.24 / demoSpeed),
            SKAction.run { [weak self] in
                guard let self else { return }
                var generator = SeededGenerator(seed: self.demoSeed &+ UInt64(self.demoRound))
                self.gridModel = GridModel(using: &generator)
                self.bubbleNodes = [[BubbleNode?]](
                    repeating: [BubbleNode?](repeating: nil, count: GridModel.cols),
                    count: GridModel.rows
                )
                self.setupGridAnimated()   // remet isAnimating à false en fin d'apparition
            },
            SKAction.wait(forDuration: 0.2 / demoSpeed),
            SKAction.run { [weak self] in self?.autoPlayNext() }
        ]), withKey: "demoLoop")
    }

    // MARK: - Navigation

    private func goBackToMenu() {
        // Le Défi du jour ne se sauvegarde pas dans le slot « Continuer » du mode normal.
        if mode == .normal && !gridModel.isGridEmpty() && !isAnimating {
            GameState.save(gridModel: gridModel, score: score)
        }
        let rootVC = view?.window?.rootViewController ?? UIViewController()
        InterstitialAdManager.shared.maybeShow(trigger: .home, from: rootVC) { [weak self] in
            guard let self else { return }
            let menu = MenuScene(size: self.size)
            menu.scaleMode = .aspectFill
            self.view?.presentScene(menu, transition: SKTransition.fade(withDuration: 0.3))
        }
    }
}
