//
//  MenuScene.swift
//  tenGO
//

import SpriteKit
import StoreKit

class MenuScene: SKScene {

    // Palette du thème actif (valeurs 1–9) pour les bulles décoratives du fond.
    private var bubbleColors: [UIColor] { ThemeManager.shared.active.bubbles }

    private var settingsOverlay: SettingsOverlay?

    /// Libellé du solde de pièces (mis à jour après une récompense).
    private var coinLabel: SKLabelNode?
    private var coinChip: SKNode?
    private var shopGuide: CoachMarkOverlay?
    /// Bouton « regarder une pub pour +10 pièces » + badge de quota restant.
    private var watchAdButton: SKNode?
    private var watchAdBadge: SKLabelNode?
    private static let rewardedCoins = 10
    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        setupBackground()
        setupUI()
        // Le bouton « regarder une pub » vient d'apparaître : c'est ici que la
        // récompensée a une chance d'être vue, donc ici qu'on la précharge.
        RewardedAdManager.shared.preloadIfNeeded()
        maybeShowShopPurchaseGuide()
    }

    // MARK: - Guide d'achat boutique (one-shot)

    /// Premier moment où le solde couvre l'article ciblé (lot d'indices) :
    /// coach-mark sur le bouton Boutique. Le flag est posé dès l'affichage —
    /// le guide ne se représente jamais, même s'il est ignoré.
    /// Au-delà de ce nombre de refus, on cesse définitivement de proposer le guide.
    private static let shopGuideMaxSnooze = 3

    private func maybeShowShopPurchaseGuide() {
        let defaults = UserDefaults.standard
        guard defaults.bool(forKey: AppConfig.UserDefaultsKey.hasSeenTutorial),
              !defaults.bool(forKey: AppConfig.UserDefaultsKey.hasSeenShopPurchaseGuide),
              // Seuil = prix UNITAIRE (30) et non plus celui du lot (120) :
              // 3 à 6 parties au lieu de 12 à 24.
              CoinManager.shared.balance >= Booster.hint.price,
              // La boutique est devenue un onglet : le guide pointe l'onglet.
              let target = childNode(withName: "//tab_shop") else { return }

        // ⚠️ Le flag n'est PAS posé ici : il l'est à la réussite de l'achat.
        // L'ancienne version le posait dès l'affichage — guide ignoré une fois,
        // guide perdu à jamais. On compte plutôt les refus.
        if defaults.integer(forKey: AppConfig.UserDefaultsKey.shopGuideSnooze) >= Self.shopGuideMaxSnooze {
            defaults.set(true, forKey: AppConfig.UserDefaultsKey.hasSeenShopPurchaseGuide)
            AnalyticsService.shopGuide(step: "gaveup")
            return
        }

        run(SKAction.wait(forDuration: 0.45)) { [weak self] in
            guard let self, self.shopGuide == nil else { return }
            self.shopGuide = CoachMarkOverlay.present(
                in: self, over: target,
                text: String(localized: "guide.shop_menu",
                             defaultValue: "Tu as assez de pièces pour ton premier booster ! Ouvre la boutique."))
            AnalyticsService.shopGuide(step: "shown")
        }
    }

    private func presentSettings() {
        if settingsOverlay?.parent != nil { return }
        let presenter = view?.window?.rootViewController
        let overlay = SettingsOverlay(sceneSize: size, presenter: presenter)
        overlay.onAction = { [weak self] action in
            guard let self = self else { return }
            switch action {
            case .replayTutorial:
                self.navigateToTutorial()
            }
        }
        overlay.present(in: self)
        settingsOverlay = overlay
    }

    // MARK: - Fond animé

    private func setupBackground() {
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
    }

    // MARK: - Setup UI

    /// Largeur des cartes de l'accueil, calée sur la largeur réellement visible.
    private var cardWidth: CGFloat = 340

    private func setupUI() {
        guard let v = view else { return }
        let theme = ThemeManager.shared.active

        // Géométrie réellement visible (aspectFill rogne la largeur).
        let scale = max(v.bounds.width / size.width, v.bounds.height / size.height)
        let usableW = v.bounds.width / scale
        let halfH = v.bounds.height / scale / 2
        // Marge haute minimale garantie : safeAreaInsets peut être nul au lancement
        // (encoche/dynamic island) → on plancher à ~47pt pour ne pas remonter dans l'encoche.
        let topInset = max(v.safeAreaInsets.top, 47) / scale
        let bottomInset = max(v.safeAreaInsets.bottom, 20) / scale
        let topRowY = halfH - topInset - 26
        let edgeX = usableW / 2 - 58
        cardWidth = min(usableW - 48, 600)

        // Barre du haut : niveau à gauche, pièces et réglages à droite. Le
        // trophée a quitté ce coin — le classement vit dans l'onglet Progression.
        addIconButton(systemName: "gearshape.fill", name: "parametres", at: CGPoint(x: edgeX, y: topRowY))
        addCoinChip(rightEdgeX: edgeX - 36, atY: topRowY)
        addLevelChip(leftEdgeX: -edgeX + 36, atY: topRowY)

        addLogo(atY: topRowY - 145)

        let tagline = SKLabelNode(text: String(localized: "menu.tagline"))
        tagline.fontName = "AvenirNext-Light"
        tagline.fontSize = 22
        tagline.fontColor = theme.logo.withAlphaComponent(0.6)
        tagline.verticalAlignmentMode = .center
        tagline.position = CGPoint(x: 0, y: topRowY - 203)
        addChild(tagline)

        // Barre d'onglets : les destinations secondaires (succès, missions,
        // classement, boutique, duel) y sont rangées, l'accueil ne porte plus
        // que ce qui se joue tout de suite.
        let tabBar = TabBar.make(width: usableW, selected: .play)
        tabBar.position = CGPoint(x: 0, y: -halfH + bottomInset + TabBar.height / 2)
        addChild(tabBar)

        // Les blocs d'action occupent la bande entre le slogan et les onglets.
        // Hauteurs fixes, espacement calculé : « Reprendre » peut manquer sans
        // laisser un trou.
        let hasSave = GameState.exists
        let heroH: CGFloat = 140, resumeH: CGFloat = 80, dailyH: CGFloat = 250, tilesH: CGFloat = 190
        var blocks: [CGFloat] = [heroH]
        if hasSave { blocks.append(resumeH) }
        blocks.append(contentsOf: [dailyH, tilesH])

        let bandTop = topRowY - 245
        let bandBottom = -halfH + bottomInset + TabBar.height + 18
        let available = bandTop - bandBottom
        let blocksH = blocks.reduce(0, +)
        let gap = min(80, max(24, (available - blocksH) / CGFloat(blocks.count - 1)))
        var cursor = bandTop - max(0, (available - (blocksH + gap * CGFloat(blocks.count - 1))) / 2)
        func place(_ height: CGFloat) -> CGFloat {
            let center = cursor - height / 2
            cursor -= height + gap
            return center
        }

        addHeroButton(atY: place(heroH), height: heroH, accent: theme.color(forValue: 4))
        if hasSave { addResumeButton(atY: place(resumeH), height: resumeH) }
        addDailyCard(atY: place(dailyH), height: dailyH)
        addModeTiles(atY: place(tilesH), height: tilesH)

        // Bouton « pub récompensée », sous le solde qu'il alimente. L'achat
        // « sans pub » vit dans la Boutique (section Pièces).
        addWatchAdButton(at: CGPoint(x: edgeX - 52, y: topRowY - 82))
    }

    // MARK: - Blocs de l'accueil

    /// Action primaire : une pilule haute, icône + libellé, impossible à rater.
    private func addHeroButton(atY y: CGFloat, height: CGFloat, accent: UIColor) {
        let node = SKNode()
        node.name = "newGame"
        node.position = CGPoint(x: 0, y: y)
        addChild(node)

        let shadow = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: height / 2)
        shadow.fillColor = UIColor(white: 0, alpha: 0.09)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: -5)
        shadow.zPosition = -1
        node.addChild(shadow)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: height / 2)
        bg.fillColor = accent
        bg.strokeColor = UIColor(white: 0.68, alpha: 0.30)
        bg.lineWidth = 1
        node.addChild(bg)

        let ink = accent.readableInk()
        let label = SKLabelNode(text: String(localized: "menu.play"))
        label.fontName = "AvenirNext-Bold"
        label.fontSize = 34
        label.fontColor = ink
        label.verticalAlignmentMode = .center

        // Icône et libellé centrés comme un seul bloc.
        let iconSize: CGFloat = 44, inner: CGFloat = 18
        let contentW = iconSize + inner + label.frame.width
        let icon = VectorIcon.play.node(size: iconSize, color: ink)
        icon.position = CGPoint(x: -contentW / 2 + iconSize / 2, y: 0)
        label.horizontalAlignmentMode = .left
        label.position = CGPoint(x: -contentW / 2 + iconSize + inner, y: 0)
        node.addChild(icon)
        node.addChild(label)
    }

    /// Reprise de partie : présente seulement quand elle a un sens, et
    /// volontairement discrète pour ne pas concurrencer « Jouer ».
    private func addResumeButton(atY y: CGFloat, height: CGFloat) {
        let theme = ThemeManager.shared.active
        let node = SKNode()
        node.name = "continuer"
        node.position = CGPoint(x: 0, y: y)
        addChild(node)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: height / 2)
        bg.fillColor = theme.logo.withAlphaComponent(0.07)
        bg.strokeColor = theme.logo.withAlphaComponent(0.26)
        bg.lineWidth = 1
        node.addChild(bg)

        let label = SKLabelNode(text: String(localized: "menu.continue"))
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 22
        label.fontColor = theme.logo
        label.verticalAlignmentMode = .center
        node.addChild(label)
    }

    /// Carte du Défi du jour : elle porte aussi la série, qui est la vraie
    /// raison de revenir chaque jour.
    private func addDailyCard(atY y: CGFloat, height: CGFloat) {
        let theme = ThemeManager.shared.active
        let done = DailyChallenge.isCompletedToday()
        let accent = done ? UIColor(red: 0.90, green: 0.90, blue: 0.89, alpha: 1) : theme.color(forValue: 6)
        let ink = accent.readableInk()
        let half = height / 2
        let leftX = -cardWidth / 2 + 30

        let card = SKNode()
        card.name = "dailyChallenge"
        card.position = CGPoint(x: 0, y: y)
        card.alpha = done ? 0.62 : 1
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 30)
        bg.fillColor = accent
        bg.strokeColor = UIColor(white: 0.68, alpha: 0.28)
        bg.lineWidth = 1
        card.addChild(bg)

        let title = SKLabelNode(text: String(localized: "menu.daily"))
        title.fontName = "AvenirNext-Bold"
        title.fontSize = 27
        title.fontColor = ink
        title.horizontalAlignmentMode = .left
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: leftX, y: half - 41)
        card.addChild(title)

        // Série : flamme + jours, puis les boucliers restants s'il y en a.
        let streak = StreakManager.shared.current
        let flame = VectorIcon.flame.node(size: 36, color: ink)
        flame.position = CGPoint(x: leftX + 18, y: 22)
        card.addChild(flame)

        let days = SKLabelNode(text: "\(streak)")
        days.fontName = "AvenirNext-Heavy"
        days.fontSize = 30
        days.fontColor = ink
        days.horizontalAlignmentMode = .left
        days.verticalAlignmentMode = .center
        days.position = CGPoint(x: leftX + 44, y: 22)
        card.addChild(days)

        let shields = StreakManager.shared.shieldCount
        if shields > 0 {
            let x = leftX + 54 + days.frame.width + 26
            let shield = VectorIcon.shield.node(size: 30, color: ink.withAlphaComponent(0.85))
            shield.position = CGPoint(x: x, y: 22)
            card.addChild(shield)

            let count = SKLabelNode(text: "\(shields)")
            count.fontName = "AvenirNext-DemiBold"
            count.fontSize = 20
            count.fontColor = ink.withAlphaComponent(0.85)
            count.horizontalAlignmentMode = .left
            count.verticalAlignmentMode = .center
            count.position = CGPoint(x: x + 21, y: 22)
            card.addChild(count)
        }

        if let nextDay = StreakManager.shared.nextMilestone {
            let next = SKLabelNode(text: String(format: String(localized: "streak.next_reward"), nextDay))
            next.fontName = "AvenirNext-Medium"
            next.fontSize = 15
            next.fontColor = ink.withAlphaComponent(0.8)
            next.horizontalAlignmentMode = .left
            next.verticalAlignmentMode = .center
            next.position = CGPoint(x: leftX, y: -30)
            card.addChild(next)
        }

        // Bandeau d'état en bas de carte : l'affordance est explicite plutôt
        // que devinée à la couleur.
        let pillH: CGFloat = 46
        let pill = SKShapeNode(rectOf: CGSize(width: cardWidth - 56, height: pillH), cornerRadius: pillH / 2)
        pill.fillColor = ink.withAlphaComponent(done ? 0.10 : 0.16)
        pill.strokeColor = .clear
        pill.position = CGPoint(x: 0, y: -half + 40)
        card.addChild(pill)

        let cta = SKLabelNode(text: String(localized: done ? "menu.daily_done" : "menu.play"))
        cta.fontName = "AvenirNext-DemiBold"
        cta.fontSize = 18
        cta.fontColor = ink
        cta.verticalAlignmentMode = .center
        cta.position = CGPoint(x: 0, y: -half + 40)
        card.addChild(cta)
    }

    /// Les trois autres façons de jouer, sur une seule ligne : elles se
    /// comparent d'un regard au lieu de s'empiler.
    private func addModeTiles(atY y: CGFloat, height: CGFloat) {
        let theme = ThemeManager.shared.active
        let levels = PuzzleWorld.levels(inWorld: 1)
        let solved = levels.filter { PuzzleProgress.shared.stars(world: $0.world, index: $0.index) > 0 }.count
        let total = levels.count

        let tiles: [(name: String, icon: VectorIcon, title: String, subtitle: String, accent: UIColor)] = [
            ("rush", .rush, String(localized: "menu.rush"), String(localized: "menu.rush_sub"), theme.color(forValue: 2)),
            ("duel", .duel, String(localized: "menu.duel"), String(localized: "menu.duel_sub"), theme.color(forValue: 1)),
            ("puzzles", .puzzle, String(localized: "menu.puzzles"), "\(solved)/\(total)", theme.color(forValue: 9)),
        ]

        let inner: CGFloat = 14
        let tileW = (cardWidth - inner * CGFloat(tiles.count - 1)) / CGFloat(tiles.count)
        for (index, tile) in tiles.enumerated() {
            let node = SKNode()
            node.name = tile.name
            node.position = CGPoint(x: -cardWidth / 2 + tileW / 2 + CGFloat(index) * (tileW + inner), y: y)
            addChild(node)

            let bg = SKShapeNode(rectOf: CGSize(width: tileW, height: height), cornerRadius: 26)
            bg.fillColor = tile.accent
            bg.strokeColor = UIColor(white: 0.68, alpha: 0.28)
            bg.lineWidth = 1
            node.addChild(bg)

            let ink = tile.accent.readableInk()
            let icon = tile.icon.node(size: 64, color: ink)
            icon.position = CGPoint(x: 0, y: 30)
            node.addChild(icon)

            let title = SKLabelNode(text: tile.title)
            title.fontName = "AvenirNext-DemiBold"
            title.fontSize = 19
            title.fontColor = ink
            title.verticalAlignmentMode = .center
            title.position = CGPoint(x: 0, y: -28)
            node.addChild(title)

            let subtitle = SKLabelNode(text: tile.subtitle)
            subtitle.fontName = "AvenirNext-Medium"
            subtitle.fontSize = 13
            subtitle.fontColor = ink.withAlphaComponent(0.75)
            subtitle.verticalAlignmentMode = .center
            subtitle.position = CGPoint(x: 0, y: -58)
            node.addChild(subtitle)
        }
    }

    /// Bouton-icône rond (SF Symbol) pour les coins (classement, paramètres).
    private func addIconButton(systemName: String, name: String, at position: CGPoint) {
        let theme = ThemeManager.shared.active
        let node = SKNode()
        node.name = name
        node.position = position
        node.zPosition = 5

        let bg = SKShapeNode(circleOfRadius: 24)
        bg.fillColor = theme.logo.withAlphaComponent(0.10)
        bg.strokeColor = theme.logo.withAlphaComponent(0.22)
        bg.lineWidth = 1
        node.addChild(bg)

        let config = UIImage.SymbolConfiguration(pointSize: 21, weight: .medium)
        if let img = UIImage(systemName: systemName, withConfiguration: config)?
            .withTintColor(theme.logo, renderingMode: .alwaysOriginal) {
            let sprite = SKSpriteNode(texture: SKTexture(image: img))
            let maxDim = max(img.size.width, img.size.height)
            let target: CGFloat = 24
            sprite.size = CGSize(width: img.size.width / maxDim * target,
                                 height: img.size.height / maxDim * target)
            node.addChild(sprite)
        }
        addChild(node)
    }


    /// Solde de pièces dépensables (monnaie de la boutique).
    /// Pastille dorée + pièce vectorielle.
    /// Chip de solde ancré par son bord droit (la largeur varie avec le solde).
    private func addCoinChip(rightEdgeX: CGFloat, atY y: CGFloat) {
        let container = SKNode()

        let coinR: CGFloat = 9
        let coin = CoinIcon.make(radius: coinR)
        coin.zPosition = 1

        let number = SKLabelNode(text: "\(CoinManager.shared.balance)")
        number.fontName = "AvenirNext-Medium"
        number.fontSize = 19
        number.fontColor = UIColor(white: 0.35, alpha: 1)
        number.verticalAlignmentMode = .center
        number.horizontalAlignmentMode = .left
        number.zPosition = 1

        let gap: CGFloat = 7
        let contentW = coinR * 2 + gap + number.frame.width
        let height: CGFloat = 40
        let width = max(contentW + 40, 70)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        bg.fillColor = UIColor(red: 0.98, green: 0.92, blue: 0.74, alpha: 0.95) // doux doré
        bg.strokeColor = UIColor(red: 0.84, green: 0.64, blue: 0.28, alpha: 0.35)
        bg.lineWidth = 1
        bg.zPosition = 0

        container.position = CGPoint(x: rightEdgeX - width / 2, y: y)

        let startX = -contentW / 2
        coin.position = CGPoint(x: startX + coinR, y: 0)
        number.position = CGPoint(x: startX + coinR * 2 + gap, y: 0)

        container.addChild(bg)
        container.addChild(coin)
        container.addChild(number)
        addChild(container)
        coinLabel = number
        coinChip = container
    }

    /// Niveau du joueur (système XP/Levels). Pastille discrète, tap → détail
    /// de progression. Chip ancré par son bord gauche (symétrique du chip pièces).
    private func addLevelChip(leftEdgeX: CGFloat, atY y: CGFloat) {
        let container = SKNode()
        container.name = "levelChip"

        let label = SKLabelNode(text: String(format: String(localized: "level.chip.label"), LevelManager.shared.level))
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 19
        label.fontColor = UIColor(white: 0.35, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .center
        label.name = "levelChip"

        let height: CGFloat = 40
        let width = max(label.frame.width + 40, 70)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        bg.fillColor = ThemeManager.shared.active.accent.withAlphaComponent(0.14)
        bg.strokeColor = ThemeManager.shared.active.accent.withAlphaComponent(0.30)
        bg.lineWidth = 1
        bg.name = "levelChip"

        container.position = CGPoint(x: leftEdgeX + width / 2, y: y)
        container.addChild(bg)
        container.addChild(label)
        addChild(container)
    }

    /// Bouton rond « regarder une pub → +10 pièces », placé sous le solde
    /// qu'il alimente : le lien entre les deux se lit sans explication.
    private func addWatchAdButton(at position: CGPoint) {
        let node = SKNode()
        node.name = "watchAd"
        node.position = position
        node.zPosition = 6

        let r: CGFloat = 38
        let bg = SKShapeNode(circleOfRadius: r)
        bg.fillColor = UIColor(red: 0.98, green: 0.92, blue: 0.74, alpha: 0.97)
        bg.strokeColor = UIColor(red: 0.84, green: 0.64, blue: 0.28, alpha: 0.45)
        bg.lineWidth = 1.5
        node.addChild(bg)

        // Triangle « play » (affordance vidéo), partie haute.
        let triPath = CGMutablePath()
        triPath.move(to: CGPoint(x: -7, y: 9))
        triPath.addLine(to: CGPoint(x: -7, y: -9))
        triPath.addLine(to: CGPoint(x: 10, y: 0))
        triPath.closeSubpath()
        let tri = SKShapeNode(path: triPath)
        tri.fillColor = UIColor(white: 0.34, alpha: 1)
        tri.strokeColor = .clear
        tri.position = CGPoint(x: 1, y: 11)
        node.addChild(tri)

        // « +10 » avec pièce, partie basse.
        let coin = CoinIcon.make(radius: 7)
        let label = SKLabelNode(text: "+\(MenuScene.rewardedCoins)")
        label.fontName = "AvenirNext-Bold"
        label.fontSize = 15
        label.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        let gap: CGFloat = 4
        let contentW = 14 + gap + label.frame.width
        coin.position = CGPoint(x: -contentW / 2 + 7, y: -15)
        label.position = CGPoint(x: -contentW / 2 + 14 + gap, y: -15)
        node.addChild(coin)
        node.addChild(label)

        // Badge de quota restant (haut-droite).
        let badgeBg = SKShapeNode(circleOfRadius: 11)
        badgeBg.fillColor = UIColor(white: 1.0, alpha: 0.96)
        badgeBg.strokeColor = UIColor(red: 0.84, green: 0.64, blue: 0.28, alpha: 0.55)
        badgeBg.lineWidth = 1
        badgeBg.position = CGPoint(x: 27, y: 27)
        badgeBg.zPosition = 1
        node.addChild(badgeBg)

        let badge = SKLabelNode(text: "")
        badge.fontName = "AvenirNext-Bold"
        badge.fontSize = 13
        badge.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
        badge.verticalAlignmentMode = .center
        badge.horizontalAlignmentMode = .center
        badge.position = CGPoint(x: 27, y: 27)
        badge.zPosition = 2
        node.addChild(badge)
        watchAdBadge = badge

        addChild(node)
        watchAdButton = node
        refreshWatchAdButton()
    }

    /// Met à jour le badge (pubs restantes aujourd'hui) et grise le bouton si épuisé.
    private func refreshWatchAdButton() {
        let remaining = RewardedAdManager.shared.remainingToday
        watchAdBadge?.text = "\(remaining)"
        watchAdButton?.alpha = remaining > 0 ? 1.0 : 0.45
    }

    private func addLogo(atY y: CGFloat) {
        // "TEN" à gauche
        let ten = SKLabelNode(text: "TEN")
        ten.fontName = "AvenirNext-Heavy"
        ten.fontSize = 72
        ten.fontColor = ThemeManager.shared.active.logo
        ten.verticalAlignmentMode = .center
        ten.horizontalAlignmentMode = .right
        ten.position = CGPoint(x: -18, y: y)
        addChild(ten)

        // Bulle colorée au centre (rose = index 6)
        let dotRadius: CGFloat = 14
        let dot = SKShapeNode(circleOfRadius: dotRadius)
        dot.fillColor = bubbleColors[Int.random(in: 0..<bubbleColors.count)]
        dot.strokeColor = .clear
        dot.position = CGPoint(x: 0, y: y + 4)
        dot.zPosition = 1
        addChild(dot)

        // "GO" à droite
        let go = SKLabelNode(text: "GO")
        go.fontName = "AvenirNext-Heavy"
        go.fontSize = 72
        go.fontColor = ThemeManager.shared.active.logo
        go.verticalAlignmentMode = .center
        go.horizontalAlignmentMode = .left
        go.position = CGPoint(x: 18, y: y)
        addChild(go)

        // Légère animation de pulsation sur la bulle
        dot.run(SKAction.repeatForever(SKAction.sequence([
            SKAction.scale(to: 1.15, duration: 1.4),
            SKAction.scale(to: 0.90, duration: 1.4)
        ])))
    }

    @discardableResult
    private func addMenuButton(text: String, name: String, width: CGFloat, height: CGFloat,
                               at position: CGPoint, accent: UIColor? = nil,
                               fontSize: CGFloat = 20, bold: Bool = false) -> SKNode {
        let theme = ThemeManager.shared.active
        let node = SKNode()
        node.name = name
        node.position = position

        // Ombre simulée — nœud identique décalé, semi-transparent
        let shadow = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        shadow.fillColor = UIColor(white: 0.0, alpha: 0.08)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 1, y: -4)
        shadow.zPosition = -1
        node.addChild(shadow)

        // Accentué → couleur du thème ; secondaire → pastille theme-aware (meilleur contraste).
        let fillColor = accent ?? theme.logo.withAlphaComponent(0.08)
        let labelColor = accent != nil ? accent!.readableInk() : theme.logo
        let strokeColor = accent != nil ? UIColor(white: 0.68, alpha: 0.30) : theme.logo.withAlphaComponent(0.28)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        bg.fillColor = fillColor
        bg.strokeColor = strokeColor
        bg.lineWidth = 1
        node.addChild(bg)

        let label = SKLabelNode(text: text)
        label.fontName = bold ? "AvenirNext-Bold" : "AvenirNext-Medium"
        label.fontSize = fontSize
        label.fontColor = labelColor
        label.verticalAlignmentMode = .center
        node.addChild(label)

        addChild(node)
        return node
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        // Guide d'achat : la cible ouvre la boutique en mode guidé, ailleurs on
        // ferme et on compte le refus (au 3e, on cesse de proposer).
        if let guide = shopGuide, guide.parent != nil {
            let result = guide.handleTouch(at: point)
            shopGuide = nil
            if result == .target {
                AnalyticsService.shopGuide(step: "opened")
                if let tab = childNode(withName: "//tab_shop") { animateTap(tab) }
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToBoutique(guided: true)
                }
            } else {
                let defaults = UserDefaults.standard
                let snoozed = defaults.integer(forKey: AppConfig.UserDefaultsKey.shopGuideSnooze) + 1
                defaults.set(snoozed, forKey: AppConfig.UserDefaultsKey.shopGuideSnooze)
                AnalyticsService.shopGuide(step: "later")
            }
            return
        }

        // Overlay prioritaire si présent
        if let overlay = settingsOverlay, overlay.parent != nil {
            overlay.handleTouch(at: point)
            if overlay.parent == nil { settingsOverlay = nil }
            return
        }
        for node in nodes(at: point) {
            guard let name = node.parent?.name ?? node.name else { continue }
            switch name {
            case "parametres":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) { self.presentSettings() }
                return
            case "newGame":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    if !UserDefaults.standard.bool(forKey: AppConfig.UserDefaultsKey.hasSeenTutorial) {
                        self.navigateToTutorial()
                    } else {
                        GameState.clear()
                        self.navigateToGame(savedState: nil)
                    }
                }
                return
            case "dailyChallenge":
                // Déjà terminé aujourd'hui → grisé, non jouable jusqu'au lendemain.
                if DailyChallenge.isCompletedToday() { return }
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToDailyChallenge()
                }
                return
            case "continuer":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToGame(savedState: GameState.load())
                }
                return
            case "puzzles":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToPuzzles()
                }
                return
            case "duel":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    let scene = DuelScene(size: self.size)
                    scene.scaleMode = .aspectFill
                    self.view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
                }
                return
            case "rush":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToRush()
                }
                return
            case "watchAd":
                animateTap(node.parent ?? node)
                presentRewardedAd()
                return
            case "levelChip":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToProfile()
                }
                return
            default: break
            }
        }

        // Onglets en dernier : un bloc d'action posé au-dessus garde la main.
        if let tab = TabBar.tab(at: point, in: self), tab != .play {
            TabBar.present(tab, from: self)
        }
    }

    private func animateTap(_ node: SKNode) {
        node.run(SKAction.sequence([
            SKAction.scale(to: 0.93, duration: 0.07),
            SKAction.scale(to: 1.0,  duration: 0.12)
        ]))
    }

    // MARK: - Pub récompensée

    private func presentRewardedAd() {
        guard let vc = view?.window?.rootViewController else { return }
        RewardedAdManager.shared.show(from: vc, onReward: { [weak self] in
            guard let self = self else { return }
            CoinManager.shared.add(MenuScene.rewardedCoins)
            self.refreshCoins()
            self.refreshWatchAdButton()
            self.showRewardFeedback()
        }, onUnavailable: { [weak self] in
            self?.showUnavailableFeedback()
        }, onLimitReached: { [weak self] in
            // Quota du jour atteint : feedback + bouton grisé.
            self?.refreshWatchAdButton()
            self?.showUnavailableFeedback()
        })
    }

    private func refreshCoins() {
        coinLabel?.text = "\(CoinManager.shared.balance)"
        coinChip?.run(SKAction.sequence([
            SKAction.scale(to: 1.12, duration: 0.08),
            SKAction.scale(to: 1.0, duration: 0.12)
        ]))
    }

    private func showRewardFeedback() {
        guard let chip = coinChip else { return }
        let popup = SKLabelNode(text: "+\(MenuScene.rewardedCoins)")
        popup.fontName = "AvenirNext-Heavy"
        popup.fontSize = 26
        popup.fontColor = UIColor(red: 0.85, green: 0.60, blue: 0.15, alpha: 1)
        popup.verticalAlignmentMode = .center
        popup.position = CGPoint(x: chip.position.x, y: chip.position.y - 30)
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

    private func showUnavailableFeedback() {
        watchAdButton?.run(SKAction.sequence([
            SKAction.moveBy(x: -6, y: 0, duration: 0.05),
            SKAction.moveBy(x: 12, y: 0, duration: 0.05),
            SKAction.moveBy(x: -6, y: 0, duration: 0.05)
        ]))
    }

    // MARK: - Navigation

    private func navigateToGame(savedState: GameState?) {
        let scene = GameScene(size: size, savedState: savedState)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.35))
    }

    private func navigateToDailyChallenge() {
        let today = DailyChallenge.make()
        let scene = GameScene(size: size, daily: today)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.35))
    }

    private func navigateToBoutique(guided: Bool = false) {
        let scene = BoutiqueScene(size: size)
        scene.guidedPurchase = guided
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.3))
    }

    private func navigateToRush() {
        let scene = GameScene(size: size, startRush: true)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.35))
    }

    private func navigateToPuzzles() {
        // Un seul monde livré : on ouvre directement sa liste de niveaux
        // plutôt qu'un écran de sélection de monde à une seule entrée.
        let scene = PuzzleLevelsScene(size: size, world: 1)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
    }

    private func navigateToProfile() {
        let scene = ProfileScene(size: size)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
    }

    private func navigateToTutorial() {
        let scene = TutorialScene(size: size)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
    }
}
