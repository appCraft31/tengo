//
//  GameScene.swift
//  tenGO
//

import SpriteKit
import GameplayKit

class GameScene: SKScene {

    // Kept for GameViewController compatibility
    var entities = [GKEntity]()
    var graphs = [String: GKGraph]()

    // MARK: - Init

    private var savedState: GameState?
    private var homeBubbleNode: SKNode!

    init(size: CGSize, savedState: GameState? = nil) {
        self.savedState = savedState
        super.init(size: size)
    }

    required init?(coder aDecoder: NSCoder) {
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
    private var pathLineNode: SKShapeNode?
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

    // MARK: - Haptics
    private let tapFeedback    = UIImpactFeedbackGenerator(style: .light)
    private let successFeedback = UIImpactFeedbackGenerator(style: .medium)

    // MARK: - UI nodes

    private var scoreLabel: SKLabelNode!
    private var sumLabel: SKLabelNode? // unused — somme masquée volontairement
    private var messageLabel: SKLabelNode!
    private var restartButton: SKLabelNode!

    // MARK: - Setup

    override func sceneDidLoad() {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        removeAllChildren()
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        setupBackground()
        setupUI()
        if let state = savedState {
            gridModel = GridModel(from: state)
            score = state.score
            scoreLabel.text = "\(score)"
        }
        setupGrid()
    }

    override func didMove(to view: SKView) {
        repositionBottomRow(in: view)
        setupSettingsButton(in: view)
        tapFeedback.prepare()
        successFeedback.prepare()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": false])
    }

    private func setupSettingsButton(in view: SKView) {
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

        let icon = SKLabelNode(text: "⚙")
        icon.name = "settingsBtn"
        icon.fontName = "AvenirNext-Medium"
        icon.fontSize = 26
        icon.verticalAlignmentMode = .center
        icon.horizontalAlignmentMode = .center
        icon.fontColor = UIColor(white: 0.45, alpha: 1)
        container.addChild(icon)

        addChild(container)
        settingsBubbleNode = container
    }

    private func presentSettings() {
        guard settingsOverlay == nil else { return }
        let overlay = SettingsOverlay(sceneSize: size)
        overlay.present(in: self)
        settingsOverlay = overlay
    }

    private func repositionBottomRow(in view: SKView) {
        // Calcule la hauteur de scène réellement visible selon l'écran (aspectFill)
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleBottom = -(view.bounds.height / scale) / 2

        let gridEdgeBottom = gridBottom - BubbleNode.bubbleRadius
        let available = gridEdgeBottom - visibleBottom
        let rowY = visibleBottom + available / 2

        homeBubbleNode.position.y  = rowY
        scoreBubbleNode.position.y = rowY
        restartBubbleNode.position.y = rowY
    }

    // MARK: - Background

    private let bubbleColors: [UIColor] = [
        UIColor(red: 0.98, green: 0.72, blue: 0.68, alpha: 1),
        UIColor(red: 0.99, green: 0.84, blue: 0.70, alpha: 1),
        UIColor(red: 0.99, green: 0.95, blue: 0.72, alpha: 1),
        UIColor(red: 0.78, green: 0.94, blue: 0.82, alpha: 1),
        UIColor(red: 0.72, green: 0.88, blue: 0.98, alpha: 1),
        UIColor(red: 0.82, green: 0.78, blue: 0.97, alpha: 1),
        UIColor(red: 0.98, green: 0.78, blue: 0.88, alpha: 1),
        UIColor(red: 0.80, green: 0.91, blue: 0.80, alpha: 1),
        UIColor(red: 0.76, green: 0.82, blue: 0.97, alpha: 1),
    ]

    private func setupBackground() {
        let configs: [(radius: CGFloat, x: CGFloat, y: CGFloat, colorIdx: Int, duration: Double)] = [
            (88,  -280,  480, 0, 7.2),
            (62,   260,  350, 4, 9.5),
            (110, -180, -150, 6, 11.0),
            (74,   310, -320, 1, 8.3),
            (96,   -60,  560, 3, 10.1),
            (54,   200, -500, 7, 6.8),
            (80,  -320, -480, 2, 12.4),
            (68,    80,  200, 5, 9.0),
            (50,  -200,  -30, 8, 7.6),
        ]
        for cfg in configs {
            let bubble = SKShapeNode(circleOfRadius: cfg.radius)
            bubble.fillColor = bubbleColors[cfg.colorIdx].withAlphaComponent(0.18)
            bubble.strokeColor = .clear
            bubble.position = CGPoint(x: cfg.x, y: cfg.y)
            bubble.zPosition = -1
            addChild(bubble)
            let up = SKAction.moveBy(x: 0, y: 18, duration: cfg.duration)
            let down = SKAction.moveBy(x: 0, y: -18, duration: cfg.duration)
            up.timingMode = .easeInEaseOut
            down.timingMode = .easeInEaseOut
            bubble.run(SKAction.repeatForever(SKAction.sequence([up, down])))
        }
    }

    private func setupUI() {
        let logoY = gridTop + 95

        let ten = SKLabelNode(text: "TEN")
        ten.fontName = "AvenirNext-Heavy"
        ten.fontSize = 48
        ten.fontColor = UIColor(white: 0.28, alpha: 1)
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
        go.fontColor = UIColor(white: 0.28, alpha: 1)
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

        let ptsLabel = SKLabelNode(text: "pts")
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
    }

    private func setupGrid() {
        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let model = gridModel.cells[row][col] else { continue }
                let node = BubbleNode(value: model.value)
                node.position = scenePos(row: row, col: col)
                addChild(node)
                bubbleNodes[row][col] = node
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
                    run(SKAction.wait(forDuration: 0.12)) { [weak self] in self?.resetGame() }
                    return
                }
                if name == "homePanelBtn" {
                    run(SKAction.wait(forDuration: 0.08)) { [weak self] in self?.goBackToMenu() }
                    return
                }
            }
            return  // bloquer le reste du touch si panel visible
        }

        // Bulle restart — toujours réactive
        let dist = hypot(point.x - restartBubbleNode.position.x, point.y - restartBubbleNode.position.y)
        if dist < 55 {
            restartBubbleNode.run(SKAction.sequence([
                SKAction.scale(to: 0.88, duration: 0.08),
                SKAction.scale(to: 1.0, duration: 0.12)
            ]))
            resetGame()
            return
        }

        guard !isAnimating else { return }
        guard let coord = gridCoord(for: point) else { return }

        currentPath = [coord]
        bubbleNodes[coord.row][coord.col]?.setSelected(true)
        let selectValue = gridModel.cells[coord.row][coord.col]?.value ?? 1
        SoundManager.shared.playSelect(value: selectValue)
        updateSumLabel()
        updatePathLine()
    }

    override func touchesMoved(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard !isAnimating, !currentPath.isEmpty, let touch = touches.first else { return }
        let point = touch.location(in: self)
        guard let coord = gridCoord(for: point) else { return }
        let last = currentPath.last!

        // Same cell — do nothing
        if coord.row == last.row && coord.col == last.col { return }

        // Backtrack one step
        if currentPath.count >= 2 {
            let prev = currentPath[currentPath.count - 2]
            if coord.row == prev.row && coord.col == prev.col {
                bubbleNodes[last.row][last.col]?.setSelected(false)
                currentPath.removeLast()
                SoundManager.shared.playBacktrack()
                updateSumLabel()
                updatePathLine()
                return
            }
        }

        // Already in path
        if currentPath.contains(where: { $0.row == coord.row && $0.col == coord.col }) { return }

        // Must be adjacent to last cell
        guard gridModel.isAdjacent(last, coord) else { return }

        // Sum must not exceed 10
        guard let bubble = gridModel.cells[coord.row][coord.col] else { return }
        guard gridModel.pathSum(currentPath) + bubble.value <= 10 else { return }

        currentPath.append(coord)
        bubbleNodes[coord.row][coord.col]?.setSelected(true)
        tapFeedback.impactOccurred()
        SoundManager.shared.playConnect(value: bubble.value)
        updateSumLabel()
        updatePathLine()
    }

    override func touchesEnded(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard !isAnimating, !currentPath.isEmpty else { return }
        if gridModel.pathSum(currentPath) == 10 {
            successFeedback.impactOccurred()
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

        let line = SKShapeNode(path: cgPath)
        line.strokeColor = UIColor(white: 1.0, alpha: 0.6)
        line.lineWidth = 6
        line.lineCap = .round
        line.lineJoin = .round
        line.zPosition = 5
        addChild(line)
        pathLineNode = line
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

        run(SKAction.sequence([
            SKAction.wait(forDuration: 0.26),
            SKAction.run { [weak self] in self?.afterPop() }
        ]))
    }

    private func afterPop() {
        if gridModel.isGridEmpty() {
            triggerWin()
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
            triggerLose()
        } else {
            isAnimating = false
        }
    }

    // MARK: - Win / Lose

    private func triggerWin() {
        SoundManager.shared.playWin()
        isWinState = true
        let bonus = 1000
        score += bonus
        updateScoreLabel()
        showScorePopup(points: bonus, at: CGPoint(x: 0, y: 50))
        GameState.addScore(score)
        GameState.clear()
        run(SKAction.wait(forDuration: 0.5)) { [weak self] in
            self?.showGameOverPanel()
        }
    }

    private func triggerLose() {
        SoundManager.shared.playLose()
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
                GameState.addScore(self.score)
                GameState.clear()
            },
            SKAction.wait(forDuration: 0.5),
            SKAction.run { [weak self] in self?.showGameOverPanel() }
        ]))
    }

    private func showGameOverPanel() {
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
        let titleText = isWinState ? "Parfait !" : "Fin de partie"
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
        let scoreDisplay = SKLabelNode(text: "0 pts")
        scoreDisplay.fontName = "AvenirNext-Heavy"
        scoreDisplay.fontSize = 64
        scoreDisplay.fontColor = UIColor(white: 0.26, alpha: 1)
        scoreDisplay.verticalAlignmentMode = .center
        scoreDisplay.position = CGPoint(x: 0, y: 80)
        panel.addChild(scoreDisplay)
        animateScoreCountUp(label: scoreDisplay, target: score)

        // Record
        let scores = GameState.highScores()
        let isNewRecord = scores.first == score && score > 0

        if isNewRecord {
            let record = SKLabelNode(text: "★  Nouveau record !")
            record.fontName = "AvenirNext-Bold"
            record.fontSize = 20
            record.fontColor = UIColor(red: 0.92, green: 0.65, blue: 0.20, alpha: 1)
            record.verticalAlignmentMode = .center
            record.position = CGPoint(x: 0, y: 30)
            panel.addChild(record)
        } else if let best = scores.first {
            let bestLabel = SKLabelNode(text: "Meilleur : \(best) pts")
            bestLabel.fontName = "AvenirNext-UltraLight"
            bestLabel.fontSize = 19
            bestLabel.fontColor = UIColor(white: 0.38, alpha: 1)
            bestLabel.verticalAlignmentMode = .center
            bestLabel.position = CGPoint(x: 0, y: 30)
            panel.addChild(bestLabel)
        }

        // Stats
        let statsLine1 = SKLabelNode(text: "Chaîne max · \(longestChain) bulles")
        statsLine1.fontName = "AvenirNext-UltraLight"
        statsLine1.fontSize = 17
        statsLine1.fontColor = UIColor(white: 0.38, alpha: 1)
        statsLine1.verticalAlignmentMode = .center
        statsLine1.position = CGPoint(x: 0, y: -12)
        panel.addChild(statsLine1)

        let statsLine2 = SKLabelNode(text: "\(combosCreated) combinaisons créées")
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

        // Bouton Rejouer
        let replayBtn = SKNode()
        replayBtn.name = "replayBtn"
        replayBtn.position = CGPoint(x: 0, y: -140)
        panel.addChild(replayBtn)

        let replayBg = SKShapeNode(rectOf: CGSize(width: 320, height: 68), cornerRadius: 34)
        replayBg.fillColor = UIColor(red: 0.82, green: 0.95, blue: 0.88, alpha: 1)
        replayBg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
        replayBg.lineWidth = 1
        replayBtn.addChild(replayBg)

        let replayLabel = SKLabelNode(text: "Rejouer")
        replayLabel.fontName = "AvenirNext-Medium"
        replayLabel.fontSize = 24
        replayLabel.fontColor = UIColor(white: 0.26, alpha: 1)
        replayLabel.verticalAlignmentMode = .center
        replayBtn.addChild(replayLabel)

        // Bouton Accueil
        let homeBtn = SKNode()
        homeBtn.name = "homePanelBtn"
        homeBtn.position = CGPoint(x: 0, y: -204)
        panel.addChild(homeBtn)

        let homeBg = SKShapeNode(rectOf: CGSize(width: 200, height: 52), cornerRadius: 26)
        homeBg.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        homeBg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
        homeBg.lineWidth = 1
        homeBtn.addChild(homeBg)

        let homeLabel = SKLabelNode(text: "Accueil")
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
    }

    private func animateScoreCountUp(label: SKLabelNode, target: Int) {
        let steps = 24
        let duration = 0.55
        let stepDuration = duration / Double(steps)
        var actions: [SKAction] = []
        for i in 1...steps {
            let value = Int(Double(target) * Double(i) / Double(steps))
            actions.append(SKAction.run { label.text = "\(value) pts" })
            actions.append(SKAction.wait(forDuration: stepDuration))
        }
        actions.append(SKAction.run { label.text = "\(target) pts" })
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
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
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
                self.gridModel = GridModel()
                self.bubbleNodes = [[BubbleNode?]](
                    repeating: [BubbleNode?](repeating: nil, count: GridModel.cols),
                    count: GridModel.rows
                )
                self.setupGridAnimated()
            }
        ]))
    }

    private func setupGridAnimated() {
        var delay: TimeInterval = 0
        let stagger: TimeInterval = 0.012

        for row in 0..<GridModel.rows {
            for col in 0..<GridModel.cols {
                guard let model = gridModel.cells[row][col] else { continue }
                let node = BubbleNode(value: model.value)
                node.position = scenePos(row: row, col: col)
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

    // MARK: - Navigation

    private func goBackToMenu() {
        // Sauvegarder si la partie n'est pas terminée
        if !gridModel.isGridEmpty() && !isAnimating {
            GameState.save(gridModel: gridModel, score: score)
        }
        let menu = MenuScene(size: size)
        menu.scaleMode = .aspectFill
        view?.presentScene(menu, transition: SKTransition.fade(withDuration: 0.3))
    }
}
