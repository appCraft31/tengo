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

    enum Mode { case normal, daily, demo, rush }

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

    /// `resuming` : la partie reprend après un aller-retour interne (boutique).
    /// Les effets de bord « début de session » sont alors sautés — voir
    /// `sceneDidLoad`.
    private let resuming: Bool

    init(size: CGSize, savedState: GameState? = nil, resuming: Bool = false) {
        self.savedState = savedState
        self.resuming = resuming
        self.mode = .normal
        super.init(size: size)
    }

    /// Mode Défi du jour : grille déterministe + twist, identique pour tous.
    init(size: CGSize, daily: DailyChallenge.Today) {
        self.mode = .daily
        self.dailyToday = daily
        self.resuming = false
        super.init(size: size)
    }

    /// Mode démo : grille déterministe (graine) jouée automatiquement par le
    /// solveur, pour produire une capture vidéo de gameplay (marketing).
    init(size: CGSize, demoSeed: UInt64, demoSpeed: Double = 1.0) {
        self.mode = .demo
        self.demoSeed = demoSeed
        self.demoSpeed = max(0.25, demoSpeed)
        self.resuming = false
        super.init(size: size)
    }

    /// Mode Rush : 60 secondes chrono, meilleur score possible. Grille non
    /// déterministe (pas de partage entre joueurs, contrairement au Défi du
    /// jour) et régénérée automatiquement si elle se bloque — le chrono seul
    /// doit décider de la fin de partie.
    init(size: CGSize, startRush: Bool) {
        self.mode = .rush
        self.resuming = false
        super.init(size: size)
    }

    required init?(coder aDecoder: NSCoder) {
        self.mode = .normal
        self.resuming = false
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
    /// Conteneur persistant du tracé (alias historique conservé).
    private var pathLineNode: SKNode?
    private var trailContainer: SKNode?
    /// Le trait lui-même, reconstruit à chaque cellule.
    private var strokeNode: SKNode?
    /// Point lumineux de tête, qui suit le doigt sans être reconstruit.
    private var trailComet: SKSpriteNode?
    private var isAnimating = false

    // MARK: - Score

    /// Score réel, crédité dès la validation (sauvegarde, records, Game Center).
    private var score = 0
    /// Score AFFICHÉ, qui ne rattrape `score` qu'à l'arrivée de la bulle « +N »
    /// sur le compteur : le gain se lit alors comme la conséquence du vol, et
    /// non comme un chiffre qui a déjà changé avant que l'animation ne parte.
    private var displayedScore = 0
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
    /// XP créditée à la fin de cette partie (affiché dans le panel game-over).
    private var lastXPResult: LevelManager.GainResult?

    // MARK: - Rush
    static let rushDuration: TimeInterval = 60
    /// Longueur de chaîne à partir de laquelle une chaîne accorde du temps bonus.
    static let rushBonusChainLength = 7
    static let rushBonusSeconds: TimeInterval = 2
    private var rushTimeRemaining: TimeInterval = GameScene.rushDuration
    private var rushTimerLabel: SKLabelNode?
    private var rushEnded = false

    // MARK: - Boosters
    private var boosterBar: SKNode?
    private var boosterButtons: [(booster: Booster, node: SKNode)] = []
    private var boosterCountLabels: [Booster: SKLabelNode] = [:]
    /// Marteau armé : le prochain tap sur une bulle la détruit.
    private var hammerArmed = false
    /// Rayon de tap d'un bouton booster, borné par le demi-espacement effectif
    /// (sur iPad l'espacement est de 88 pt : un rayon fixe de 48 faisait se
    /// chevaucher deux boutons voisins).
    private var boosterHitRadius: CGFloat = 48
    /// Boosters dont le coach-mark d'usage reste à montrer.
    private var boosterCoachQueue: [Booster] = []
    private var boosterCoach: CoachMarkOverlay?
    /// Modale « à court de boosters » et instantané de la partie capturé à son
    /// ouverture (pour ne rien perdre si une animation tourne au moment du tap).
    private var boosterShopPrompt: SKNode?
    private var pendingShopSnapshot: (grid: GridModel, score: Int)?
    /// Pastille « Boutique » qui remplace la barre quand elle est vide.
    private var shopPillNode: SKNode?

    // MARK: - Haptics
    // Haptics via HapticManager (toggle dans les paramètres)

    // MARK: - Juice
    /// Couche décorative. zPosition 12 — surtout PAS 14, que `performReset`
    /// purge à chaque relance de partie.
    private var fxLayer: SKNode!
    /// Caméra dédiée aux secousses : elle ne déplace que le rendu, jamais les
    /// zones tactiles (les touches sont lues en coordonnées scène).
    private var sceneCamera: SKCameraNode!
    private var juice: JuiceDirector!
    /// Meilleure chaîne déjà célébrée dans cette partie (ressenti uniquement,
    /// le score reste strictement inchangé).
    private var celebratedChain = 0

    /// Progression du chemin courant vers 10, dans [0,1].
    private var pathTension: CGFloat {
        min(1, CGFloat(gridModel.pathSum(currentPath)) / 10)
    }

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
        // En capture marketing, le juice reste complet quelle que soit la
        // configuration d'accessibilité de la machine de capture.
        JuiceSettings.forceFull = (mode == .demo)
        setupJuice()
        setupBackground()
        setupUI()
        if mode == .daily, let today = dailyToday {
            gridModel = today.grid
        } else if mode == .demo {
            var generator = SeededGenerator(seed: demoSeed)
            gridModel = GridModel(using: &generator)
        } else if mode == .rush {
            var generator = SystemRandomNumberGenerator()
            gridModel = GridModel(using: &generator)
        } else if let state = savedState {
            gridModel = GridModel(from: state)
            score = state.score
            displayedScore = state.score
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

        // Effets de bord de DÉBUT DE SESSION : à ne jouer qu'une fois par
        // partie. Un aller-retour vers la boutique reconstruit la scène, et
        // `registerGameAndMaybeRequest` n'est PAS idempotent : il incrémente le
        // compteur de parties qui déclenche la demande d'autorisation de
        // notifications. Sans ce garde, faire deux allers-retours suffirait à
        // provoquer la popup système prématurément.
        if !resuming {
            let streakProtected = StreakManager.shared.registerPlay()
            StreakManager.shared.awardMilestones(currentStreak: StreakManager.shared.current)
            if streakProtected {
                flashMessage(String(localized: "streak.protected"), duration: 2.4)
            }
            NotificationManager.shared.registerGameAndMaybeRequest()
        }
        setupBoosters()

        if mode == .rush {
            startRushCountdown()
        }
    }

    /// Couche d'effets + caméra de secousse. Créée avant tout le reste et
    /// jamais retirée : `performReset` ne purge que la zPosition 14.
    private func setupJuice() {
        let camera = SKCameraNode()
        camera.position = .zero
        addChild(camera)
        self.camera = camera
        sceneCamera = camera

        let layer = SKNode()
        layer.zPosition = 12
        addChild(layer)
        fxLayer = layer

        juice = JuiceDirector(scene: self, fxLayer: layer, camera: camera)
        // Pas de secousse quand un overlay modal est ouvert : panel et réglages
        // sont enfants de la scène et bougeraient avec elle.
        juice.allowsScreenEffects = { [weak self] in
            guard let self = self else { return false }
            return self.gameOverPanel == nil && self.settingsOverlay?.parent == nil
        }
    }

    override func didMove(to view: SKView) {
        repositionBottomRow(in: view)
        // La barre est maintenant à sa place : les coach-marks peuvent viser juste.
        queueBoosterCoachMarks()
        setupSettingsButton(in: view)
        HapticManager.prepare()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": false])
    }

    /// Bouton paramètres — même composant visuel et même position que sur la
    /// page d'accueil (MenuScene.addIconButton : engrenage, fond teinté logo).
    private func setupSettingsButton(in view: SKView) {
        guard mode != .demo else { return }   // pas de chrome parasite en capture vidéo
        let theme = ThemeManager.shared.active
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleW = view.bounds.width / scale
        let visibleH = view.bounds.height / scale
        // Même géométrie que MenuScene.setupUI (marge haute plancher 47 pt).
        let topInset = max(view.safeAreaInsets.top, 47) / scale
        let x = visibleW / 2 - 58
        let y = visibleH / 2 - topInset - 26

        let container = SKNode()
        container.name = "settingsBtn"
        container.position = CGPoint(x: x, y: y)
        container.zPosition = 10

        let bg = SKShapeNode(circleOfRadius: 24)
        bg.fillColor = theme.logo.withAlphaComponent(0.10)
        bg.strokeColor = theme.logo.withAlphaComponent(0.22)
        bg.lineWidth = 1
        container.addChild(bg)

        let config = UIImage.SymbolConfiguration(pointSize: 21, weight: .medium)
        if let img = UIImage(systemName: "gearshape.fill", withConfiguration: config)?
            .withTintColor(theme.logo, renderingMode: .alwaysOriginal) {
            let sprite = SKSpriteNode(texture: SKTexture(image: img))
            let maxDim = max(img.size.width, img.size.height)
            let target: CGFloat = 24
            sprite.size = CGSize(width: img.size.width / maxDim * target,
                                 height: img.size.height / maxDim * target)
            container.addChild(sprite)
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

    private func repositionBottomRow(in view: SKView, animated: Bool = false) {
        // Calcule la hauteur de scène réellement visible selon l'écran (aspectFill)
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleBottom = -(view.bounds.height / scale) / 2

        let gridEdgeBottom = gridBottom - BubbleNode.bubbleRadius
        let band = gridEdgeBottom - visibleBottom
        let bandMid = (gridEdgeBottom + visibleBottom) / 2

        // Position horizontale par défaut de la rangée de contrôle (⌂ / score / ↺).
        let sideX: CGFloat = 120

        // Barre absente (Défi/démo) OU vide (aucun booster possédé) : la rangée
        // de contrôle reprend toute la bande, exactement comme avant l'existence
        // des boosters. C'est ce qui « masque » la barre, sans `isHidden`.
        guard let bar = boosterBar, !boosterButtons.isEmpty || shopPillNode != nil else {
            homeBubbleNode.position = CGPoint(x: -sideX, y: bandMid)
            scoreBubbleNode.position = CGPoint(x: 0, y: bandMid)
            restartBubbleNode.position = CGPoint(x: sideX, y: bandMid)
            return
        }
        let n = boosterButtons.count
        shopPillNode?.position = .zero

        /// Pose une position, en l'animant quand la barre change de composition.
        func place(_ node: SKNode, _ p: CGPoint) {
            guard animated else {
                node.position = p
                return
            }
            let move = SKAction.move(to: p, duration: 0.22)
            move.timingMode = .easeOut
            node.run(move, withKey: "layout")
        }

        // Hauteur nécessaire pour empiler 2 rangées (booster Ø + écart + score Ø).
        let twoRowNeeded = GameScene.boosterRadius * 2 + 28 + 116
        if band >= twoRowNeeded {
            // iPhone : 2 rangées empilées (boosters au-dessus), cluster centré.
            let rowGap: CGFloat = 116
            let spacing: CGFloat = 132
            bar.position = CGPoint(x: 0, y: bandMid + rowGap / 2)
            // Cluster centré quel que soit le nombre de boutons.
            // n=3 → -132, 0, 132 (identique à la mise en page historique).
            for (i, item) in boosterButtons.enumerated() {
                place(item.node, CGPoint(x: (CGFloat(i) - CGFloat(n - 1) / 2) * spacing, y: 0))
            }
            boosterHitRadius = min(48, spacing / 2 - 2)
            let rowY = bandMid - rowGap / 2
            homeBubbleNode.position = CGPoint(x: -sideX, y: rowY)
            scoreBubbleNode.position = CGPoint(x: 0, y: rowY)
            restartBubbleNode.position = CGPoint(x: sideX, y: rowY)
        } else {
            // iPad (bande courte) : une seule rangée horizontale, boosters à gauche,
            // contrôles à droite — exploite la largeur disponible, rien n'est masqué.
            bar.position = CGPoint(x: 0, y: bandMid)
            // Bloc ancré à droite (bord droit figé à -112) : les boutons
            // ⌂ / score / ↺ ne bougent jamais, quel que soit le nombre de
            // boosters. n=3 → -288, -200, -112 (mise en page historique).
            let spacing: CGFloat = 88
            for (i, item) in boosterButtons.enumerated() {
                place(item.node, CGPoint(x: -112 - CGFloat(n - 1 - i) * spacing, y: 0))
            }
            boosterHitRadius = min(48, spacing / 2 - 2)
            homeBubbleNode.position = CGPoint(x: 92, y: bandMid)
            scoreBubbleNode.position = CGPoint(x: 200, y: bandMid)
            restartBubbleNode.position = CGPoint(x: 308, y: bandMid)
        }
    }

    // MARK: - Background

    private var bubbleColors: [UIColor] { ThemeManager.shared.active.bubbles }

    private func setupBackground() {
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
    }

    private func setupUI() {
        let logoY = gridTop + 95

        if mode == .rush {
            // Le chrono remplace le logo décoratif : c'est l'information la
            // plus importante à l'écran en Rush.
            let timer = SKLabelNode(text: "60")
            timer.fontName = "AvenirNext-Heavy"
            timer.fontSize = 48
            timer.fontColor = ThemeManager.shared.active.logo
            timer.verticalAlignmentMode = .center
            timer.horizontalAlignmentMode = .center
            timer.position = CGPoint(x: 0, y: logoY)
            addChild(timer)
            rushTimerLabel = timer
        } else {
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
        }

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

        // Modale « à court de boosters » : bloque le reste du touch.
        if let prompt = boosterShopPrompt, prompt.parent != nil {
            for node in nodes(at: point) {
                switch node.name ?? node.parent?.name ?? "" {
                case "boosterPromptShop":
                    navigateToBoutique(reason: "empty_bar")
                    return
                case "boosterPromptClose", "boosterPromptDim":
                    dismissBoosterShopPrompt()
                    pendingShopSnapshot = nil
                    return
                default: continue
                }
            }
            return
        }

        // Coach-mark d'usage : n'importe quel tap le referme et passe au
        // suivant. Le tap est consommé pour ne pas déclencher le booster
        // pointé par mégarde.
        if let coach = boosterCoach, coach.parent != nil {
            dismissBoosterCoach(handledAt: point)
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
            // Pastille boutique (barre vide) : accès direct, sans modale — le
            // joueur a explicitement tapé « Boutique ».
            if let pill = shopPillNode {
                let p = bar.convert(pill.position, to: self)
                if abs(point.x - p.x) < 98, abs(point.y - p.y) < 26 {
                    pill.run(SKAction.sequence([
                        SKAction.scale(to: 0.93, duration: 0.07),
                        SKAction.scale(to: 1.0, duration: 0.1)
                    ]))
                    pendingShopSnapshot = gridModel.isGridEmpty() ? nil : (gridModel, score)
                    run(SKAction.wait(forDuration: 0.12)) { [weak self] in
                        self?.navigateToBoutique(reason: "empty_bar")
                    }
                    return
                }
            }
            for (booster, node) in boosterButtons {
                let p = bar.convert(node.position, to: self)
                if hypot(point.x - p.x, point.y - p.y) < boosterHitRadius {
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
                // Consommation seule : plus d'achat implicite (80 pièces
                // pouvaient partir ici sans que le joueur ne voie rien).
                if BoosterManager.shared.consume(.hammer) {
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
        let selectValue = gridModel.cells[coord.row][coord.col]?.value ?? 1
        bubbleNodes[coord.row][coord.col]?.setSelected(true, tension: pathTension)
        SoundManager.shared.playSelect(bubbleValue: selectValue)
        juice.onSelect()
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
        // Contrainte de somme — refus signalé au joueur, qui n'avait jusqu'ici
        // aucun retour quand son doigt passait sur une bulle « de trop ».
        // On ne révèle aucun chiffre : juste « pas celle-là ».
        guard gridModel.pathSum(currentPath) + bubble.value <= 10 else {
            if let node = bubbleNodes[coord.row][coord.col] {
                juice.onRejected(node: node, at: coord)
            }
            return false
        }

        currentPath.append(coord)
        let tension = pathTension
        bubbleNodes[coord.row][coord.col]?.setSelected(true, tension: tension)
        SoundManager.shared.playConnect(bubbleValue: bubble.value, tension: Double(tension))
        juice.onAppend(tension: tension)
        updateSumLabel()
        updatePathLine()
        return true
    }

    override func touchesEnded(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard !isAnimating, !currentPath.isEmpty else { return }
        if gridModel.pathSum(currentPath) == 10 {
            // L'haptique de validation est portée par le JuiceDirector
            // (motif proportionné à la longueur), déclenché dans commitPath.
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
        let tension = pathTension

        // Les bulles déjà sélectionnées suivent la tension : la montée vers 10
        // se ressent sur tout le chemin, pas seulement sur la dernière bulle.
        for coord in currentPath {
            bubbleNodes[coord.row][coord.col]?.setSelected(true, tension: tension)
        }

        // Le trait lui-même est reconstruit à chaque cellule ; la comète, elle,
        // vit sur le conteneur persistant et survit donc au rebuild — c'est ce
        // qui donne la sensation de continuité sous le doigt.
        strokeNode?.removeFromParent()
        strokeNode = nil

        guard currentPath.count >= 2 else {
            trailContainer?.removeFromParent()
            trailContainer = nil
            trailComet = nil
            return
        }

        let container = ensureTrailContainer()

        let cgPath = CGMutablePath()
        cgPath.move(to: scenePos(row: currentPath[0].row, col: currentPath[0].col))
        for coord in currentPath.dropFirst() {
            cgPath.addLine(to: scenePos(row: coord.row, col: coord.col))
        }

        let stroke = makeTrailNode(path: cgPath, tension: tension)
        container.addChild(stroke)
        strokeNode = stroke

        if let head = currentPath.last {
            moveComet(to: scenePos(row: head.row, col: head.col), tension: tension)
        }
    }

    /// Conteneur persistant du tracé : seuls ses enfants « stroke » sont
    /// remplacés, pour que les animations posées dessus ne soient pas tuées.
    private func ensureTrailContainer() -> SKNode {
        if let existing = trailContainer { return existing }
        let container = SKNode()
        container.zPosition = 5
        addChild(container)
        trailContainer = container
        pathLineNode = container
        return container
    }

    private func moveComet(to position: CGPoint, tension: CGFloat) {
        guard JuiceSettings.motionEnabled else { return }
        let comet: SKSpriteNode
        if let existing = trailComet {
            comet = existing
        } else {
            comet = SKSpriteNode(texture: FXTextures.dot)
            comet.blendMode = .add
            comet.colorBlendFactor = 1
            comet.zPosition = 2
            comet.position = position
            trailContainer?.addChild(comet)
            trailComet = comet
        }
        let accent = ThemeManager.shared.active.accent
        comet.color = accent.mixedWithWhite(0.25 + 0.45 * tension)
        let size = 26 + 16 * tension
        comet.size = CGSize(width: size, height: size)
        comet.alpha = 0.45 + 0.35 * tension
        comet.run(SKAction.move(to: position, duration: 0.06))
    }

    /// Construit la ligne de tracé selon le style cosmétique actif (purement visuel).
    private func makeTrailNode(path: CGMutablePath, tension: CGFloat) -> SKNode {
        TrailRenderer.make(path: path,
                           style: CosmeticManager.shared.activeTrail,
                           accent: ThemeManager.shared.active.accent,
                           tension: tension)
    }

    private func cancelPath() {
        for coord in currentPath {
            bubbleNodes[coord.row][coord.col]?.setSelected(false)
        }
        currentPath = []
        clearTrail()
        SoundManager.shared.cancelPath()
    }

    /// Démonte intégralement le tracé (trait + comète + conteneur).
    private func clearTrail() {
        strokeNode?.removeFromParent()
        strokeNode = nil
        trailComet?.removeFromParent()
        trailComet = nil
        trailContainer?.removeFromParent()
        trailContainer = nil
        pathLineNode = nil
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

    /// Met à jour le chiffre ; le pulse visuel est joué à l'arrivée du vol
    /// de la bulle « +N » (showScorePopup), pas ici.
    private func updateScoreLabel() {
        scoreLabel.text = "\(displayedScore)"
    }

    /// Aligne l'affichage sur le score réel, sans attendre les bulles encore en
    /// vol. Indispensable en fin de partie : le panneau et les records doivent
    /// montrer le total, jamais un compteur en retard d'une animation.
    private func syncDisplayedScore() {
        guard displayedScore != score else { return }
        displayedScore = score
        updateScoreLabel()
    }

    /// « +N » dans une mini-bulle qui pop à la dernière bulle du chemin puis
    /// s'envole vers la bulle de score. Chaque appel crée ses propres nœuds :
    /// les popups rapprochés coexistent sans état partagé.
    private func showScorePopup(points: Int, at bubblePos: CGPoint, tier: PopTier = .small) {
        let isBigCombo = points >= 100
        let radius: CGFloat = isBigCombo ? 32 : 26
        let tint = isBigCombo
            ? UIColor(red: 0.92, green: 0.62, blue: 0.18, alpha: 1)
            : UIColor(white: 0.3, alpha: 0.85)

        let flight = SKNode()
        flight.position = CGPoint(x: bubblePos.x, y: bubblePos.y + 30)
        flight.zPosition = 20
        flight.setScale(0.0)
        addChild(flight)

        // Halo doré sur les grandes chaînes : la récompense se voit avant même
        // que le chiffre ne soit lu.
        if tier == .large, JuiceSettings.motionEnabled {
            let halo = SKSpriteNode(texture: FXTextures.dot)
            halo.size = CGSize(width: radius * 4, height: radius * 4)
            halo.color = UIColor(red: 0.98, green: 0.80, blue: 0.35, alpha: 1)
            halo.colorBlendFactor = 1
            halo.blendMode = .add
            halo.alpha = 0.55
            halo.zPosition = -1
            flight.addChild(halo)
            halo.run(SKAction.sequence([
                SKAction.group([
                    SKAction.scale(to: 1.5, duration: 0.35),
                    SKAction.fadeOut(withDuration: 0.35)
                ]),
                SKAction.removeFromParent()
            ]))
        }

        let circle = SKShapeNode(circleOfRadius: radius)
        circle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 0.92)
        circle.strokeColor = tint.withAlphaComponent(0.8)
        circle.lineWidth = 2
        flight.addChild(circle)

        let label = SKLabelNode(text: "+\(points)")
        label.fontName = isBigCombo ? "AvenirNext-Heavy" : "AvenirNext-DemiBold"
        label.fontSize = isBigCombo ? 26 : 20
        label.fontColor = tint
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .center
        flight.addChild(label)

        flight.run(SKAction.sequence([
            // Pop-in
            SKAction.scale(to: 1.15, duration: 0.10),
            SKAction.scale(to: 1.0, duration: 0.05),
            SKAction.wait(forDuration: 0.25),
            // Vol : la cible est convertie MAINTENANT (le layout peut avoir bougé).
            SKAction.run { [weak self, weak flight] in
                guard let self, let flight else { return }
                let target = self.scoreBubbleNode.position
                let path = UIBezierPath()
                path.move(to: flight.position)
                let mid = CGPoint(x: (flight.position.x + target.x) / 2,
                                  y: (flight.position.y + target.y) / 2)
                let side: CGFloat = flight.position.x <= target.x ? 80 : -80
                path.addQuadCurve(to: target, controlPoint: CGPoint(x: mid.x + side, y: mid.y))
                let follow = SKAction.follow(path.cgPath, asOffset: false,
                                             orientToPath: false, duration: 0.5)
                follow.timingMode = .easeIn
                flight.run(SKAction.sequence([
                    SKAction.group([
                        follow,
                        SKAction.scale(to: 0.45, duration: 0.5),
                        SKAction.fadeAlpha(to: 0.7, duration: 0.5)
                    ]),
                    SKAction.run { [weak self] in
                        guard let self else { return }
                        // Le compteur ne monte qu'ICI : la bulle vient de le
                        // percuter. Plafonné au score réel, pour qu'un ordre
                        // d'arrivée inattendu ne le fasse jamais dépasser.
                        self.displayedScore = min(self.displayedScore + points, self.score)
                        self.updateScoreLabel()
                        self.scoreBubbleNode.run(SKAction.sequence([
                            SKAction.scale(to: 1.18, duration: 0.07),
                            SKAction.scale(to: 1.0, duration: 0.12)
                        ]))
                    },
                    SKAction.removeFromParent()
                ]))
            }
        ]))
    }

    // MARK: - Commit sequence

    private func commitPath() {
        isAnimating = true
        clearTrail()

        let points = scoreForPath(length: currentPath.count)
        let lastCoord = currentPath.last!
        let popupOrigin = scenePos(row: lastCoord.row, col: lastCoord.col)
        let pathCopy = currentPath
        let length = pathCopy.count
        combosCreated += 1
        longestChain = max(longestChain, length)
        currentPath = []
        if mode != .demo {
            MissionManager.shared.reportMove()
            MissionManager.shared.reportChain(length: length)
        }
        if mode == .rush && length >= GameScene.rushBonusChainLength {
            awardRushTimeBonus()
        }

        // Palier d'intensité. Battre son record de chaîne dans la partie fait
        // monter le ressenti d'un cran — le score, lui, ne bouge pas d'un point.
        var tier = PopTier(length: length)
        if length > celebratedChain && length >= 3 {
            celebratedChain = length
            tier = tier.boosted
        }

        // Le score réel est crédité tout de suite (sauvegarde, records) ; le
        // compteur affiché, lui, attend l'arrivée de la bulle « +N ».
        score += points
        if mode != .demo { MissionManager.shared.reportScore(points) }
        showScorePopup(points: points, at: popupOrigin, tier: tier)

        SoundManager.shared.playCombo(length: length)
        juice.onCommit(tier: tier, length: length)
        pushNeighbors(of: pathCopy, tier: tier)

        for (index, coord) in pathCopy.enumerated() {
            if let bubble = bubbleNodes[coord.row][coord.col] {
                let burst = PopEffects.makeBurst(color: BubbleNode.color(for: bubble.value),
                                                 tier: tier)
                burst.position = scenePos(row: coord.row, col: coord.col)
                // Décalage libre : le burst est décoratif et vit dans fxLayer,
                // il n'est attendu par personne.
                burst.alpha = 0
                burst.run(SKAction.sequence([
                    SKAction.wait(forDuration: TimeInterval(index) * 0.020),
                    SKAction.fadeIn(withDuration: 0.01)
                ]))
                fxLayer.addChild(burst)

                // Décalage PLAFONNÉ : la disparition des bulles, elle, doit
                // tenir dans la fenêtre de 0,26 s avant la gravité.
                let popDelay = min(TimeInterval(index) * 0.012, 0.08)
                bubble.run(SKAction.sequence([
                    SKAction.wait(forDuration: popDelay),
                    SKAction.run { bubble.playPopAnimation(completion: {}) }
                ]))
            }
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

    /// Souffle de la chaîne sur les bulles voisines. Purement décoratif :
    /// aucune position du modèle n'est touchée, et `nudge` repose sur des
    /// déplacements absolus annulés avant toute chute.
    private func pushNeighbors(of path: [(row: Int, col: Int)], tier: PopTier) {
        let push = tier.neighborPush
        guard push > 0, JuiceSettings.motionEnabled else { return }

        let inPath = Set(path.map { $0.row * GridModel.cols + $0.col })
        var pushed = Set<Int>()

        for coord in path {
            for dr in -1...1 {
                for dc in -1...1 where !(dr == 0 && dc == 0) {
                    let r = coord.row + dr
                    let c = coord.col + dc
                    guard r >= 0, r < GridModel.rows, c >= 0, c < GridModel.cols else { continue }
                    let key = r * GridModel.cols + c
                    guard !inPath.contains(key), !pushed.contains(key),
                          let node = bubbleNodes[r][c] else { continue }
                    pushed.insert(key)
                    node.nudge(dx: CGFloat(dc) * push, dy: CGFloat(dr) * push)
                }
            }
        }
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
            if mode == .demo {
                demoReseed()
            } else if mode == .rush {
                // Le chrono seul décide de la fin : une grille bloquée se
                // régénère au lieu de mettre fin à la partie.
                performShuffle()
            } else {
                triggerLose()
            }
        } else {
            isAnimating = false
            // Filet de sécurité : un coach-mark reporté parce que la scène était
            // occupée doit finir par s'afficher.
            showNextBoosterCoach()
        }
    }

    // MARK: - Boosters

    static let boosterRadius: CGFloat = 42

    /// Ordre d'affichage stable des boosters dans la barre.
    private static let boosterOrder: [Booster] = [.hint, .shuffle, .hammer]

    /// Crée le conteneur de la barre (vide). Son contenu est piloté
    /// exclusivement par `refreshBoosterButtons()`.
    private func setupBoosters() {
        guard mode == .normal else { return }
        let bar = SKNode()
        bar.zPosition = 8
        addChild(bar)
        boosterBar = bar
        refreshBoosterButtons()
    }

    /// Construit le bouton rond d'un booster (sans le positionner).
    private func makeBoosterButton(_ booster: Booster) -> SKNode {
        let node = SKNode()

        let circle = SKShapeNode(circleOfRadius: GameScene.boosterRadius)
        circle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        circle.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        circle.lineWidth = 1.5
        node.addChild(circle)

        node.addChild(BoosterIcon.make(booster, size: 44, color: UIColor(white: 0.42, alpha: 1)))

        // Badge quantité : pastille en bas à droite du bouton.
        let badgePill = SKShapeNode(circleOfRadius: 17)
        badgePill.fillColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        badgePill.strokeColor = UIColor(white: 0.70, alpha: 0.5)
        badgePill.lineWidth = 1
        badgePill.position = CGPoint(x: 31, y: -31)
        badgePill.zPosition = 1
        node.addChild(badgePill)

        let badge = SKLabelNode(text: "")
        badge.fontName = "AvenirNext-Bold"
        badge.fontSize = 14
        badge.fontColor = UIColor(white: 0.30, alpha: 1)
        badge.verticalAlignmentMode = .center
        badge.horizontalAlignmentMode = .center
        badge.position = CGPoint(x: 31, y: -31)
        badge.zPosition = 2
        node.addChild(badge)
        boosterCountLabels[booster] = badge

        return node
    }

    /// Synchronise la barre sur l'inventaire : **seuls les boosters possédés
    /// sont affichés**. Un booster à 0 n'a rien à faire à l'écran — l'ancienne
    /// version montrait trois icônes grisées avec un prix hors de portée dès la
    /// première partie.
    ///
    /// Le nom est conservé : ses quatre sites d'appel continuent de fonctionner.
    private func refreshBoosterButtons() {
        guard mode == .normal, let bar = boosterBar else { return }

        let wanted = GameScene.boosterOrder.filter { BoosterManager.shared.count($0) > 0 }
        let current = boosterButtons.map { $0.booster }

        // Retraits (le booster vient d'être épuisé).
        for (booster, node) in boosterButtons where !wanted.contains(booster) {
            boosterCountLabels[booster] = nil
            node.run(SKAction.sequence([
                SKAction.group([
                    SKAction.fadeOut(withDuration: 0.16),
                    SKAction.scale(to: 0.2, duration: 0.16)
                ]),
                SKAction.removeFromParent()
            ]))
        }
        boosterButtons.removeAll { !wanted.contains($0.booster) }

        // Ajouts, dans l'ordre canonique.
        for booster in wanted where !current.contains(booster) {
            let node = makeBoosterButton(booster)
            bar.addChild(node)
            boosterButtons.append((booster, node))
            animateBoosterAppearance(node)
        }
        boosterButtons.sort { a, b in
            let ia = GameScene.boosterOrder.firstIndex(of: a.booster) ?? 0
            let ib = GameScene.boosterOrder.firstIndex(of: b.booster) ?? 0
            return ia < ib
        }

        // Badges.
        for (booster, _) in boosterButtons {
            boosterCountLabels[booster]?.text = "×\(BoosterManager.shared.count(booster))"
        }

        syncShopPill(in: bar, barIsEmpty: wanted.isEmpty)

        if wanted != current, let view = view {
            repositionBottomRow(in: view, animated: !current.isEmpty && !wanted.isEmpty)
        }
        queueBoosterCoachMarks()
    }

    /// Pastille « Boutique » affichée à la place de la barre quand le joueur
    /// n'a plus aucun booster.
    ///
    /// Sans elle le funnel serait mort : les boosters non possédés n'étant plus
    /// affichés, `denyBooster` n'est quasiment plus atteignable. Elle
    /// n'apparaît que lorsqu'elle a du sens — jamais pendant les toutes
    /// premières parties, où la barre doit rester vide.
    private func syncShopPill(in bar: SKNode, barIsEmpty: Bool) {
        let defaults = UserDefaults.standard
        let deserved = barIsEmpty
            && (CoinManager.shared.balance >= Booster.hint.price
                || defaults.bool(forKey: AppConfig.UserDefaultsKey.hasSeenShopPurchaseGuide))

        guard deserved else {
            shopPillNode?.removeFromParent()
            shopPillNode = nil
            return
        }
        guard shopPillNode == nil else { return }

        let node = SKNode()
        node.name = "boosterShopPill"

        let pill = SKShapeNode(rectOf: CGSize(width: 196, height: 52), cornerRadius: 26)
        pill.name = "boosterShopPill"
        pill.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        pill.strokeColor = UIColor(white: 0.70, alpha: 0.55)
        pill.lineWidth = 1.5
        node.addChild(pill)

        let icon = BoosterIcon.make(.hint, size: 26, color: UIColor(white: 0.42, alpha: 1))
        icon.position = CGPoint(x: -64, y: 0)
        node.addChild(icon)

        let label = SKLabelNode(text: String(localized: "game.shop_button", defaultValue: "Boutique"))
        label.name = "boosterShopPill"
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 18
        label.fontColor = UIColor(white: 0.35, alpha: 1)
        label.verticalAlignmentMode = .center
        label.position = CGPoint(x: 12, y: 0)
        node.addChild(label)

        bar.addChild(node)
        shopPillNode = node
    }

    // MARK: - Coach-marks d'usage

    private func boosterCoachKey(_ booster: Booster) -> String {
        AppConfig.UserDefaultsKey.boosterCoachSeenPrefix + booster.rawValue
    }

    /// Reconstruit la file à partir des boosters présents dans la barre et
    /// jamais encore expliqués. Appelé depuis la synchronisation de la barre :
    /// c'est le seul point traversé par TOUS les chemins d'acquisition (achat
    /// en boutique, don, reprise de partie, reset).
    private func queueBoosterCoachMarks() {
        let pending = boosterButtons.map { $0.booster }.filter {
            !UserDefaults.standard.bool(forKey: boosterCoachKey($0))
        }
        guard !pending.isEmpty else { return }
        boosterCoachQueue = pending
        showNextBoosterCoach()
    }

    /// Affiche le prochain coach-mark si le moment s'y prête. Les conditions
    /// sont volontairement strictes : expliquer un booster pendant une
    /// animation ou un tracé en cours serait pire que ne rien expliquer.
    private func showNextBoosterCoach() {
        // `view != nil` est indispensable : tant que la scène n'est pas montée,
        // `repositionBottomRow` n'a pas tourné, la barre est encore en (0,0) et
        // le coach-mark figerait un cadre au milieu de la grille.
        guard mode == .normal,
              view != nil,
              boosterCoach == nil,
              gameOverPanel == nil,
              settingsOverlay?.parent == nil,
              !isAnimating, !hammerArmed, currentPath.isEmpty,
              let next = boosterCoachQueue.first,
              let item = boosterButtons.first(where: { $0.booster == next })
        else { return }

        boosterCoach = CoachMarkOverlay.present(in: self, over: item.node, text: next.coachText)
        AnalyticsService.boosterCoachShown(next.rawValue)
    }

    /// Ferme le coach-mark courant et marque le booster comme expliqué.
    /// Le flag est posé À LA FERMETURE, jamais à l'affichage : c'est le défaut
    /// du guide d'achat historique, qui se perdait s'il était ignoré.
    private func dismissBoosterCoach(handledAt point: CGPoint?) {
        guard let coach = boosterCoach else { return }
        if let point = point {
            _ = coach.handleTouch(at: point)
        } else {
            coach.dismiss()
        }
        if let done = boosterCoachQueue.first {
            UserDefaults.standard.set(true, forKey: boosterCoachKey(done))
            boosterCoachQueue.removeFirst()
        }
        boosterCoach = nil
        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.25),
            SKAction.run { [weak self] in self?.showNextBoosterCoach() }
        ]))
    }

    /// Apparition d'un booster fraîchement acquis : il doit se remarquer, c'est
    /// le moment où le joueur apprend qu'il en possède un.
    private func animateBoosterAppearance(_ node: SKNode) {
        guard JuiceSettings.motionEnabled else { return }
        node.setScale(0.1)
        node.alpha = 0
        node.run(SKAction.group([
            SKAction.fadeIn(withDuration: 0.18),
            SKAction.sequence([
                SKAction.scale(to: 1.14, duration: 0.22),
                SKAction.scale(to: 1.0, duration: 0.12)
            ])
        ]))

        let halo = SKShapeNode(circleOfRadius: GameScene.boosterRadius)
        halo.fillColor = .clear
        halo.strokeColor = ThemeManager.shared.active.accent.withAlphaComponent(0.75)
        halo.lineWidth = 3
        halo.zPosition = -1
        node.addChild(halo)
        halo.run(SKAction.sequence([
            SKAction.group([
                SKAction.scale(to: 1.9, duration: 0.45),
                SKAction.fadeOut(withDuration: 0.45)
            ]),
            SKAction.removeFromParent()
        ]))
        HapticManager.light()
    }

    private func handleBoosterTap(_ booster: Booster) {
        guard !isAnimating else { return }
        switch booster {
        case .hammer:
            // Arme le ciblage (consommation différée au tap sur la bulle).
            // Plus de repli sur le solde : la barre n'affiche que le possédé.
            if BoosterManager.shared.count(booster) > 0 {
                hammerArmed = true
                messageLabel.text = String(localized: "booster.hammer_prompt",
                                           defaultValue: "Touche une bulle à détruire")
                messageLabel.isHidden = false
            } else { denyBooster(booster) }
        case .hint:
            disarmHammer()
            // Le chemin d'abord : une grille sans solution ne doit pas coûter
            // un indice au joueur.
            guard let path = gridModel.showcasePath() else {
                flashMessage(String(localized: "booster.hint_none",
                                    defaultValue: "Aucun chemin ici — essaie le mélange"))
                return
            }
            if acquireBooster(booster) { showHint(path: path) } else { denyBooster(booster) }
        case .shuffle:
            disarmHammer()
            if acquireBooster(booster) { performShuffle() } else { denyBooster(booster) }
        }
    }

    /// Message éphémère dans la zone de consigne (réutilise `messageLabel`).
    private func flashMessage(_ text: String, duration: TimeInterval = 1.6) {
        messageLabel.text = text
        messageLabel.isHidden = false
        messageLabel.removeAction(forKey: "flashMessage")
        messageLabel.run(SKAction.sequence([
            SKAction.wait(forDuration: duration),
            SKAction.run { [weak self] in
                guard let self = self, !self.hammerArmed else { return }
                self.messageLabel.isHidden = true
            }
        ]), withKey: "flashMessage")
    }

    private func disarmHammer() {
        guard hammerArmed else { return }
        hammerArmed = false
        messageLabel.isHidden = true
    }

    /// Consomme une unité de l'inventaire. Met à jour les badges.
    ///
    /// N'achète PLUS implicitement : un booster absent de l'inventaire n'est de
    /// toute façon plus affiché dans la barre. L'ancienne version enchaînait un
    /// `purchase()` silencieux — jusqu'à 80 pièces pouvaient partir d'un seul
    /// tap, sans confirmation ni trace visible du débit.
    private func acquireBooster(_ booster: Booster) -> Bool {
        guard BoosterManager.shared.consume(booster) else { return false }
        AnalyticsService.boosterUsed(booster.rawValue)
        refreshBoosterButtons()
        return true
    }

    // MARK: - Modale « à court de boosters »

    /// Explique où trouver des boosters et ouvre la boutique — l'ancienne
    /// version se contentait d'une secousse muette, sans issue.
    private func presentBoosterShopPrompt() {
        guard mode == .normal, boosterShopPrompt == nil, gameOverPanel == nil else { return }

        // Snapshot AVANT toute interaction : `isAnimating` peut devenir vrai
        // entre l'ouverture de la modale et le tap sur « Boutique ».
        // GridModel est une struct : l'affectation fige une copie de la grille.
        pendingShopSnapshot = gridModel.isGridEmpty() ? nil : (gridModel, score)

        let panel = SKNode()
        panel.zPosition = 15
        panel.name = "boosterPrompt"

        let dim = SKSpriteNode(color: UIColor(white: 0, alpha: 0.45),
                               size: CGSize(width: size.width * 2, height: size.height * 2))
        dim.name = "boosterPromptDim"
        panel.addChild(dim)

        let w: CGFloat = 470, h: CGFloat = 280
        let card = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: 32)
        card.fillColor = UIColor(red: 0.98, green: 0.96, blue: 0.93, alpha: 1)
        card.strokeColor = UIColor(white: 0.70, alpha: 0.25)
        card.lineWidth = 1
        panel.addChild(card)

        let title = SKLabelNode(text: String(localized: "game.no_boosters_title",
                                             defaultValue: "À court de boosters"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 28
        title.fontColor = UIColor(white: 0.25, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: h / 2 - 58)
        panel.addChild(title)

        let body = SKLabelNode(text: String(localized: "game.no_boosters_body",
                                            defaultValue: "Les boosters s'achètent en boutique avec les pièces gagnées en partie."))
        body.fontName = "AvenirNext-Medium"
        body.fontSize = 17
        body.fontColor = UIColor(white: 0.38, alpha: 1)
        body.verticalAlignmentMode = .center
        body.numberOfLines = 3
        body.lineBreakMode = .byWordWrapping
        body.preferredMaxLayoutWidth = w - 72
        body.position = CGPoint(x: 0, y: 10)
        panel.addChild(body)

        addPromptButton(to: panel, name: "boosterPromptShop",
                        title: String(localized: "game.no_boosters_shop", defaultValue: "Boutique"),
                        fill: ThemeManager.shared.active.accent,
                        ink: UIColor(white: 0.20, alpha: 1),
                        at: CGPoint(x: -w / 4 - 6, y: -h / 2 + 58))
        addPromptButton(to: panel, name: "boosterPromptClose",
                        title: String(localized: "game.no_boosters_continue", defaultValue: "Continuer"),
                        fill: UIColor(white: 0.90, alpha: 1),
                        ink: UIColor(white: 0.35, alpha: 1),
                        at: CGPoint(x: w / 4 + 6, y: -h / 2 + 58))

        addChild(panel)
        boosterShopPrompt = panel
        panel.alpha = 0
        panel.run(SKAction.fadeIn(withDuration: 0.18))
    }

    private func addPromptButton(to panel: SKNode, name: String, title: String,
                                 fill: UIColor, ink: UIColor, at position: CGPoint) {
        let pill = SKShapeNode(rectOf: CGSize(width: 190, height: 54), cornerRadius: 27)
        pill.name = name
        pill.fillColor = fill
        pill.strokeColor = .clear
        pill.position = position
        panel.addChild(pill)

        let label = SKLabelNode(text: title)
        label.name = name
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 19
        label.fontColor = ink
        label.verticalAlignmentMode = .center
        label.position = position
        panel.addChild(label)
    }

    private func dismissBoosterShopPrompt() {
        boosterShopPrompt?.removeFromParent()
        boosterShopPrompt = nil
    }

    /// Feedback « plus de boosters » : secousse du bouton, puis explication.
    private func denyBooster(_ booster: Booster) {
        if let node = boosterButtons.first(where: { $0.booster == booster })?.node {
            node.run(SKAction.sequence([
                SKAction.moveBy(x: -6, y: 0, duration: 0.05),
                SKAction.moveBy(x: 12, y: 0, duration: 0.05),
                SKAction.moveBy(x: -6, y: 0, duration: 0.05)
            ]))
        }
        presentBoosterShopPrompt()
    }

    /// Indice : met en valeur un chemin valide (somme 10) sans rien consommer de la grille.
    /// Le chemin est calculé par l'appelant AVANT toute consommation — sinon une
    /// grille sans solution mangeait l'indice sans rien afficher.
    private func showHint(path: [(row: Int, col: Int)]) {
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
        if let bubble = bubbleNodes[coord.row][coord.col] {
            let burst = PopEffects.makeBurst(color: BubbleNode.color(for: bubble.value),
                                             tier: .small)
            burst.position = scenePos(row: coord.row, col: coord.col)
            fxLayer.addChild(burst)
            bubble.playPopAnimation(completion: {})
        }
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
            lastXPResult = LevelManager.shared.awardForGame(score: score,
                                                              longestChain: longestChain,
                                                              combosCreated: combosCreated,
                                                              isPerfect: isWinState)
            MissionManager.shared.reportGameEnded(isPerfect: isWinState)
            AnalyticsService.levelEnd(mode: "normal", score: score, won: isWinState)
        case .daily:
            // Pièces et XP offertes une seule fois, à la première complétion du jour.
            if !DailyChallenge.isCompletedToday() {
                CoinManager.shared.awardDailyChallenge()
                lastXPResult = LevelManager.shared.awardForDailyChallenge(isPerfect: isWinState)
                MissionManager.shared.reportGameEnded(isPerfect: isWinState)
            }
            DailyChallenge.markCompleted()
            GameCenterManager.shared.submitDailyScore(score)
            AnalyticsService.levelEnd(mode: "daily", score: score, won: isWinState)
            AnalyticsService.dailyChallengePlayed(score: score)
        case .rush:
            GameState.addRushScore(score)
            GameCenterManager.shared.submitRushScore(score)
            lastXPResult = LevelManager.shared.awardForRush()
            MissionManager.shared.reportGameEnded(isPerfect: false)
            AnalyticsService.levelEnd(mode: "rush", score: score, won: false)
        case .demo:
            break   // démo : aucun score enregistré ni soumis
        }
    }

    private func triggerWin() {
        // La célébration ne doit pas hériter d'un hit-stop encore actif.
        juice.resetTimeScale()
        SoundManager.shared.playWin()
        InterstitialAdManager.shared.markGameCompleted()
        isWinState = true
        let bonus = 1000
        // Le bonus de victoire a lui aussi sa bulle : on rattrape d'abord les
        // gains encore en vol, puis on laisse le +1000 créditer à son arrivée.
        syncDisplayedScore()
        score += bonus
        if mode != .demo { MissionManager.shared.reportScore(bonus) }
        showScorePopup(points: bonus, at: CGPoint(x: 0, y: 50), tier: .large)
        // Vider la grille est LE moment fort du jeu, et il n'était jusqu'ici
        // souligné par rien.
        juice.onWin(at: CGPoint(x: 0, y: 0),
                    accent: ThemeManager.shared.active.accent)
        recordScore()
        // 1,1 s au lieu de 0,5 : le panel ne doit pas couper la célébration.
        // C'est le seul allongement de timing du chantier, et il est hors jeu.
        run(SKAction.wait(forDuration: 1.1)) { [weak self] in
            self?.showGameOverPanel()
        }
        // Une grille vidée est un moment de satisfaction : on sollicite un avis
        // (iOS plafonne lui-même la fréquence à ~3×/an). Décalé d'autant, pour
        // rester APRÈS l'apparition du panel.
        run(SKAction.wait(forDuration: 2.6)) { [weak self] in
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
        juice.resetTimeScale()
        juice.onLose()

        // Défaite adoucie : les bulles s'affaissent en cascade au lieu de
        // subir une secousse sèche sur 63 nœuds. Perdre parce qu'aucun coup
        // n'existe plus n'est pas une faute du joueur — le retour ne doit pas
        // être punitif.
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let node = bubbleNodes[row][col] else { continue }
                node.cancelNudge()
                guard JuiceSettings.motionEnabled else { continue }
                let drop = SKAction.moveBy(x: 0, y: -6, duration: 0.22)
                drop.timingMode = .easeOut
                node.run(SKAction.sequence([
                    SKAction.wait(forDuration: TimeInterval(row) * 0.02),
                    drop
                ]))
            }
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

    // MARK: - Rush

    /// Décompte de lancement (« READY… 3, 2, 1, GO! »), entrée bloquée le
    /// temps de la séquence.
    private func startRushCountdown() {
        isAnimating = true
        let label = SKLabelNode(text: "")
        label.fontName = "AvenirNext-Heavy"
        label.fontSize = 64
        label.fontColor = ThemeManager.shared.active.logo
        label.verticalAlignmentMode = .center
        label.position = .zero
        label.zPosition = 20
        addChild(label)

        let steps = [
            String(localized: "rush.ready"), "3", "2", "1", String(localized: "rush.go")
        ]
        var actions: [SKAction] = []
        for step in steps {
            actions.append(SKAction.run {
                label.text = step
                label.setScale(0.7)
                label.run(SKAction.scale(to: 1.0, duration: 0.18))
            })
            actions.append(SKAction.wait(forDuration: 0.6))
        }
        actions.append(SKAction.run { [weak self] in
            label.removeFromParent()
            self?.startRushTimer()
        })
        run(SKAction.sequence(actions))
    }

    private func startRushTimer() {
        isAnimating = false
        rushTimeRemaining = GameScene.rushDuration
        updateRushTimerLabel()
        run(SKAction.repeatForever(SKAction.sequence([
            SKAction.wait(forDuration: 1.0),
            SKAction.run { [weak self] in self?.tickRushTimer() }
        ])), withKey: "rushTimer")
    }

    private func tickRushTimer() {
        guard !rushEnded else { return }
        rushTimeRemaining -= 1
        updateRushTimerLabel()
        if rushTimeRemaining <= 0 {
            triggerRushTimeUp()
        }
    }

    private func updateRushTimerLabel() {
        rushTimerLabel?.text = "\(max(0, Int(rushTimeRemaining.rounded())))"
    }

    /// Chaîne particulièrement longue en Rush → bonus de temps, avec un
    /// retour visuel discret près du chrono (le rythme doit rester rapide).
    private func awardRushTimeBonus() {
        rushTimeRemaining += GameScene.rushBonusSeconds
        updateRushTimerLabel()
        guard let timerLabel = rushTimerLabel else { return }
        let bonus = SKLabelNode(text: "+\(Int(GameScene.rushBonusSeconds))s")
        bonus.fontName = "AvenirNext-Bold"
        bonus.fontSize = 20
        bonus.fontColor = UIColor(red: 0.35, green: 0.70, blue: 0.45, alpha: 1)
        bonus.verticalAlignmentMode = .center
        bonus.position = CGPoint(x: timerLabel.position.x + 46, y: timerLabel.position.y)
        bonus.zPosition = 20
        addChild(bonus)
        bonus.run(SKAction.sequence([
            SKAction.group([
                SKAction.moveBy(x: 0, y: 24, duration: 0.6),
                SKAction.fadeOut(withDuration: 0.6)
            ]),
            SKAction.removeFromParent()
        ]))
    }

    /// Fin de partie Rush : le chrono seul décide, jamais la grille.
    private func triggerRushTimeUp() {
        guard !rushEnded else { return }
        rushEnded = true
        removeAction(forKey: "rushTimer")
        isAnimating = true
        isWinState = false
        SoundManager.shared.playLose()
        InterstitialAdManager.shared.markGameCompleted()
        syncDisplayedScore()
        recordScore()
        run(SKAction.wait(forDuration: 0.4)) { [weak self] in
            self?.showGameOverPanel()
        }
    }

    private func showGameOverPanel() {
        // Le panneau affiche le total : plus question d'attendre une bulle.
        syncDisplayedScore()
        // Boosters indisponibles pendant l'écran de fin.
        boosterCoach?.dismiss()
        boosterCoach = nil
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
        let titleText: String
        if mode == .rush {
            titleText = String(localized: "rush.time_up_title")
        } else if isWinState {
            titleText = String(localized: "game_over.perfect_title")
        } else {
            titleText = String(localized: "game_over.game_over_title")
        }
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

        // Pièces et XP gagnées cette partie, sur une même ligne (icône pièce +
        // montant, puis XP si créditée — sans emoji).
        let xpGainedThisGame = lastXPResult?.xpGained ?? 0
        if coinsEarnedThisGame > 0 || xpGainedThisGame > 0 {
            let rewardLine = SKNode()
            rewardLine.position = CGPoint(x: 0, y: 118)

            var pieces: [(node: SKNode, width: CGFloat)] = []

            if coinsEarnedThisGame > 0 {
                let group = SKNode()
                let label = SKLabelNode(text: "+\(coinsEarnedThisGame)")
                label.fontName = "AvenirNext-Bold"
                label.fontSize = 22
                label.fontColor = UIColor(red: 0.78, green: 0.55, blue: 0.12, alpha: 1)
                label.verticalAlignmentMode = .center
                label.horizontalAlignmentMode = .left
                let coin = CoinIcon.make(radius: 12)
                let gap: CGFloat = 8
                let width = 24 + gap + label.frame.width
                coin.position = CGPoint(x: -width / 2 + 12, y: 0)
                label.position = CGPoint(x: -width / 2 + 24 + gap, y: 0)
                group.addChild(coin)
                group.addChild(label)
                pieces.append((group, width))
            }

            if xpGainedThisGame > 0 {
                let label = SKLabelNode(text: String(format: String(localized: "game_over.xp_gained"), xpGainedThisGame))
                label.fontName = "AvenirNext-Bold"
                label.fontSize = 22
                label.fontColor = UIColor(red: 0.31, green: 0.52, blue: 0.85, alpha: 1)
                label.verticalAlignmentMode = .center
                label.horizontalAlignmentMode = .center
                pieces.append((label, label.frame.width))
            }

            let interGap: CGFloat = 28
            let totalWidth = pieces.reduce(0) { $0 + $1.width } + interGap * CGFloat(max(0, pieces.count - 1))
            var cursorX = -totalWidth / 2
            for piece in pieces {
                piece.node.position.x = cursorX + piece.width / 2
                rewardLine.addChild(piece.node)
                cursorX += piece.width + interGap
            }

            panel.addChild(rewardLine)
        }

        // Niveau supérieur atteint cette partie : petite mise en avant discrète
        // entre le titre et le séparateur (événement rare, pas de mise en page dédiée).
        if let xpResult = lastXPResult, xpResult.leveledUp {
            let burst = PopEffects.makeBurst(color: ThemeManager.shared.active.accent, tier: .small)
            burst.position = CGPoint(x: 0, y: 150)
            burst.zPosition = 16
            panel.addChild(burst)

            let levelUpText = String(localized: "game_over.level_up") + " "
                + String(format: String(localized: "game_over.new_level"), xpResult.newLevel, LevelManager.shared.currentTitle)
            let levelUpLabel = SKLabelNode(text: levelUpText)
            levelUpLabel.fontName = "AvenirNext-Bold"
            levelUpLabel.fontSize = 14
            levelUpLabel.fontColor = UIColor(red: 0.31, green: 0.52, blue: 0.85, alpha: 1)
            levelUpLabel.verticalAlignmentMode = .center
            levelUpLabel.position = CGPoint(x: 0, y: 150)
            levelUpLabel.zPosition = 17
            panel.addChild(levelUpLabel)
        }

        // Record
        let scores = mode == .rush ? GameState.rushHighScores() : GameState.highScores()
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

        // Bouton Rejouer — masqué en mode Défi (une seule partie par jour) ;
        // en Rush, "Rejouer" EST le CTA principal (courtes sessions répétées).
        if mode == .normal || mode == .rush {
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
        displayedScore = 0
        combosCreated = 0
        longestChain = 0
        celebratedChain = 0
        isWinState = false
        scoreLabel.text = "0"
        CameraShake.reset(sceneCamera)
        juice.resetTimeScale()

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
                if self.mode == .rush {
                    self.removeAction(forKey: "rushTimer")
                    self.rushEnded = false
                    self.rushTimeRemaining = GameScene.rushDuration
                    self.updateRushTimerLabel()
                    self.startRushCountdown()
                }
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

    /// Ouvre la boutique depuis la partie, en préservant celle-ci.
    ///
    /// Volontairement SANS interstitiel : `goBackToMenu` en déclenche un, mais
    /// on ne monétise pas le chemin qui mène le joueur à dépenser ses pièces.
    private func navigateToBoutique(reason: String) {
        boosterCoach?.dismiss()
        boosterCoach = nil
        disarmHammer()
        cancelPath()
        dismissBoosterShopPrompt()

        // Snapshot pris à l'ouverture de la modale : la partie ne doit pas être
        // perdue si une animation était en cours au moment du tap.
        if let snapshot = pendingShopSnapshot {
            GameState.save(gridModel: snapshot.grid, score: snapshot.score)
        }
        AnalyticsService.shopOpenedFromGame(reason: reason)

        let shop = BoutiqueScene(size: size)
        shop.returnDestination = pendingShopSnapshot != nil ? .game : .menu
        shop.scaleMode = .aspectFill
        pendingShopSnapshot = nil
        view?.presentScene(shop, transition: SKTransition.fade(withDuration: 0.3))
    }

    private func goBackToMenu() {
        boosterCoach?.dismiss()
        boosterCoach = nil
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
