//
//  BoutiqueScene.swift
//  tenGO
//
//  Boutique en une seule page défilante, classée par sections : Thèmes,
//  Matières de bulles, Styles de tracé, Boosters, Pièces (dont « Sans pub »).
//  Achat avec les pièces gagnées, sélection du cosmétique actif. Tout est non bloquant.
//

import SpriteKit
import StoreKit

class BoutiqueScene: SKScene {

    private var attemptedProductLoad = false

    /// Mode guidé (premier achat) : défile jusqu'aux Boosters et met en avant
    /// la carte du lot d'indices jusqu'à l'achat ou une autre interaction.
    var guidedPurchase = false
    private var purchaseGuide: CoachMarkOverlay?
    /// Sortie « Plus tard » du tutoriel d'achat.
    private var guideLaterButton: SKNode?

    /// Écran de retour. `.game` quand la boutique a été ouverte depuis une
    /// partie en cours : le joueur doit retrouver sa grille, pas le menu.
    enum ReturnDestination { case menu, game }
    var returnDestination: ReturnDestination = .menu

    private let cardH: CGFloat = 150
    private var usableWidth: CGFloat = 600
    private var usableHeight: CGFloat = 800
    private var cardW: CGFloat = 280
    /// Marge haute réellement sûre (encoche / Dynamic Island), en points scène.
    private var safeTop: CGFloat = 47
    /// Marge basse réellement sûre (indicateur d'accueil), en points scène.
    private var safeBottom: CGFloat = 20

    // MARK: - Défilement (page unique)

    /// Contenu défilant (toutes les sections), masqué à la zone visible.
    private var contentNode = SKNode()
    private var contentHeight: CGFloat = 0
    private var viewportTop: CGFloat = 0
    private var viewportBottom: CGFloat = 0
    /// Décalage de défilement appliqué (0 = haut de page).
    private var scrollOffset: CGFloat = 0
    /// Position y (relative au contenu) du haut de chaque section.
    private var sectionTops: [String: CGFloat] = [:]
    private var dragStartY: CGFloat?
    private var dragStartOffset: CGFloat = 0
    private var isDragging = false

