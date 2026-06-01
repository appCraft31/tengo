//
//  BoutiqueScene.swift
//  tenGO
//
//  Boutique : achète des thèmes avec les pièces gagnées, et sélectionne le thème actif.
//

import SpriteKit

class BoutiqueScene: SKScene {

    private let cardH: CGFloat = 152
    // Largeur réellement visible (l'aspectFill rogne la largeur) — calculée depuis la vue.
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

        // Titre
        let title = SKLabelNode(text: String(localized: "shop.title", defaultValue: "Boutique"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 40
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY)
        addChild(title)

        // Solde de pièces
        addBalanceChip(atY: topY - 62, theme: theme)

        // Cartes de thèmes (grille 2 colonnes)
        let themes = ThemeManager.shared.themes
        let startY = topY - 168
        let colCenter = usableWidth / 4
        let colX: [CGFloat] = [-colCenter, colCenter]
        for (i, t) in themes.enumerated() {
            let col = i % 2
            let row = i / 2
            let pos = CGPoint(x: colX[col], y: startY - CGFloat(row) * (cardH + 18))
            addThemeCard(t, at: pos)
        }

        // Bouton retour
        addBackButton(atY: -size.height * 0.43, theme: theme)
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

        // Pilule dorée
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

    private func addThemeCard(_ theme: Theme, at position: CGPoint) {
        let card = SKNode()
        card.name = "theme:\(theme.id)"
        card.position = position

        let isActive = ThemeManager.shared.activeID == theme.id
        let owned = ThemeManager.shared.owns(theme.id)
        let cardSize = CGSize(width: cardW, height: cardH)

        // Ombre portée douce (profondeur)
        let shadow = SKShapeNode(rectOf: cardSize, cornerRadius: 26)
        shadow.fillColor = UIColor(white: 0, alpha: 0.08)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: -5)
        shadow.zPosition = -1
        card.addChild(shadow)

        // Fond de carte = ambiance du thème (aperçu)
        let bg = SKShapeNode(rectOf: cardSize, cornerRadius: 26)
        bg.fillColor = theme.background
        bg.strokeColor = isActive ? theme.accent : theme.logo.withAlphaComponent(0.18)
        bg.lineWidth = isActive ? 3 : 1
        card.addChild(bg)

        // Aperçu : 5 bulles du thème, joliment espacées en haut
        let previewValues = [1, 3, 5, 7, 9]
        let bubbleR: CGFloat = 16
        let spacing = min(42, (cardW - 56) / CGFloat(previewValues.count - 1))
        let totalW = CGFloat(previewValues.count - 1) * spacing
        for (i, v) in previewValues.enumerated() {
            let dot = SKShapeNode(circleOfRadius: bubbleR)
            dot.fillColor = theme.color(forValue: v)
            dot.strokeColor = UIColor(white: 1, alpha: 0.22)
            dot.lineWidth = 1
            dot.position = CGPoint(x: -totalW / 2 + CGFloat(i) * spacing, y: 42)
            card.addChild(dot)
        }

        // Nom du thème (sans emoji — l'identité passe par les couleurs)
        let name = SKLabelNode(text: themeName(theme.id))
        name.fontName = "AvenirNext-DemiBold"
        name.fontSize = 22
        name.fontColor = theme.logo
        name.verticalAlignmentMode = .center
        name.position = CGPoint(x: 0, y: -6)
        card.addChild(name)

        // Badge de statut (pilule)
        addStatusBadge(to: card, theme: theme, isActive: isActive, owned: owned, atY: -48)

        addChild(card)
    }

    private func addStatusBadge(to card: SKNode, theme: Theme, isActive: Bool, owned: Bool, atY y: CGFloat) {
        if isActive {
            // Pilule pleine accent + « Actif »
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
            // Pilule contour + « Choisir »
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
            // Prix : pièce + montant, dans une pilule discrète
            let priceLabel = SKLabelNode(text: "\(theme.price)")
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

    /// Couleur de texte lisible sur un fond donné (clair → texte foncé, sombre → blanc).
    private func contrastingText(on color: UIColor) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        color.getRed(&r, green: &g, blue: &b, alpha: &a)
        let luminance = 0.299 * r + 0.587 * g + 0.114 * b
        return luminance > 0.6 ? UIColor(white: 0.18, alpha: 1) : .white
    }

    /// Nom localisé du thème (clés statiques → extraction + fallback français).
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

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        for node in nodes(at: point) {
            let name = node.name ?? node.parent?.name ?? node.parent?.parent?.name
            guard let name else { continue }
            if name == "shopBack" {
                goBackToMenu()
                return
            }
            if name.hasPrefix("theme:") {
                let id = String(name.dropFirst("theme:".count))
                handleThemeTap(id: id, cardNode: cardNode(named: name))
                return
            }
        }
    }

    private func cardNode(named name: String) -> SKNode? {
        children.first { $0.name == name }
    }

    private func handleThemeTap(id: String, cardNode: SKNode?) {
        guard let theme = ThemeManager.shared.theme(id: id) else { return }
        let manager = ThemeManager.shared

        if manager.activeID == id { return }   // déjà actif

        if manager.owns(id) {
            manager.setActive(id)
            HapticManager.light()
            rebuild()
            return
        }

        // Achat
        if manager.purchase(theme) {
            manager.setActive(id)
            HapticManager.medium()
            rebuild()
        } else {
            // Fonds insuffisants → secousse
            HapticManager.light()
            cardNode?.run(SKAction.sequence([
                SKAction.moveBy(x: 7, y: 0, duration: 0.05),
                SKAction.moveBy(x: -14, y: 0, duration: 0.05),
                SKAction.moveBy(x: 14, y: 0, duration: 0.05),
                SKAction.moveBy(x: -7, y: 0, duration: 0.05),
            ]))
        }
    }

    private func goBackToMenu() {
        let menu = MenuScene(size: size)
        menu.scaleMode = .aspectFill
        view?.presentScene(menu, transition: SKTransition.fade(withDuration: 0.3))
    }
}
