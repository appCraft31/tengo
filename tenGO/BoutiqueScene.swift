//
//  BoutiqueScene.swift
//  tenGO
//
//  Boutique : achète des thèmes avec les pièces gagnées, et sélectionne le thème actif.
//

import SpriteKit

class BoutiqueScene: SKScene {

    private let cardH: CGFloat = 132
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

        let topY = size.height * 0.40

        // Titre
        let title = SKLabelNode(text: String(localized: "shop.title", defaultValue: "Boutique"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 40
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY)
        addChild(title)

        // Solde de pièces
        addBalanceChip(atY: topY - 58, theme: theme)

        // Cartes de thèmes (grille 2 colonnes)
        let themes = ThemeManager.shared.themes
        let startY = topY - 130
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

        let coin = CoinIcon.make(radius: 11)
        coin.zPosition = 1

        let label = SKLabelNode(text: "\(CoinManager.shared.balance)")
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 24
        label.fontColor = theme.logo
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        label.zPosition = 1

        let gap: CGFloat = 8
        let contentW = 22 + gap + label.frame.width
        let startX = -contentW / 2
        coin.position = CGPoint(x: startX + 11, y: 0)
        label.position = CGPoint(x: startX + 22 + gap, y: 0)

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

        // Fond de carte = ambiance du thème (aperçu)
        let bg = SKShapeNode(rectOf: CGSize(width: cardW, height: cardH), cornerRadius: 22)
        bg.fillColor = theme.background
        bg.strokeColor = isActive ? theme.accent : theme.logo.withAlphaComponent(0.25)
        bg.lineWidth = isActive ? 3 : 1
        card.addChild(bg)

        // Aperçu : quelques bulles du thème
        let previewValues = [1, 3, 6, 9]
        let bubbleR: CGFloat = 17
        let spacing: CGFloat = 44
        let totalW = CGFloat(previewValues.count - 1) * spacing
        for (i, v) in previewValues.enumerated() {
            let dot = SKShapeNode(circleOfRadius: bubbleR)
            dot.fillColor = theme.color(forValue: v)
            dot.strokeColor = .clear
            dot.position = CGPoint(x: -totalW / 2 + CGFloat(i) * spacing, y: 30)
            card.addChild(dot)
        }

        // Nom du thème
        let name = SKLabelNode(text: "\(theme.emoji)  \(themeName(theme.id))")
        name.fontName = "AvenirNext-Medium"
        name.fontSize = 20
        name.fontColor = theme.logo
        name.verticalAlignmentMode = .center
        name.position = CGPoint(x: 0, y: -14)
        card.addChild(name)

        // Statut : actif / possédé / prix
        addStatus(to: card, theme: theme, isActive: isActive, owned: owned)

        addChild(card)
    }

    private func addStatus(to card: SKNode, theme: Theme, isActive: Bool, owned: Bool) {
        let y: CGFloat = -46
        if isActive {
            let label = SKLabelNode(text: "✓ " + String(localized: "shop.active", defaultValue: "Actif"))
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 17
            label.fontColor = theme.accent
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: y)
            card.addChild(label)
        } else if owned {
            let label = SKLabelNode(text: String(localized: "shop.select", defaultValue: "Choisir"))
            label.fontName = "AvenirNext-Medium"
            label.fontSize = 17
            label.fontColor = theme.logo.withAlphaComponent(0.85)
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: y)
            card.addChild(label)
        } else {
            // Prix : pièce + montant
            let priceLabel = SKLabelNode(text: "\(theme.price)")
            priceLabel.fontName = "AvenirNext-Bold"
            priceLabel.fontSize = 18
            priceLabel.fontColor = theme.logo
            priceLabel.verticalAlignmentMode = .center
            priceLabel.horizontalAlignmentMode = .left

            let coin = CoinIcon.make(radius: 9)
            let gap: CGFloat = 6
            let contentW = 18 + gap + priceLabel.frame.width
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
