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

    private var theme: Theme { ThemeManager.shared.active }

    private func setupUI() {
        guard let view = view else { return }
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        // safeAreaInsets peut être nul au lancement : plancher à ~47 pt (encoche).
        let safeTop = max(view.safeAreaInsets.top, 47) / scale
        let bottomY = -visibleHalfH + max(view.safeAreaInsets.bottom, 20) / scale
        let contentW = min(usableWidth - 48, 600)

        let levels = PuzzleWorld.levels(inWorld: world)
        let progress = PuzzleProgress.shared
        let completed = progress.completedCount(inWorld: world)
        let stars = progress.totalStars(inWorld: world)

        // En-tête : retour à gauche, nom du monde, étoiles à droite.
        let headerY = visibleHalfH - safeTop - 36
        addBackButton(at: CGPoint(x: -contentW / 2 + 30, y: headerY))

        let starsLabel = SKLabelNode(text: "★ \(stars) / \(levels.count * 3)")
        starsLabel.fontName = "AvenirNext-Bold"
        starsLabel.fontSize = 17
        starsLabel.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
        starsLabel.verticalAlignmentMode = .center
        let pillW = starsLabel.frame.width + 34
        let pill = SKShapeNode(rectOf: CGSize(width: pillW, height: 42), cornerRadius: 21)
        pill.fillColor = UIColor(red: 0.99, green: 0.95, blue: 0.80, alpha: 1)
        pill.strokeColor = .clear
        pill.position = CGPoint(x: contentW / 2 - pillW / 2, y: headerY)
        addChild(pill)
        Relief.raise(pill, depth: 3)
        starsLabel.position = pill.position
        addChild(starsLabel)

        let title = SKLabelNode(text: String(localized: "menu.puzzles"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 34
        title.fontColor = theme.logo
        title.horizontalAlignmentMode = .left
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: -contentW / 2 + 74, y: headerY)
        let titleMaxW = contentW - 74 - pillW - 14
        if title.frame.width > titleMaxW { title.setScale(titleMaxW / title.frame.width) }
        addChild(title)

        // Carte du monde : son nom, l'avancement et la barre.
        let cardH: CGFloat = 104
        let cardY = headerY - 42 - cardH / 2
        addWorldCard(atY: cardY, width: contentW, height: cardH,
                     completed: completed, total: levels.count)

        // Niveau à jouer : le premier déverrouillé pas encore résolu.
        let next = levels.first {
            progress.isUnlocked(world: $0.world, index: $0.index)
                && progress.stars(world: $0.world, index: $0.index) == 0
        }
        let ctaH: CGFloat = 70
        let ctaY = bottomY + 18 + ctaH / 2
        if let next { addPlayButton(for: next, atY: ctaY, width: contentW, height: ctaH) }

        // Grille de pastilles rondes : 4 colonnes, dimensionnées sur la largeur
        // utile, et tassées si l'écran est trop court (iPad).
        let perRow = 4
        let rows = Int((Double(levels.count) / Double(perRow)).rounded(.up))
        let gap: CGFloat = 18
        let gridTop = cardY - cardH / 2 - 28
        let gridBottom = (next == nil ? bottomY : ctaY + ctaH / 2) + 22
        let byWidth = (contentW - gap * CGFloat(perRow - 1)) / CGFloat(perRow)
        let byHeight = (gridTop - gridBottom - gap * CGFloat(max(0, rows - 1))) / CGFloat(max(1, rows))
        let cell = min(byWidth, byHeight)
        let gridW = cell * CGFloat(perRow) + gap * CGFloat(perRow - 1)

        for (offset, level) in levels.enumerated() {
            let column = offset % perRow
            let row = offset / perRow
            let x = -gridW / 2 + cell / 2 + CGFloat(column) * (cell + gap)
            let y = gridTop - cell / 2 - CGFloat(row) * (cell + gap)
            addLevelTile(level, at: CGPoint(x: x, y: y), side: cell,
                         isNext: level.index == next?.index, colorIndex: offset)
        }
    }

    private func addWorldCard(atY y: CGFloat, width: CGFloat, height: CGFloat, completed: Int, total: Int) {
        let tint = theme.color(forValue: 9)
        let ink = tint.readableInk()

        let card = SKNode()
        card.position = CGPoint(x: 0, y: y)
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: 28)
        bg.fillColor = tint
        bg.strokeColor = .clear
        card.addChild(bg)
        Relief.raise(bg, depth: 6)

        let count = SKLabelNode(text: "\(completed) / \(total)")
        count.fontName = "AvenirNext-Bold"
        count.fontSize = 18
        count.fontColor = ink
        count.horizontalAlignmentMode = .right
        count.verticalAlignmentMode = .center
        count.position = CGPoint(x: width / 2 - 24, y: 18)
        card.addChild(count)

        let name = SKLabelNode(text: String(localized: String.LocalizationValue(PuzzleWorld.nameKey(forWorld: world))))
        name.fontName = "AvenirNext-Bold"
        name.fontSize = 21
        name.fontColor = ink
        name.horizontalAlignmentMode = .left
        name.verticalAlignmentMode = .center
        name.position = CGPoint(x: -width / 2 + 24, y: 18)
        let nameMaxW = width - 48 - count.frame.width - 14
        if name.frame.width > nameMaxW { name.setScale(nameMaxW / name.frame.width) }
        card.addChild(name)

        let barW = width - 48, barH: CGFloat = 12
        let track = SKShapeNode(rectOf: CGSize(width: barW, height: barH), cornerRadius: barH / 2)
        track.fillColor = UIColor(white: 1, alpha: 0.55)
        track.strokeColor = .clear
        track.position = CGPoint(x: 0, y: -20)
        card.addChild(track)

        let ratio = total > 0 ? CGFloat(completed) / CGFloat(total) : 0
        if ratio > 0 {
            let fillW = max(barH, barW * min(1, ratio))
            let fill = SKShapeNode(rectOf: CGSize(width: fillW, height: barH), cornerRadius: barH / 2)
            fill.fillColor = tint.ledgeShade.ledgeShade
            fill.strokeColor = .clear
            fill.position = CGPoint(x: -barW / 2 + fillW / 2, y: -20)
            card.addChild(fill)
        }
    }

    /// Action principale : enchaîner sur le prochain niveau sans le chercher
    /// dans la grille.
    private func addPlayButton(for level: PuzzleLevel, atY y: CGFloat, width: CGFloat, height: CGFloat) {
        let tint = theme.color(forValue: 4)
        let ink = tint.readableInk()

        let button = SKNode()
        button.name = "level_\(level.world)_\(level.index)"
        button.position = CGPoint(x: 0, y: y)
        addChild(button)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        bg.fillColor = tint
        bg.strokeColor = .clear
        bg.name = button.name
        button.addChild(bg)
        Relief.raise(bg, depth: 6)

        let label = SKLabelNode(text: String(format: String(localized: "puzzle.level_label"), level.index))
        label.fontName = "AvenirNext-Bold"
        label.fontSize = 24
        label.fontColor = ink
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        let iconSize: CGFloat = 30, inner: CGFloat = 12
        let contentW = iconSize + inner + label.frame.width
        let icon = VectorIcon.play.node(size: iconSize, color: ink)
        icon.position = CGPoint(x: -contentW / 2 + iconSize / 2, y: 0)
        label.position = CGPoint(x: -contentW / 2 + iconSize + inner, y: 0)
        button.addChild(icon)
        button.addChild(label)
    }

    private func addLevelTile(_ level: PuzzleLevel, at position: CGPoint, side: CGFloat,
                              isNext: Bool, colorIndex: Int) {
        let unlocked = PuzzleProgress.shared.isUnlocked(world: level.world, index: level.index)
        let stars = PuzzleProgress.shared.stars(world: level.world, index: level.index)
        let radius = side / 2

        let tile = SKNode()
        tile.name = unlocked ? "level_\(level.world)_\(level.index)" : nil
        tile.position = position
        addChild(tile)

        // Niveau à jouer : un anneau accent le désigne dans la grille.
        if isNext {
            let ring = SKShapeNode(circleOfRadius: radius + 7)
            ring.fillColor = theme.color(forValue: 4)
            ring.strokeColor = .clear
            tile.addChild(ring)
        }

        let tint = theme.color(forValue: [7, 3, 4, 6, 2, 9, 1, 5, 8][colorIndex % 9])
        let bg = SKShapeNode(circleOfRadius: radius)
        bg.strokeColor = .clear
        if !unlocked {
            bg.fillColor = theme.logo.withAlphaComponent(0.08)
        } else if stars > 0 {
            bg.fillColor = tint
        } else {
            bg.fillColor = Relief.surface
        }
        tile.addChild(bg)
        // Seuls les niveaux jouables prennent du relief : un niveau verrouillé
        // ne doit pas avoir l'air d'un bouton.
        if unlocked && !isNext { Relief.raise(bg, depth: 4) }

        if unlocked {
            let ink = stars > 0 ? tint.readableInk() : theme.logo
            let number = SKLabelNode(text: "\(level.index)")
            number.fontName = "AvenirNext-Heavy"
            number.fontSize = side * 0.34
            number.fontColor = ink
            number.verticalAlignmentMode = .center
            number.position = CGPoint(x: 0, y: side * 0.09)
            tile.addChild(number)

            let starsLabel = SKLabelNode(text: String(repeating: "★", count: stars)
                                         + String(repeating: "☆", count: 3 - stars))
            starsLabel.fontName = "AvenirNext-Bold"
            starsLabel.fontSize = side * 0.15
            starsLabel.fontColor = ink.withAlphaComponent(stars > 0 ? 0.9 : 0.5)
            starsLabel.verticalAlignmentMode = .center
            starsLabel.position = CGPoint(x: 0, y: -side * 0.21)
            tile.addChild(starsLabel)
        } else {
            // Icône système monochrome (pas d'emoji dans le jeu).
            let config = UIImage.SymbolConfiguration(pointSize: 40, weight: .semibold)
            if let img = UIImage(systemName: "lock.fill", withConfiguration: config)?
                .withTintColor(theme.logo.withAlphaComponent(0.35), renderingMode: .alwaysOriginal) {
                let lock = SKSpriteNode(texture: SKTexture(image: img))
                let target = side * 0.26
                let maxDim = max(img.size.width, img.size.height)
                lock.size = CGSize(width: img.size.width / maxDim * target,
                                   height: img.size.height / maxDim * target)
                lock.position = .zero
                tile.addChild(lock)
            }
        }
    }

    private func addBackButton(at position: CGPoint) {
        let back = SKNode()
        back.name = "back"
        back.position = position
        addChild(back)

        let circle = SKShapeNode(circleOfRadius: 30)
        circle.fillColor = Relief.surface
        circle.strokeColor = .clear
        circle.name = "back"
        back.addChild(circle)
        Relief.raise(circle, depth: 4)

        let icon = SKLabelNode(text: "‹")
        icon.fontName = "AvenirNext-Medium"
        icon.fontSize = 34
        icon.fontColor = theme.logo
        icon.verticalAlignmentMode = .center
        icon.horizontalAlignmentMode = .center
        icon.position = CGPoint(x: -2, y: 3)
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
