//
//  PuzzleLevelsScene.swift
//  tenGO
//
//  Sélection des niveaux d'un monde : grille de pastilles, étoiles obtenues,
//  progression du monde. Un niveau se déverrouille dès que le précédent a été
//  résolu ; un niveau déjà résolu reste rejouable pour améliorer ses étoiles.
//

import SpriteKit

class PuzzleLevelsScene: SKScene {

    private let world: Int

    init(size: CGSize, world: Int) {
        self.world = world
        super.init(size: size)
    }

    required init?(coder aDecoder: NSCoder) {
        self.world = 1
        super.init(coder: aDecoder)
    }

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
        setupUI()
    }

    // MARK: - UI

    private func setupUI() {
        guard let view = view else { return }
        let topY = size.height / 2
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        let safeBottomInset = view.safeAreaInsets.bottom / scale
        let bottomY = -visibleHalfH + safeBottomInset
        let contentW = min(usableWidth - 48, 600)

        let title = SKLabelNode(text: String(localized: String.LocalizationValue(PuzzleWorld.nameKey(forWorld: world))))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 34
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        let levels = PuzzleWorld.levels(inWorld: world)
        let progress = PuzzleProgress.shared
        let subtitle = SKLabelNode(text: String(format: String(localized: "puzzle.world_progress"),
                                                progress.completedCount(inWorld: world),
                                                levels.count,
                                                progress.totalStars(inWorld: world),
                                                levels.count * 3))
        subtitle.fontName = "AvenirNext-Medium"
        subtitle.fontSize = 16
        subtitle.fontColor = UIColor(white: 0.42, alpha: 1)
        subtitle.verticalAlignmentMode = .center
        subtitle.position = CGPoint(x: 0, y: topY - 146)
        addChild(subtitle)

        // Grille de pastilles : 4 colonnes, dimensionnées sur la largeur utile.
        let perRow = 4
        let gap: CGFloat = 16
        let cell = (contentW - gap * CGFloat(perRow - 1)) / CGFloat(perRow)
        let listTop = topY - 210
        var cursorY = listTop - cell / 2

        for (offset, level) in levels.enumerated() {
            let column = offset % perRow
            let x = -contentW / 2 + cell / 2 + CGFloat(column) * (cell + gap)
            addLevelTile(level, at: CGPoint(x: x, y: cursorY), side: cell)
            if column == perRow - 1 { cursorY -= (cell + gap) }
        }

        addBackButton(atY: bottomY + 80)
    }

    private func addLevelTile(_ level: PuzzleLevel, at position: CGPoint, side: CGFloat) {
        let unlocked = PuzzleProgress.shared.isUnlocked(world: level.world, index: level.index)
        let stars = PuzzleProgress.shared.stars(world: level.world, index: level.index)

        let tile = SKNode()
        tile.name = unlocked ? "level_\(level.world)_\(level.index)" : nil
        tile.position = position
        addChild(tile)

        let bg = SKShapeNode(rectOf: CGSize(width: side, height: side), cornerRadius: 20)
        bg.fillColor = unlocked
            ? (stars > 0
               ? UIColor(red: 0.85, green: 0.95, blue: 0.87, alpha: 0.95)
               : UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 0.95))
            : UIColor(white: 0.88, alpha: 0.55)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.3)
        bg.lineWidth = 1
        tile.addChild(bg)

        if unlocked {
            let number = SKLabelNode(text: "\(level.index)")
            number.fontName = "AvenirNext-Heavy"
            number.fontSize = side * 0.34
            number.fontColor = UIColor(white: 0.26, alpha: 1)
            number.verticalAlignmentMode = .center
            number.position = CGPoint(x: 0, y: side * 0.10)
            tile.addChild(number)

            let starsLabel = SKLabelNode(text: String(repeating: "★", count: stars)
                                         + String(repeating: "☆", count: 3 - stars))
            starsLabel.fontName = "AvenirNext-Bold"
            starsLabel.fontSize = side * 0.17
            starsLabel.fontColor = stars > 0
                ? UIColor(red: 0.95, green: 0.72, blue: 0.20, alpha: 1)
                : UIColor(white: 0.62, alpha: 1)
            starsLabel.verticalAlignmentMode = .center
            starsLabel.position = CGPoint(x: 0, y: -side * 0.22)
            tile.addChild(starsLabel)
        } else {
            let lock = SKLabelNode(text: "🔒")
            lock.fontSize = side * 0.30
            lock.verticalAlignmentMode = .center
            lock.position = .zero
            tile.addChild(lock)
        }
    }

    private func addBackButton(atY y: CGFloat) {
        let back = SKNode()
        back.name = "back"
        back.position = CGPoint(x: 0, y: y)
        addChild(back)

        let circle = SKShapeNode(circleOfRadius: 36)
        circle.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        circle.strokeColor = UIColor(white: 0.68, alpha: 0.45)
        circle.lineWidth = 1.5
        back.addChild(circle)

        let icon = SKLabelNode(text: "‹")
        icon.fontName = "AvenirNext-Medium"
        icon.fontSize = 32
        icon.fontColor = UIColor(white: 0.45, alpha: 1)
        icon.verticalAlignmentMode = .center
        icon.horizontalAlignmentMode = .center
        back.addChild(icon)
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)
        for node in nodes(at: point) {
            guard let name = node.parent?.name ?? node.name else { continue }
            if name == "back" {
                let menu = MenuScene(size: size)
                menu.scaleMode = .aspectFill
                view?.presentScene(menu, transition: SceneTransition.fade(0.28))
                return
            }
            if name.hasPrefix("level_") {
                let parts = name.split(separator: "_")
                guard parts.count == 3, let w = Int(parts[1]), let index = Int(parts[2]),
                      let level = PuzzleWorld.level(world: w, index: index) else { return }
                let scene = GameScene(size: size, puzzle: level)
                scene.scaleMode = .aspectFill
                view?.presentScene(scene, transition: SceneTransition.fade(0.3))
                return
            }
        }
    }
}