    private var maxScroll: CGFloat {
        max(0, contentHeight - (viewportTop - viewportBottom))
    }

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scaleMode = .aspectFill
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        usableWidth = view.bounds.width / scale
        usableHeight = view.bounds.height / scale
        cardW = usableWidth / 2 - 16
        // safeAreaInsets peut être nul au lancement : plancher à ~47 pt (encoche).
        safeTop = max(view.safeAreaInsets.top, 47) / scale
        safeBottom = max(view.safeAreaInsets.bottom, 20) / scale
        rebuild()
        // Capture de review App Store des achats in-app : « Sans pub » est déjà
        // en haut de page ; SHOP_COINS=1 défile en plus jusqu'aux packs.
        if ProcessInfo.processInfo.environment["SHOP_COINS"] == "1" { scrollTo(section: "coins") }
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": true])
    }

    /// Défile pour amener le haut d'une section en haut de la zone visible.
    private func scrollTo(section key: String) {
        guard let top = sectionTops[key] else { return }
        scrollOffset = min(max(0, -top - 6), maxScroll)
        contentNode.position = CGPoint(x: 0, y: viewportTop + scrollOffset)
    }

    // MARK: - Layout

    private func rebuild() {
        removeAllChildren()
        let theme = ThemeManager.shared.active
        backgroundColor = theme.background

        // Chrome fixe, calé sur la hauteur réellement visible (aspectFill),
        // sous l'encoche / Dynamic Island.
        let topY = usableHeight / 2 - safeTop - 26
        let bottomY = -usableHeight / 2

        let title = SKLabelNode(text: String(localized: "shop.title", defaultValue: "Boutique"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 38
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY)
        title.zPosition = 10
        addChild(title)

        addBalanceChip(atY: topY - 56, theme: theme)

        // Ouverte depuis l'accueil, la boutique EST l'onglet Boutique. Ouverte
        // depuis une partie, elle garde son bouton de retour : les onglets
        // ramèneraient au menu et abandonneraient la partie en cours.
        let bottomChromeH: CGFloat
        switch returnDestination {
        case .menu:
            let tabBar = TabBar.make(width: usableWidth, selected: .shop)
            tabBar.position = CGPoint(x: 0, y: bottomY + safeBottom + TabBar.height / 2)
            tabBar.zPosition = 10
            addChild(tabBar)
            bottomChromeH = safeBottom + TabBar.height
        case .game:
            addBackButton(atY: bottomY + 64, theme: theme)
            bottomChromeH = 88   // conserve exactement l'ancien viewport
        }

        // Zone défilante entre le solde et le pied d'écran (masquée au viewport).
        viewportTop = topY - 100
        viewportBottom = bottomY + bottomChromeH + 16
        let crop = SKCropNode()
        let mask = SKSpriteNode(color: .white,
                                size: CGSize(width: usableWidth, height: viewportTop - viewportBottom))
        mask.position = CGPoint(x: 0, y: (viewportTop + viewportBottom) / 2)
        crop.maskNode = mask
        addChild(crop)
        contentNode = SKNode()
        crop.addChild(contentNode)

        buildContent(theme: theme)

        scrollOffset = min(scrollOffset, maxScroll)
        contentNode.position = CGPoint(x: 0, y: viewportTop + scrollOffset)

        // rebuild() détruit tout : ré-attache le guide tant qu'il est actif,
        // en défilant d'abord jusqu'à la section Boosters.
        if guidedPurchase {
            scrollTo(section: "boosters")
            // Cible la pilule ×1 : le tutoriel fait acheter UNE unité (30), pas
            // le lot (120). Repli sur la carte si la pilule est introuvable.
            let hint = Booster.hint
            let target = contentNode.childNode(withName: "booster:\(hint.rawValue):1")
                ?? contentNode.childNode(withName: "booster:\(hint.rawValue)")
            if let target = target {
                let guide = CoachMarkOverlay.present(
                    in: self, over: target,
                    text: String(localized: "guide.shop_item",
                                 defaultValue: "Achète un indice pour 30 pièces. Il te sortira d'un mauvais pas."))
                // Insistance : taper à côté ne ferme pas le guide, la cible pulse.
                // Le joueur n'est pas piégé pour autant — le lien « Plus tard »
                // ci-dessous et le bouton Retour restent actifs.
                guide.blocksOutsideTaps = true
                purchaseGuide = guide
                addGuideLaterButton()
            }
        }
    }

    /// Sortie explicite du tutoriel d'achat. Sans elle, `blocksOutsideTaps`
    /// enfermerait le joueur — ce qu'on refuse (dark pattern, et Apple le
    /// refuse aussi).
    private func addGuideLaterButton() {
        guideLaterButton?.removeFromParent()
        let label = SKLabelNode(text: String(localized: "guide.shop_later", defaultValue: "Plus tard"))
        label.name = "guideLater"
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 16
        label.fontColor = UIColor(white: 0.98, alpha: 0.9)
        label.verticalAlignmentMode = .center
        // Assez haut pour ne pas se confondre avec le bouton « Accueil ».
        label.position = CGPoint(x: 0, y: -usableHeight / 2 + 168)
        label.zPosition = 96   // au-dessus du voile du coach-mark (90)
        addChild(label)
        guideLaterButton = label
    }

    private func dismissGuideAsLater() {
        purchaseGuide?.dismiss()
        purchaseGuide = nil
        guidedPurchase = false
        guideLaterButton?.removeFromParent()
        guideLaterButton = nil
        let defaults = UserDefaults.standard
        let snoozed = defaults.integer(forKey: AppConfig.UserDefaultsKey.shopGuideSnooze) + 1
        defaults.set(snoozed, forKey: AppConfig.UserDefaultsKey.shopGuideSnooze)
        AnalyticsService.shopGuide(step: "later")
    }

    /// Empile toutes les sections dans contentNode (0 = haut du contenu, on
    /// descend en y négatif). Renseigne contentHeight et sectionTops.
    private func buildContent(theme: Theme) {
        sectionTops = [:]
        var cursor: CGFloat = 0
        let colCenter = usableWidth / 4
        let colX: [CGFloat] = [-colCenter, colCenter]
        let rowGap: CGFloat = 18
        let sectionGap: CGFloat = 34

        func header(_ text: String, key: String) {
            sectionTops[key] = cursor
            cursor -= 26
            let label = SKLabelNode(text: text)
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 24
            label.fontColor = theme.logo
            label.verticalAlignmentMode = .center
            label.horizontalAlignmentMode = .left
            label.position = CGPoint(x: -usableWidth / 2 + 22, y: cursor)
            contentNode.addChild(label)
            cursor -= 30
        }

        func gridPlace(_ index: Int) -> CGPoint {
            CGPoint(x: colX[index % 2],
                    y: cursor - cardH / 2 - CGFloat(index / 2) * (cardH + rowGap))
        }

        func endGrid(count: Int) {
            let rows = (count + 1) / 2
            cursor -= CGFloat(rows) * (cardH + rowGap) + sectionGap
        }

        // « Sans pub » mis en avant, tout en haut de la boutique.
        addNoAdsCard(theme: theme, cursor: &cursor)

        header(String(localized: "shop.tab_themes", defaultValue: "Thèmes"), key: "themes")
        for (i, t) in ThemeManager.shared.themes.enumerated() { addThemeCard(t, at: gridPlace(i)) }
        endGrid(count: ThemeManager.shared.themes.count)

        header(String(localized: "shop.tab_bubbles", defaultValue: "Matières"), key: "bubbles")
        for (i, s) in CosmeticManager.shared.bubbleStyles.enumerated() {
            let preview = BubbleNode.makeVisual(value: 5, styleKind: s.kind, radius: 23)
            addCosmeticCard(kind: .bubbleStyle, id: s.id, price: s.price,
                            title: bubbleStyleName(s.id), preview: preview, previewY: 36, at: gridPlace(i))
        }
        endGrid(count: CosmeticManager.shared.bubbleStyles.count)

        header(String(localized: "shop.tab_trails", defaultValue: "Tracés"), key: "trails")
        for (i, t) in CosmeticManager.shared.trails.enumerated() {
            addCosmeticCard(kind: .trail, id: t.id, price: t.price,
                            title: trailStyleName(t.id), preview: trailPreview(t), previewY: 36, at: gridPlace(i))
        }
        endGrid(count: CosmeticManager.shared.trails.count)

        header(String(localized: "shop.tab_boosters", defaultValue: "Boosters"), key: "boosters")
        // Rangées pleine largeur (pas la grille) : elles portent une description.
        for booster in Booster.allCases { addBoosterRow(booster, theme: theme, cursor: &cursor) }
        cursor -= sectionGap - 16   // le pas de rangée inclut déjà 16 pt

        header(String(localized: "shop.tab_coins", defaultValue: "Pièces"), key: "coins")

        let products = StoreManager.shared.products
        if ProcessInfo.processInfo.environment["SHOP_MODE"] == "1" && products.isEmpty {
            // Rendu déterministe pour la capture App Store (sans dépendance réseau StoreKit).
            let mocks: [(Int, String)] = [(500, "$0.99"), (1200, "$1.99"), (3000, "$3.99"), (6500, "$7.99")]
            for (i, m) in mocks.enumerated() { addCoinPackMock(amount: m.0, priceText: m.1, at: gridPlace(i)) }
            endGrid(count: mocks.count)
        } else if products.isEmpty {
            let msg = attemptedProductLoad
                ? String(localized: "shop.coins_unavailable", defaultValue: "Indisponible pour le moment")
                : String(localized: "shop.coins_loading", defaultValue: "Chargement…")
            let label = SKLabelNode(text: msg)
            label.fontName = "AvenirNext-Medium"
            label.fontSize = 19
            label.fontColor = theme.logo.withAlphaComponent(0.7)
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: cursor - 30)
            contentNode.addChild(label)
            cursor -= 80
            if !attemptedProductLoad {
                attemptedProductLoad = true
                Task { @MainActor in
                    await StoreManager.shared.loadProducts()
                    self.rebuild()
                }
            }
        } else {
            for (i, p) in products.enumerated() { addCoinPackCard(p, at: gridPlace(i)) }
            endGrid(count: products.count)
        }

        contentHeight = -cursor + 8
    }

    /// Carte pleine largeur « Sans pub » (non-consommable), mise en avant
    /// entre Thèmes et Matières : fond accentué + halo pulsant tant que
    /// l'achat n'a pas été fait.
    private func addNoAdsCard(theme: Theme, cursor: inout CGFloat) {
        let purchased = AdFreeManager.shared.isPurchased
        let h: CGFloat = purchased ? 116 : 132
        let w = usableWidth - 32
        let card = SKNode()
        card.name = "noAds"
        card.position = CGPoint(x: 0, y: cursor - h / 2)

        let ink = contrastingText(on: theme.accent)

        // Halo pulsant derrière la carte (seulement tant que non acheté).
        if !purchased {
            let halo = SKShapeNode(rectOf: CGSize(width: w + 12, height: h + 12), cornerRadius: 32)
            halo.fillColor = theme.accent.withAlphaComponent(0.35)
            halo.strokeColor = .clear
            halo.zPosition = -2
            halo.run(.repeatForever(.sequence([
                .group([.scale(to: 1.03, duration: 1.1), .fadeAlpha(to: 0.5, duration: 1.1)]),
                .group([.scale(to: 1.0, duration: 1.1), .fadeAlpha(to: 1.0, duration: 1.1)]),
            ])))
            card.addChild(halo)
        }

        let shadow = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: 26)
        shadow.fillColor = UIColor(white: 0, alpha: 0.08)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: -5)
        shadow.zPosition = -1
        card.addChild(shadow)

        let bg = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: 26)
        bg.fillColor = theme.accent
        bg.strokeColor = theme.logo.withAlphaComponent(0.25)
        bg.lineWidth = purchased ? 3 : 1.5
        card.addChild(bg)

        // Pictogramme « écran publicitaire barré ».
        let picto = SKNode()
        picto.position = CGPoint(x: -w / 2 + 48, y: 0)
        let screen = SKShapeNode(rectOf: CGSize(width: 34, height: 24), cornerRadius: 5)
        screen.fillColor = .clear
        screen.strokeColor = ink.withAlphaComponent(0.85)
        screen.lineWidth = 2.5
        picto.addChild(screen)
        let slashPath = CGMutablePath()
        slashPath.move(to: CGPoint(x: -19, y: -14))
        slashPath.addLine(to: CGPoint(x: 19, y: 14))
        let slash = SKShapeNode(path: slashPath)
        slash.strokeColor = ink.withAlphaComponent(0.85)
        slash.lineWidth = 3
        slash.lineCap = .round
        picto.addChild(slash)
        card.addChild(picto)

        let titleLabel = SKLabelNode(text: String(localized: "menu.remove_ads", defaultValue: "Sans pub"))
        titleLabel.fontName = "AvenirNext-Bold"
        titleLabel.fontSize = 23
        titleLabel.fontColor = ink
        titleLabel.verticalAlignmentMode = .center
        titleLabel.horizontalAlignmentMode = .left
        titleLabel.position = CGPoint(x: -w / 2 + 86, y: purchased ? 12 : 22)
        card.addChild(titleLabel)

        let subtitle = SKLabelNode(text: String(localized: "shop.no_ads_desc",
                                                defaultValue: "Supprime définitivement la publicité"))
        subtitle.fontName = "AvenirNext-Medium"
        subtitle.fontSize = 14
        subtitle.fontColor = ink.withAlphaComponent(0.75)
        subtitle.verticalAlignmentMode = .center
        subtitle.horizontalAlignmentMode = .left
        subtitle.position = CGPoint(x: -w / 2 + 86, y: purchased ? -14 : -2)
        card.addChild(subtitle)

        // Bonus de bienvenue : 500 pièces offertes avec l'achat.
        if !purchased {
            let bonusCoin = CoinIcon.make(radius: 8)
            bonusCoin.position = CGPoint(x: -w / 2 + 94, y: -26)
            card.addChild(bonusCoin)
            let bonus = SKLabelNode(text: String(localized: "shop.no_ads_bonus",
                                                 defaultValue: "+ 500 pièces offertes"))
            bonus.fontName = "AvenirNext-Bold"
            bonus.fontSize = 14
            bonus.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
            bonus.verticalAlignmentMode = .center
            bonus.horizontalAlignmentMode = .left
            bonus.position = CGPoint(x: -w / 2 + 108, y: -26)
            card.addChild(bonus)
        }

        if purchased {
            let pill = SKShapeNode(rectOf: CGSize(width: 96, height: 36), cornerRadius: 18)
            pill.fillColor = theme.background
            pill.strokeColor = .clear
            pill.position = CGPoint(x: w / 2 - 70, y: 0)
            card.addChild(pill)
            let label = SKLabelNode(text: String(localized: "shop.active", defaultValue: "Actif"))
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 15
            label.fontColor = theme.logo
            label.verticalAlignmentMode = .center
            label.position = pill.position
            card.addChild(label)
        } else {
            let priceText = StoreManager.shared.adFreeProduct?.displayPrice ?? "3,99 €"
            let pill = SKShapeNode(rectOf: CGSize(width: 110, height: 36), cornerRadius: 18)
            pill.fillColor = theme.background
            pill.strokeColor = .clear
            pill.position = CGPoint(x: w / 2 - 74, y: 0)
            card.addChild(pill)
            let label = SKLabelNode(text: priceText)
            label.fontName = "AvenirNext-Bold"
            label.fontSize = 16
            label.fontColor = theme.logo
            label.verticalAlignmentMode = .center
            label.position = pill.position
            card.addChild(label)
        }

        contentNode.addChild(card)
        cursor -= h + 18
    }

    /// Rangée pleine largeur d'un booster : icône, nom, **description**, stock,
    /// et deux prix (unité / lot).
    ///
    /// Format choisi plutôt que la grille 2 colonnes parce que `cardH` (150) est
    /// global à toute la boutique : l'agrandir pour loger une description aurait
    /// aussi étiré thèmes, matières, tracés et packs de pièces. Une rangée offre
    /// en prime une bien meilleure cible pour le coach-mark du tutoriel d'achat.
    private func addBoosterRow(_ booster: Booster, theme: Theme, cursor: inout CGFloat) {
        let h: CGFloat = 112
        let w = usableWidth - 32
        let card = SKNode()
        card.name = "booster:\(booster.rawValue)"
        card.position = CGPoint(x: 0, y: cursor - h / 2)

        let shadow = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: 26)
        shadow.fillColor = UIColor(white: 0, alpha: 0.08)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: -5)
        shadow.zPosition = -1
        card.addChild(shadow)

        let bg = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: 26)
        bg.fillColor = UIColor(red: 0.99, green: 0.97, blue: 0.94, alpha: 1)
        bg.strokeColor = theme.logo.withAlphaComponent(0.18)
        bg.lineWidth = 1.5
        card.addChild(bg)

        let icon = BoosterIcon.make(booster, size: 40, color: theme.logo)
        icon.position = CGPoint(x: -w / 2 + 48, y: 0)
        card.addChild(icon)

        let textX = -w / 2 + 86
        // Largeur laissée libre par les pilules de prix, à droite.
        let textWidth = w - 86 - 176

        let title = SKLabelNode(text: booster.title)
        title.fontName = "AvenirNext-Bold"
        title.fontSize = 22
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.horizontalAlignmentMode = .left
        title.position = CGPoint(x: textX, y: 30)
        card.addChild(title)

        let desc = SKLabelNode(text: booster.details)
        desc.fontName = "AvenirNext-Medium"
        desc.fontSize = 14
        desc.fontColor = UIColor(white: 0.32, alpha: 1)
        desc.verticalAlignmentMode = .center
        desc.horizontalAlignmentMode = .left
        desc.numberOfLines = 2
        desc.lineBreakMode = .byWordWrapping
        desc.preferredMaxLayoutWidth = textWidth
        desc.position = CGPoint(x: textX, y: 2)
        card.addChild(desc)

        let owned = BoosterManager.shared.count(booster)
        let stock = SKLabelNode(text: String(format: String(localized: "shop.booster_stock",
                                                            defaultValue: "en stock : %lld"), owned))
        stock.fontName = "AvenirNext-Medium"
        stock.fontSize = 13
        stock.fontColor = UIColor(white: 0.45, alpha: 1)
        stock.verticalAlignmentMode = .center
        stock.horizontalAlignmentMode = .left
        stock.position = CGPoint(x: textX, y: -34)
        card.addChild(stock)

        // Deux achats possibles : l'unité (utilisée par le tutoriel) et le lot remisé.
        addBoosterPricePill(to: card, booster: booster, quantity: 1,
                            price: booster.price, theme: theme,
                            at: CGPoint(x: w / 2 - 74, y: 24))
        addBoosterPricePill(to: card, booster: booster, quantity: booster.bundleQuantity,
                            price: booster.bundlePrice, theme: theme,
                            at: CGPoint(x: w / 2 - 74, y: -24))

        contentNode.addChild(card)
        cursor -= h + 16
    }

    private func addBoosterPricePill(to card: SKNode, booster: Booster, quantity: Int,
                                     price: Int, theme: Theme, at position: CGPoint) {
        let name = "booster:\(booster.rawValue):\(quantity)"
        let affordable = CoinManager.shared.balance >= price

        let pill = SKShapeNode(rectOf: CGSize(width: 124, height: 38), cornerRadius: 19)
        pill.name = name
        pill.fillColor = affordable ? theme.accent : UIColor(white: 0.90, alpha: 1)
        pill.strokeColor = .clear
        pill.position = position
        card.addChild(pill)

        let ink = affordable ? contrastingText(on: theme.accent) : UIColor(white: 0.45, alpha: 1)

        let qty = SKLabelNode(text: "×\(quantity)")
        qty.name = name
        qty.fontName = "AvenirNext-Bold"
        qty.fontSize = 15
        qty.fontColor = ink
        qty.verticalAlignmentMode = .center
        qty.horizontalAlignmentMode = .left
        qty.position = CGPoint(x: position.x - 46, y: position.y)
        card.addChild(qty)

        let coin = CoinIcon.make(radius: 8)
        coin.name = name
        coin.position = CGPoint(x: position.x + 2, y: position.y)
        card.addChild(coin)

        let priceLabel = SKLabelNode(text: "\(price)")
        priceLabel.name = name
        priceLabel.fontName = "AvenirNext-Bold"
        priceLabel.fontSize = 16
        priceLabel.fontColor = ink
        priceLabel.verticalAlignmentMode = .center
        priceLabel.horizontalAlignmentMode = .left
        priceLabel.position = CGPoint(x: position.x + 14, y: position.y)
        card.addChild(priceLabel)
    }

    /// Bonus de bienvenue crédité avec l'achat « sans pub ».
    private static let noAdsBonusCoins = 500

    /// Achat du mod « sans pub » depuis la Boutique. Succès → +500 pièces,
    /// rebuild (badge « Actif ») ; les pubs sont coupées via .tenGOAdFreeChanged.
    private func handleNoAds() {
        guard !AdFreeManager.shared.isPurchased else { return }
        guard let product = StoreManager.shared.adFreeProduct else {
            shake(contentNode.childNode(withName: "noAds"))
            Task { @MainActor in
                await StoreManager.shared.loadProducts()
                self.rebuild()
            }
            return
        }
        HapticManager.light()
        Task { @MainActor in
            let success = await StoreManager.shared.purchase(product)
            if success {
                HapticManager.medium()
                CoinManager.shared.add(Self.noAdsBonusCoins)
                self.rebuild()
                self.showNoAdsBonusFeedback()
            } else {
                self.shake(self.contentNode.childNode(withName: "noAds"))
            }
        }
    }

    /// « +500 » doré qui s'élève depuis le solde après l'achat sans pub.
    private func showNoAdsBonusFeedback() {
        let chipY = usableHeight / 2 - safeTop - 26 - 56
        let popup = SKLabelNode(text: "+\(Self.noAdsBonusCoins)")
        popup.fontName = "AvenirNext-Heavy"
        popup.fontSize = 26
        popup.fontColor = UIColor(red: 0.85, green: 0.60, blue: 0.15, alpha: 1)
        popup.verticalAlignmentMode = .center
        popup.position = CGPoint(x: 0, y: chipY - 34)
        popup.zPosition = 30
        addChild(popup)
        popup.run(SKAction.sequence([
            SKAction.group([
                SKAction.moveBy(x: 0, y: 52, duration: 0.8),
                SKAction.sequence([SKAction.wait(forDuration: 0.4), SKAction.fadeOut(withDuration: 0.4)])
            ]),
            SKAction.removeFromParent()
        ]))
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

        if let card = contentNode.childNode(withName: "pack:\(product.id)") {
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

    /// Carte de pack de pièces factice (capture de review App Store, sans StoreKit).
    private func addCoinPackMock(amount: Int, priceText: String, at position: CGPoint) {
        let theme = ThemeManager.shared.active
        let preview = SKNode()
        preview.addChild(CoinIcon.make(radius: 24))
        addCardFrame(name: "packmock:\(amount)", background: theme.background,
                     border: theme.logo.withAlphaComponent(0.18), borderWidth: 1,
                     title: "\(amount)", titleColor: theme.logo,
                     preview: preview, previewY: 40, at: position)
        guard let card = contentNode.childNode(withName: "packmock:\(amount)") else { return }
        let pill = SKShapeNode(rectOf: CGSize(width: 110, height: 34), cornerRadius: 17)
        pill.fillColor = theme.accent
        pill.strokeColor = .clear
        pill.position = CGPoint(x: 0, y: -48)
        card.addChild(pill)
        let label = SKLabelNode(text: priceText)
        label.fontName = "AvenirNext-Bold"
        label.fontSize = 16
        label.fontColor = contrastingText(on: theme.accent)
        label.verticalAlignmentMode = .center
        label.position = CGPoint(x: 0, y: -48)
        card.addChild(label)
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
        if let card = contentNode.childNode(withName: "item:theme:\(theme.id)") {
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
        if let card = contentNode.childNode(withName: "item:\(prefix):\(id)") {
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

        contentNode.addChild(card)
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
        node.zPosition = 10
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
        dragStartY = touch.location(in: self).y
        dragStartOffset = scrollOffset
        isDragging = false
    }

    override func touchesMoved(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first, let startY = dragStartY else { return }
        // Pendant le tutoriel d'achat, le défilement est verrouillé : la section
        // Boosters a été amenée en vue, la cible ne doit pas pouvoir s'échapper.
        if purchaseGuide?.parent != nil { return }

        let dy = touch.location(in: self).y - startY
        if !isDragging && abs(dy) > 8 {
            isDragging = true
        }
        if isDragging {
            scrollOffset = min(max(0, dragStartOffset + dy), maxScroll)
            contentNode.position = CGPoint(x: 0, y: viewportTop + scrollOffset)
        }
    }

    override func touchesCancelled(_ touches: Set<UITouch>, with event: UIEvent?) {
        isDragging = false
        dragStartY = nil
    }

    /// Les actions se déclenchent au relâchement : un drag (défilement) ne
    /// doit jamais valider une carte sous le doigt.
    override func touchesEnded(_ touches: Set<UITouch>, with event: UIEvent?) {
        defer { dragStartY = nil }
        guard let touch = touches.first else { return }
        if isDragging { isDragging = false; return }
        let point = touch.location(in: self)

        // Guide d'achat. Deux sorties seulement : acheter, ou « Plus tard ».
        // Tout autre tap laisse le guide en place (la cible pulse).
        if let guide = purchaseGuide, guide.parent != nil {
            if nodes(at: point).contains(where: { ($0.name ?? "") == "guideLater" }) {
                dismissGuideAsLater()
                return
            }
            let result = guide.handleTouch(at: point)
            switch result {
            case .ignored:
                return          // insistance : rien d'autre ne se produit
            case .target:
                guideLaterButton?.removeFromParent()
                guideLaterButton = nil
                completeGuidedPurchase()
                return
            case .dismissed:
                purchaseGuide = nil
                guidedPurchase = false
                guideLaterButton?.removeFromParent()
                guideLaterButton = nil
            }
        }

        for node in nodes(at: point) {
            guard let name = node.name ?? node.parent?.name ?? node.parent?.parent?.name else { continue }

            if name == "shopBack" { goBackToMenu(); return }
            if name == "noAds" { handleNoAds(); return }
            if name.hasPrefix("item:theme:") {
                handleTheme(String(name.dropFirst("item:theme:".count)), card: contentNode.childNode(withName: name)); return
            }
            if name.hasPrefix("item:bubbleStyle:") {
                handleCosmetic(.bubbleStyle, id: String(name.dropFirst("item:bubbleStyle:".count)), card: contentNode.childNode(withName: name)); return
            }
            if name.hasPrefix("item:trail:") {
                handleCosmetic(.trail, id: String(name.dropFirst("item:trail:".count)), card: contentNode.childNode(withName: name)); return
            }
            if name.hasPrefix("pack:") {
                handleCoinPack(String(name.dropFirst("pack:".count))); return
            }
            if name.hasPrefix("booster:") {
                // "booster:<raw>" (carte) ou "booster:<raw>:<qty>" (pilule de prix).
                let parts = name.split(separator: ":")
                guard parts.count >= 2 else { return }
                let raw = String(parts[1])
                let qty = parts.count > 2 ? (Int(parts[2]) ?? 1) : 1
                handleBoosterPurchase(raw, quantity: qty,
                                      card: contentNode.childNode(withName: "booster:\(raw)"))
                return
            }
        }

        if returnDestination == .menu, let tab = TabBar.tab(at: point, in: self), tab != .shop {
            TabBar.present(tab, from: self)
        }
    }

    /// `quantity` = 1 pour l'achat unitaire, `bundleQuantity` pour le lot remisé.
    private func handleBoosterPurchase(_ id: String, quantity: Int, card: SKNode?) {
        guard let booster = Booster(rawValue: id) else { return }
        let isBundle = quantity == booster.bundleQuantity && quantity > 1
        let cost = isBundle ? booster.bundlePrice : booster.price * max(1, quantity)
        let ok = isBundle
            ? BoosterManager.shared.purchaseBundle(booster)
            : BoosterManager.shared.purchase(booster, quantity: max(1, quantity))
        guard ok else {
            shake(card)
            return
        }
        AnalyticsService.boosterPurchased(booster.rawValue, quantity: quantity,
                                          cost: cost, source: "shop")
        HapticManager.medium()
        rebuild()
    }

    /// Achat déclenché depuis le coach-mark : achète UNE unité d'indice, puis
    /// rembourse intégralement — le premier booster est offert.
    ///
    /// Le débit puis le crédit sont montrés successivement, à 1,4 s d'écart :
    /// le joueur apprend qu'acheter coûte des pièces, sans rien perdre. Un
    /// remboursement différé (après usage en partie) aurait exigé un état à
    /// honorer au prochain lancement, et lésé qui ne rejoue pas.
    private func completeGuidedPurchase() {
        let hint = Booster.hint
        let card = contentNode.childNode(withName: "booster:\(hint.rawValue)")
        guard BoosterManager.shared.purchase(hint, quantity: 1) else {
            shake(card)
            return
        }
        AnalyticsService.boosterPurchased(hint.rawValue, quantity: 1,
                                          cost: hint.price, source: "tutorial")
        AnalyticsService.shopGuide(step: "purchased")

        // Le guide ne doit plus se ré-attacher lors des rebuild() qui suivent.
        guidedPurchase = false
        purchaseGuide?.dismiss()
        purchaseGuide = nil

        HapticManager.medium()
        rebuild()
        showShopToast(String(localized: "guide.shop_success",
                             defaultValue: "Acheté ! Ton indice t'attend en partie."))

        run(SKAction.wait(forDuration: 1.4)) { [weak self] in
            guard let self else { return }
            CoinManager.shared.add(hint.price)
            UserDefaults.standard.set(true, forKey: AppConfig.UserDefaultsKey.hasSeenShopPurchaseGuide)
            AnalyticsService.boosterRefunded(hint.rawValue, amount: hint.price)
            AnalyticsService.shopGuide(step: "refunded")
            HapticManager.light()
            // rebuild() D'ABORD (il fait removeAllChildren), le toast ensuite.
            self.rebuild()
            self.showShopToast(String(localized: "guide.shop_refund",
                                      defaultValue: "Cadeau de bienvenue : tes 30 pièces te sont rendues."))
        }
    }

    /// Message temporaire en bas d'écran. Attaché à la scène et non à
    /// `contentNode` : il doit survivre au défilement, mais **pas** à un
    /// `rebuild()` — toujours l'afficher après celui-ci.
    private func showShopToast(_ text: String) {
        let toast = SKLabelNode(text: text)
        toast.fontName = "AvenirNext-DemiBold"
        toast.fontSize = 19
        toast.fontColor = UIColor(red: 0.35, green: 0.55, blue: 0.40, alpha: 1)
        toast.verticalAlignmentMode = .center
        toast.numberOfLines = 2
        toast.lineBreakMode = .byWordWrapping
        toast.preferredMaxLayoutWidth = usableWidth - 48
        toast.position = CGPoint(x: 0, y: -usableHeight / 2 + 140)
        toast.zPosition = 40
        toast.alpha = 0
        addChild(toast)
        toast.run(SKAction.sequence([
            SKAction.fadeIn(withDuration: 0.2),
            SKAction.wait(forDuration: 2.2),
            SKAction.fadeOut(withDuration: 0.4),
            SKAction.removeFromParent()
        ]))
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

    /// Retour : menu, ou partie en cours si la boutique a été ouverte depuis le jeu.
    private func goBackToMenu() {
        let destination: SKScene
        switch returnDestination {
        case .menu:
            destination = MenuScene(size: size)
        case .game:
            // `resuming: true` : les effets de bord de début de partie
            // (série, paliers, compteur de notifications) ne doivent PAS
            // être rejoués sur un simple aller-retour boutique.
            destination = GameScene(size: size,
                                    savedState: GameState.load(),
                                    resuming: true)
        }
        destination.scaleMode = .aspectFill
        view?.presentScene(destination, transition: SKTransition.fade(withDuration: 0.3))
    }
}
