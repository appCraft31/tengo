//
//  BoutiqueScene.swift
//  tenGO
//
//  Boutique à onglets : Thèmes, Matières de bulles, Styles de tracé.
//  Achat avec les pièces gagnées, sélection du cosmétique actif. Tout est non bloquant.
//

import SpriteKit
import StoreKit

class BoutiqueScene: SKScene {

    private enum Tab: String { case themes, bubbles, trails, boosters, coins }
    private var tab: Tab = .themes
    private var attemptedProductLoad = false

    private let cardH: CGFloat = 150
    private var usableWidth: CGFloat = 600
    private var cardW: CGFloat = 280

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scaleMode = .aspectFill
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        usableWidth = view.bounds.width / scale
        cardW = usableWidth / 2 - 16
        rebuild()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": true])
    }

    // MARK: - Layout

    private func rebuild() {
        removeAllChildren()
        let theme = ThemeManager.shared.active
        backgroundColor = theme.background

        let topY = size.height * 0.42

        let title = SKLabelNode(text: String(localized: "shop.title", defaultValue: "Boutique"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 38
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY)
        addChild(title)

        addBalanceChip(atY: topY - 56, theme: theme)
        addTabBar(atY: topY - 116, theme: theme)

        let startY = topY - 232
        let colCenter = usableWidth / 4
        let colX: [CGFloat] = [-colCenter, colCenter]

        func place(_ index: Int) -> CGPoint {
            CGPoint(x: colX[index % 2], y: startY - CGFloat(index / 2) * (cardH + 18))
        }

        switch tab {
        case .themes:
            for (i, t) in ThemeManager.shared.themes.enumerated() {
                addThemeCard(t, at: place(i))
            }
        case .bubbles:
            for (i, s) in CosmeticManager.shared.bubbleStyles.enumerated() {
                let preview = BubbleNode.makeVisual(value: 5, styleKind: s.kind, radius: 23)
                addCosmeticCard(kind: .bubbleStyle, id: s.id, price: s.price,
                                title: bubbleStyleName(s.id), preview: preview, previewY: 36, at: place(i))
            }
        case .trails:
            for (i, t) in CosmeticManager.shared.trails.enumerated() {
                addCosmeticCard(kind: .trail, id: t.id, price: t.price,
                                title: trailStyleName(t.id), preview: trailPreview(t), previewY: 36, at: place(i))
            }
        case .boosters:
            for (i, booster) in Booster.allCases.enumerated() {
                addBoosterCard(booster, at: place(i))
            }
        case .coins:
            let products = StoreManager.shared.products
            if products.isEmpty {
                let msg = attemptedProductLoad
                    ? String(localized: "shop.coins_unavailable", defaultValue: "Indisponible pour le moment")
                    : String(localized: "shop.coins_loading", defaultValue: "Chargement…")
                addInfoLabel(msg, atY: startY - 10, theme: theme)
                if !attemptedProductLoad {
                    attemptedProductLoad = true
                    Task { @MainActor in
                        await StoreManager.shared.loadProducts()
                        if self.tab == .coins { self.rebuild() }
                    }
                }
            } else {
                for (i, p) in products.enumerated() {
                    addCoinPackCard(p, at: place(i))
                }
            }
        }

        addBackButton(atY: -size.height * 0.43, theme: theme)
    }

    private func addTabBar(atY y: CGFloat, theme: Theme) {
        let items: [(Tab, String)] = [
            (.themes, String(localized: "shop.tab_themes", defaultValue: "Thèmes")),
            (.bubbles, String(localized: "shop.tab_bubbles", defaultValue: "Matières")),
            (.trails, String(localized: "shop.tab_trails", defaultValue: "Tracés")),
            (.boosters, String(localized: "shop.tab_boosters", defaultValue: "Boosters")),
            (.coins, String(localized: "shop.tab_coins", defaultValue: "Pièces")),
        ]
        let tabW = (usableWidth - 16) / CGFloat(items.count)
        for (i, item) in items.enumerated() {
            let isOn = tab == item.0
            let x = -usableWidth / 2 + 8 + tabW * (CGFloat(i) + 0.5)
            let node = SKNode()
            node.name = "tab:\(item.0.rawValue)"
            node.position = CGPoint(x: x, y: y)

            let bg = SKShapeNode(rectOf: CGSize(width: tabW - 6, height: 40), cornerRadius: 20)
            bg.fillColor = isOn ? theme.accent : theme.logo.withAlphaComponent(0.08)
            bg.strokeColor = isOn ? .clear : theme.logo.withAlphaComponent(0.18)
            bg.lineWidth = 1
            node.addChild(bg)

            let label = SKLabelNode(text: item.1)
            label.fontName = isOn ? "AvenirNext-Bold" : "AvenirNext-Medium"
            label.fontSize = 14
            label.fontColor = isOn ? contrastingText(on: theme.accent) : theme.logo.withAlphaComponent(0.8)
            label.verticalAlignmentMode = .center
            node.addChild(label)
            addChild(node)
        }
    }

    private func addInfoLabel(_ text: String, atY y: CGFloat, theme: Theme) {
        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 19
        label.fontColor = theme.logo.withAlphaComponent(0.7)
        label.verticalAlignmentMode = .center
        label.position = CGPoint(x: 0, y: y)
        addChild(label)
    }

    /// Carte d'un pack de pièces (achat en vraie monnaie via StoreKit).
    private func addCoinPackCard(_ product: Product, at position: CGPoint) {
        let theme = ThemeManager.shared.active
        let amount = StoreManager.shared.coins(for: product)

        let preview = SKNode()
        let coin = CoinIcon.make(radius: 24)
        preview.addChild(coin)

        addCardFrame(name: "pack:\(product.id)", background: theme.background,
                     border: theme.logo.withAlphaComponent(0.18), borderWidth: 1,
                     title: "\(amount)", titleColor: theme.logo,
                     preview: preview, previewY: 40, at: position)

        if let card = childNode(withName: "pack:\(product.id)") {
            // Pilule accent = prix en euros (bouton d'achat)
            let pill = SKShapeNode(rectOf: CGSize(width: 110, height: 34), cornerRadius: 17)
            pill.fillColor = theme.accent
            pill.strokeColor = .clear
            pill.position = CGPoint(x: 0, y: -48)
            card.addChild(pill)
            let label = SKLabelNode(text: product.displayPrice)
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 16
            label.fontColor = contrastingText(on: theme.accent)
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: -48)
            card.addChild(label)
        }
    }

    /// Carte d'un lot de booster (achat en pièces).
    private func addBoosterCard(_ booster: Booster, at position: CGPoint) {
        let theme = ThemeManager.shared.active
        let owned = BoosterManager.shared.count(booster)

        let preview = SKNode()
        let icon = BoosterIcon.make(booster, size: 46, color: theme.logo)
        preview.addChild(icon)

        addCardFrame(name: "booster:\(booster.rawValue)", background: theme.background,
                     border: theme.logo.withAlphaComponent(0.18), borderWidth: 1,
                     title: "\(boosterName(booster)) ×\(booster.bundleQuantity)", titleColor: theme.logo,
                     preview: preview, previewY: 40, at: position)

        guard let card = childNode(withName: "booster:\(booster.rawValue)") else { return }

        // Stock possédé.
        let stock = SKLabelNode(text: String(format: String(localized: "shop.booster_stock",
                                                             defaultValue: "en stock : %d"), owned))
        stock.fontName = "AvenirNext-Medium"
        stock.fontSize = 13
        stock.fontColor = theme.logo.withAlphaComponent(0.6)
        stock.verticalAlignmentMode = .center
        stock.position = CGPoint(x: 0, y: 20)
        card.addChild(stock)

        // Pilule prix (pièces).
        let priceLabel = SKLabelNode(text: "\(booster.bundlePrice)")
        priceLabel.fontName = "AvenirNext-Bold"
        priceLabel.fontSize = 17
        priceLabel.fontColor = theme.logo
        priceLabel.verticalAlignmentMode = .center
        priceLabel.horizontalAlignmentMode = .left
        let coin = CoinIcon.make(radius: 9)
        let gap: CGFloat = 6
        let contentW = 18 + gap + priceLabel.frame.width
        let pill = SKShapeNode(rectOf: CGSize(width: contentW + 28, height: 32), cornerRadius: 16)
        pill.fillColor = theme.logo.withAlphaComponent(0.06)
        pill.strokeColor = theme.logo.withAlphaComponent(0.18)
        pill.lineWidth = 1
        pill.position = CGPoint(x: 0, y: -48)
        card.addChild(pill)
        let startX = -contentW / 2
        coin.position = CGPoint(x: startX + 9, y: -48)
        priceLabel.position = CGPoint(x: startX + 18 + gap, y: -48)
        card.addChild(coin)
        card.addChild(priceLabel)
    }

    // MARK: - Cartes

    private func addThemeCard(_ theme: Theme, at position: CGPoint) {
        let isActive = ThemeManager.shared.activeID == theme.id
        let owned = ThemeManager.shared.owns(theme.id)

        // Aperçu : 5 bulles de la palette
        let preview = SKNode()
        let previewValues = [1, 3, 5, 7, 9]
        let spacing = min(42, (cardW - 56) / CGFloat(previewValues.count - 1))
        let totalW = CGFloat(previewValues.count - 1) * spacing
        for (i, v) in previewValues.enumerated() {
            let dot = SKShapeNode(circleOfRadius: 16)
            dot.fillColor = theme.color(forValue: v)
            dot.strokeColor = UIColor(white: 1, alpha: 0.22)
            dot.lineWidth = 1
            dot.position = CGPoint(x: -totalW / 2 + CGFloat(i) * spacing, y: 0)
            preview.addChild(dot)
        }

        addCardFrame(name: "item:theme:\(theme.id)", background: theme.background,
                     border: isActive ? theme.accent : theme.logo.withAlphaComponent(0.18),
                     borderWidth: isActive ? 3 : 1,
                     title: themeName(theme.id), titleColor: theme.logo,
                     preview: preview, previewY: 42, at: position)
        // Badge
        if let card = childNode(withName: "item:theme:\(theme.id)") {
            addStatusBadge(to: card, theme: theme, isActive: isActive, owned: owned, atY: -48,
                           priceOverride: owned ? nil : theme.price)
        }
    }

    private func addCosmeticCard(kind: CosmeticKind, id: String, price: Int, title: String,
                                 preview: SKNode, previewY: CGFloat, at position: CGPoint) {
        let theme = ThemeManager.shared.active
        let isActive = CosmeticManager.shared.activeID(kind) == id
        let owned = CosmeticManager.shared.owns(kind, id)
        let prefix = kind == .bubbleStyle ? "bubbleStyle" : "trail"

        addCardFrame(name: "item:\(prefix):\(id)", background: theme.background,
                     border: isActive ? theme.accent : theme.logo.withAlphaComponent(0.18),
                     borderWidth: isActive ? 3 : 1,
                     title: title, titleColor: theme.logo,
                     preview: preview, previewY: previewY, at: position)
        if let card = childNode(withName: "item:\(prefix):\(id)") {
            addStatusBadge(to: card, theme: theme, isActive: isActive, owned: owned, atY: -48,
                           priceOverride: owned ? nil : price)
        }
    }

    /// Cadre commun de carte (ombre + fond + titre + aperçu).
    private func addCardFrame(name: String, background: UIColor, border: UIColor, borderWidth: CGFloat,
                              title: String, titleColor: UIColor, preview: SKNode, previewY: CGFloat, at position: CGPoint) {
        let card = SKNode()
        card.name = name
        card.position = position
        let cardSize = CGSize(width: cardW, height: cardH)

        let shadow = SKShapeNode(rectOf: cardSize, cornerRadius: 26)
        shadow.fillColor = UIColor(white: 0, alpha: 0.08)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: -5)
        shadow.zPosition = -1
        card.addChild(shadow)

        let bg = SKShapeNode(rectOf: cardSize, cornerRadius: 26)
        bg.fillColor = background
        bg.strokeColor = border
        bg.lineWidth = borderWidth
        card.addChild(bg)

        preview.position = CGPoint(x: 0, y: previewY)
        card.addChild(preview)

        let titleLabel = SKLabelNode(text: title)
        titleLabel.fontName = "AvenirNext-DemiBold"
        titleLabel.fontSize = 22
        titleLabel.fontColor = titleColor
        titleLabel.verticalAlignmentMode = .center
        titleLabel.position = CGPoint(x: 0, y: -6)
        card.addChild(titleLabel)

        addChild(card)
    }

    private func addStatusBadge(to card: SKNode, theme: Theme, isActive: Bool, owned: Bool, atY y: CGFloat, priceOverride: Int? = nil) {
        if isActive {
            let pill = SKShapeNode(rectOf: CGSize(width: 96, height: 32), cornerRadius: 16)
            pill.fillColor = theme.accent
            pill.strokeColor = .clear
            pill.position = CGPoint(x: 0, y: y)
            card.addChild(pill)
            let label = SKLabelNode(text: String(localized: "shop.active", defaultValue: "Actif"))
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 15
            label.fontColor = contrastingText(on: theme.accent)
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: y)
            card.addChild(label)
        } else if owned {
            let pill = SKShapeNode(rectOf: CGSize(width: 108, height: 32), cornerRadius: 16)
            pill.fillColor = .clear
            pill.strokeColor = theme.logo.withAlphaComponent(0.5)
            pill.lineWidth = 1.5
            pill.position = CGPoint(x: 0, y: y)
            card.addChild(pill)
            let label = SKLabelNode(text: String(localized: "shop.select", defaultValue: "Choisir"))
            label.fontName = "AvenirNext-Medium"
            label.fontSize = 15
            label.fontColor = theme.logo
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: y)
            card.addChild(label)
        } else {
            let price = priceOverride ?? 0
            let priceLabel = SKLabelNode(text: "\(price)")
            priceLabel.fontName = "AvenirNext-Bold"
            priceLabel.fontSize = 17
            priceLabel.fontColor = theme.logo
            priceLabel.verticalAlignmentMode = .center
            priceLabel.horizontalAlignmentMode = .left
            let coin = CoinIcon.make(radius: 9)
            let gap: CGFloat = 6
            let contentW = 18 + gap + priceLabel.frame.width
            let pill = SKShapeNode(rectOf: CGSize(width: contentW + 28, height: 32), cornerRadius: 16)
            pill.fillColor = theme.logo.withAlphaComponent(0.06)
            pill.strokeColor = theme.logo.withAlphaComponent(0.18)
            pill.lineWidth = 1
            pill.position = CGPoint(x: 0, y: y)
            card.addChild(pill)
            let startX = -contentW / 2
            coin.position = CGPoint(x: startX + 9, y: y)
            priceLabel.position = CGPoint(x: startX + 18 + gap, y: y)
            card.addChild(coin)
            card.addChild(priceLabel)
        }
    }

    private func addBalanceChip(atY y: CGFloat, theme: Theme) {
        let container = SKNode()
        container.position = CGPoint(x: 0, y: y)
        let coin = CoinIcon.make(radius: 12)
        coin.zPosition = 1
        let label = SKLabelNode(text: "\(CoinManager.shared.balance)")
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 24
        label.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        label.zPosition = 1
        let gap: CGFloat = 9
        let contentW = 24 + gap + label.frame.width
        let pill = SKShapeNode(rectOf: CGSize(width: contentW + 44, height: 46), cornerRadius: 23)
        pill.fillColor = UIColor(red: 0.98, green: 0.92, blue: 0.74, alpha: 0.95)
        pill.strokeColor = UIColor(red: 0.84, green: 0.64, blue: 0.28, alpha: 0.4)
        pill.lineWidth = 1
        container.addChild(pill)
        let startX = -contentW / 2
        coin.position = CGPoint(x: startX + 12, y: 0)
        label.position = CGPoint(x: startX + 24 + gap, y: 0)
        container.addChild(coin)
        container.addChild(label)
        addChild(container)
    }

    private func addBackButton(atY y: CGFloat, theme: Theme) {
        let node = SKNode()
        node.name = "shopBack"
        node.position = CGPoint(x: 0, y: y)
        let bg = SKShapeNode(rectOf: CGSize(width: 210, height: 56), cornerRadius: 28)
        bg.fillColor = UIColor(white: 0.96, alpha: 0.95)
        bg.strokeColor = UIColor(white: 0.68, alpha: 0.35)
        bg.lineWidth = 1
        node.addChild(bg)
        let label = SKLabelNode(text: String(localized: "shop.back", defaultValue: "Accueil"))
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 19
        label.fontColor = UIColor(white: 0.32, alpha: 1)
        label.verticalAlignmentMode = .center
        node.addChild(label)
        addChild(node)
    }

    // MARK: - Aperçu de tracé

    private func trailPreview(_ style: TrailStyle) -> SKNode {
        let path = CGMutablePath()
        let pts: [CGPoint] = [(-58, -8), (-29, 12), (0, -12), (29, 12), (58, -8)].map { CGPoint(x: $0.0, y: $0.1) }
        path.move(to: pts[0])
        for p in pts.dropFirst() { path.addLine(to: p) }
        return TrailRenderer.make(path: path, style: style, accent: ThemeManager.shared.active.accent)
    }

    // MARK: - Couleur de texte contrastée

    private func contrastingText(on color: UIColor) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        color.getRed(&r, green: &g, blue: &b, alpha: &a)
        let luminance = 0.299 * r + 0.587 * g + 0.114 * b
        return luminance > 0.6 ? UIColor(white: 0.18, alpha: 1) : .white
    }

    // MARK: - Noms localisés

    private func themeName(_ id: String) -> String {
        switch id {
        case "default": return String(localized: "theme.default", defaultValue: "Pastel")
        case "forest":  return String(localized: "theme.forest", defaultValue: "Forêt")
        case "ocean":   return String(localized: "theme.ocean", defaultValue: "Océan")
        case "desert":  return String(localized: "theme.desert", defaultValue: "Désert")
        case "candy":   return String(localized: "theme.candy", defaultValue: "Bonbon")
        case "space":   return String(localized: "theme.space", defaultValue: "Espace")
        case "night":   return String(localized: "theme.night", defaultValue: "Nuit")
        default:        return id
        }
    }

    private func bubbleStyleName(_ id: String) -> String {
        switch id {
        case "classic": return String(localized: "style.classic", defaultValue: "Classique")
        case "matte":   return String(localized: "style.matte", defaultValue: "Mat")
        case "glossy":  return String(localized: "style.glossy", defaultValue: "Brillant")
        case "glass":   return String(localized: "style.glass", defaultValue: "Verre")
        case "neon":    return String(localized: "style.neon", defaultValue: "Néon")
        default:        return id
        }
    }

    private func boosterName(_ booster: Booster) -> String {
        switch booster {
        case .hint:    return String(localized: "booster.hint", defaultValue: "Indice")
        case .shuffle: return String(localized: "booster.shuffle", defaultValue: "Mélange")
        case .hammer:  return String(localized: "booster.hammer", defaultValue: "Marteau")
        }
    }

    private func trailStyleName(_ id: String) -> String {
        switch id {
        case "classic": return String(localized: "trail.classic", defaultValue: "Classique")
        case "dotted":  return String(localized: "trail.dotted", defaultValue: "Pointillé")
        case "ink":     return String(localized: "trail.ink", defaultValue: "Encre")
        case "ribbon":  return String(localized: "trail.ribbon", defaultValue: "Ruban")
        case "neon":    return String(localized: "trail.neon", defaultValue: "Néon")
        default:        return id
        }
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        for node in nodes(at: point) {
            guard let name = node.name ?? node.parent?.name ?? node.parent?.parent?.name else { continue }
            if name == "shopBack" { goBackToMenu(); return }
            if name.hasPrefix("tab:") {
                if let t = Tab(rawValue: String(name.dropFirst("tab:".count))), t != tab {
                    tab = t
                    HapticManager.light()
                    rebuild()
                }
                return
            }
            if name.hasPrefix("item:theme:") {
                handleTheme(String(name.dropFirst("item:theme:".count)), card: childNode(withName: name)); return
            }
            if name.hasPrefix("item:bubbleStyle:") {
                handleCosmetic(.bubbleStyle, id: String(name.dropFirst("item:bubbleStyle:".count)), card: childNode(withName: name)); return
            }
            if name.hasPrefix("item:trail:") {
                handleCosmetic(.trail, id: String(name.dropFirst("item:trail:".count)), card: childNode(withName: name)); return
            }
            if name.hasPrefix("pack:") {
                handleCoinPack(String(name.dropFirst("pack:".count))); return
            }
            if name.hasPrefix("booster:") {
                handleBoosterPurchase(String(name.dropFirst("booster:".count)),
                                      card: childNode(withName: name)); return
            }
        }
    }

    private func handleBoosterPurchase(_ id: String, card: SKNode?) {
        guard let booster = Booster(rawValue: id) else { return }
        if BoosterManager.shared.purchaseBundle(booster) {
            HapticManager.medium(); rebuild()
        } else {
            shake(card)
        }
    }

    private func handleCoinPack(_ productID: String) {
        guard let product = StoreManager.shared.products.first(where: { $0.id == productID }) else { return }
        HapticManager.light()
        Task { @MainActor in
            let success = await StoreManager.shared.purchase(product)
            if success { HapticManager.medium(); rebuild() }
        }
    }

    private func handleTheme(_ id: String, card: SKNode?) {
        let manager = ThemeManager.shared
        guard let theme = manager.theme(id: id), manager.activeID != id else { return }
        if manager.owns(id) {
            manager.setActive(id); HapticManager.light(); rebuild()
        } else if manager.purchase(theme) {
            manager.setActive(id); HapticManager.medium(); rebuild()
        } else {
            shake(card)
        }
    }

    private func handleCosmetic(_ kind: CosmeticKind, id: String, card: SKNode?) {
        let m = CosmeticManager.shared
        guard m.activeID(kind) != id else { return }
        let price = (kind == .bubbleStyle
                     ? m.bubbleStyles.first { $0.id == id }?.price
                     : m.trails.first { $0.id == id }?.price) ?? 0
        if m.owns(kind, id) {
            m.setActive(kind, id); HapticManager.light(); rebuild()
        } else if m.purchase(kind, id: id, price: price) {
            m.setActive(kind, id); HapticManager.medium(); rebuild()
        } else {
            shake(card)
        }
    }

    private func shake(_ card: SKNode?) {
        HapticManager.light()
        card?.run(.sequence([
            .moveBy(x: 7, y: 0, duration: 0.05),
            .moveBy(x: -14, y: 0, duration: 0.05),
            .moveBy(x: 14, y: 0, duration: 0.05),
            .moveBy(x: -7, y: 0, duration: 0.05),
        ]))
    }

    private func goBackToMenu() {
        let menu = MenuScene(size: size)
        menu.scaleMode = .aspectFill
        view?.presentScene(menu, transition: SKTransition.fade(withDuration: 0.3))
    }
}
