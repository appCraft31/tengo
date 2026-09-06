//
//  AchievementsScene.swift
//  tenGO
//
//  Liste des succès, filtrable par catégorie, avec % de complétion global.
//

import SpriteKit

class AchievementsScene: SKScene {

    private let category: AchievementCategory

    init(size: CGSize, category: AchievementCategory = .chain) {
        self.category = category
        super.init(size: size)
    }

    required init?(coder aDecoder: NSCoder) {
        self.category = .chain
        super.init(coder: aDecoder)
    }

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
        setupUI()
    }

    // MARK: - UI

    /// Largeur réellement visible en coordonnées scène (la scène fait 750×1334
    /// en aspectFill : les bords sont rognés, une largeur en dur gaspille
    /// la moitié de l'écran).
    private var cardWidth: CGFloat = 340

    private func setupUI() {
        guard let view = view else { return }
        let topY = size.height / 2
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        let safeBottomInset = view.safeAreaInsets.bottom / scale
        let bottomY = -visibleHalfH + safeBottomInset
        cardWidth = min(usableWidth - 48, 600)

        let title = SKLabelNode(text: String(localized: "achievements.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        let completion = SKLabelNode(text: String(format: String(localized: "achievements.completion"),
                                                    AchievementManager.unlockedCount, AchievementManager.totalCount))
        completion.fontName = "AvenirNext-Medium"
        completion.fontSize = 15
        completion.fontColor = UIColor(white: 0.42, alpha: 1)
        completion.verticalAlignmentMode = .center
        completion.position = CGPoint(x: 0, y: topY - 142)
        addChild(completion)

        addCategoryTabs(atY: topY - 190)

        // La liste doit tenir SANS être tronquée : la catégorie Score compte 9
        // succès, un plafond fixe en masquait 3 que le joueur ne pouvait donc
        // jamais voir. On ajuste la hauteur de ligne à la place disponible.
        let items = AchievementManager.achievements(category: category)
        let listTop = topY - 250
        let listBottom = bottomY + 150
        let available = max(0, listTop - listBottom)
        let gap: CGFloat = 12
        let count = CGFloat(max(1, items.count))
        let rowH = max(56, min(130, available / count - gap))
        let maxVisible = max(1, Int(available / (rowH + gap)))

        // Bloc centré verticalement : une catégorie de 5 succès ne doit pas
        // laisser la moitié basse de l'écran vide.
        let shown = min(items.count, maxVisible)
        let blockH = rowH * CGFloat(shown) + gap * CGFloat(max(0, shown - 1))
        var cursorY = listTop - max(0, (available - blockH) / 2) - rowH / 2

        for item in items.prefix(maxVisible) {
            addAchievementRow(item, atY: cursorY, height: rowH)
            cursorY -= (rowH + gap)
        }

        addBackButton(atY: bottomY + 90)
    }

    private static let tabOrder: [AchievementCategory] = [.chain, .perfect, .score, .daily]

    private func addCategoryTabs(atY y: CGFloat) {
        // Les onglets occupent la largeur de la liste : des pastilles de 80 pt
        // au milieu d'un écran de 600 étaient minuscules et peu tapables.
        let gap: CGFloat = 10
        let count = CGFloat(Self.tabOrder.count)
        let tabW = (cardWidth - gap * (count - 1)) / count
        let totalW = cardWidth
        var x = -totalW / 2 + tabW / 2

        for cat in Self.tabOrder {
            let selected = cat == category
            let tab = SKNode()
            tab.name = "category_\(cat.rawValue)"
            tab.position = CGPoint(x: x, y: y)
            addChild(tab)

            let bg = SKShapeNode(rectOf: CGSize(width: tabW, height: 46), cornerRadius: 23)
            bg.fillColor = selected ? ThemeManager.shared.active.accent : UIColor(white: 0.9, alpha: 0.7)
            bg.strokeColor = UIColor(white: 0.68, alpha: 0.3)
            bg.lineWidth = 1
            tab.addChild(bg)

            let label = SKLabelNode(text: String(localized: String.LocalizationValue(cat.titleKey)))
            label.fontName = selected ? "AvenirNext-Bold" : "AvenirNext-Medium"
            label.fontSize = 15
            label.fontColor = selected ? UIColor(white: 0.15, alpha: 1) : UIColor(white: 0.4, alpha: 1)
            label.verticalAlignmentMode = .center
            label.horizontalAlignmentMode = .center
            tab.addChild(label)

            x += tabW + gap
        }
    }

    private func addAchievementRow(_ item: AchievementManager.DisplayAchievement, atY y: CGFloat, height: CGFloat) {
        let def = item.definition
        let width = cardWidth

        let row = SKNode()
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: 20)
        bg.fillColor = item.isUnlocked
            ? UIColor(red: 0.85, green: 0.95, blue: 0.87, alpha: 0.95)
            : UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 0.9)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.3)
        bg.lineWidth = 1
        row.addChild(bg)

        // Icône catégorie — se resserre avec la ligne sur les petits écrans.
        let iconR = min(26, height * 0.28)
        let iconBg = SKShapeNode(circleOfRadius: iconR)
        iconBg.fillColor = ThemeManager.shared.active.accent.withAlphaComponent(item.isUnlocked ? 0.9 : 0.35)
        iconBg.strokeColor = .clear
        iconBg.position = CGPoint(x: -width / 2 + 22 + iconR, y: 0)
        row.addChild(iconBg)

        let config = UIImage.SymbolConfiguration(pointSize: 20, weight: .medium)
        if let img = UIImage(systemName: def.category.icon, withConfiguration: config)?
            .withTintColor(.white, renderingMode: .alwaysOriginal) {
            let sprite = SKSpriteNode(texture: SKTexture(image: img))
            let maxDim = max(img.size.width, img.size.height)
            let target = iconR * 1.25
            sprite.size = CGSize(width: img.size.width / maxDim * target, height: img.size.height / maxDim * target)
            sprite.position = iconBg.position
            row.addChild(sprite)
        }

        // La progression partage la ligne du TITRE (court), pas celle de la
        // description : sinon les descriptions longues passent sous le
        // « 0 / 10000 » de droite. SKLabelNode ne tronque pas de façon fiable
        // via preferredMaxLayoutWidth, on évite donc le recouvrement par la
        // mise en page plutôt que par la troncature.
        let textLeft = -width / 2 + 44 + iconR * 2

        let titleLabel = SKLabelNode(text: AchievementManager.title(for: def))
        titleLabel.fontName = "AvenirNext-DemiBold"
        titleLabel.fontSize = 19
        titleLabel.fontColor = UIColor(white: 0.24, alpha: 1)
        titleLabel.horizontalAlignmentMode = .left
        titleLabel.verticalAlignmentMode = .center
        titleLabel.position = CGPoint(x: textLeft, y: height * 0.18)
        row.addChild(titleLabel)

        let descLabel = SKLabelNode(text: AchievementManager.description(for: def))
        descLabel.fontName = "AvenirNext-UltraLight"
        descLabel.fontSize = 14
        descLabel.fontColor = UIColor(white: 0.42, alpha: 1)
        descLabel.horizontalAlignmentMode = .left
        descLabel.verticalAlignmentMode = .center
        descLabel.position = CGPoint(x: textLeft, y: -height * 0.17)
        row.addChild(descLabel)

        let statusText = item.isUnlocked
            ? String(localized: "achievements.unlocked")
            : "\(item.currentValue) / \(def.target)"
        let statusLabel = SKLabelNode(text: statusText)
        statusLabel.fontName = "AvenirNext-Bold"
        statusLabel.fontSize = 15
        statusLabel.fontColor = item.isUnlocked
            ? UIColor(red: 0.32, green: 0.58, blue: 0.38, alpha: 1)
            : UIColor(white: 0.45, alpha: 1)
        statusLabel.horizontalAlignmentMode = .right
        statusLabel.verticalAlignmentMode = .center
        statusLabel.position = CGPoint(x: width / 2 - 16, y: height * 0.18)
        row.addChild(statusLabel)
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
            if name.hasPrefix("category_"), let raw = name.split(separator: "_").last,
               let newCategory = AchievementCategory(rawValue: String(raw)), newCategory != category {
                let scene = AchievementsScene(size: size, category: newCategory)
                scene.scaleMode = .aspectFill
                view?.presentScene(scene, transition: SceneTransition.fade(0.18))
                return
            }
        }
    }
}
