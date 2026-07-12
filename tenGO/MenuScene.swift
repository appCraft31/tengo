//
//  MenuScene.swift
//  tenGO
//

import SpriteKit

class MenuScene: SKScene {

    // Palette du thème actif (valeurs 1–9) pour les bulles décoratives du fond.
    private var bubbleColors: [UIColor] { ThemeManager.shared.active.bubbles }

    private var settingsOverlay: SettingsOverlay?

    /// Libellé du solde de pièces (mis à jour après une récompense).
    private var coinLabel: SKLabelNode?
    private var coinChip: SKNode?
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
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": true])
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

    private func setupUI() {
        let centerY = size.height / 2

        // Géométrie réellement visible (aspectFill rogne la largeur).
        var topRowY = centerY * 0.78
        var edgeX = size.width * 0.30
        if let v = view {
            let scale = max(v.bounds.width / size.width, v.bounds.height / size.height)
            // Marge haute minimale garantie : safeAreaInsets peut être nul au lancement
            // (encoche/dynamic island) → on plancher à ~47pt pour ne pas remonter dans l'encoche.
            let topInset = max(v.safeAreaInsets.top, 47) / scale
            topRowY = (v.bounds.height / scale) / 2 - topInset - 26
            edgeX = (v.bounds.width / scale) / 2 - 58
        }

        // Logo + slogan — bloc descendu pour centrer l'ensemble logo+boutons.
        addLogo(atY: centerY * 0.22)

        let tagline = SKLabelNode(text: String(localized: "menu.tagline"))
        tagline.fontName = "AvenirNext-Light"
        tagline.fontSize = 22
        tagline.fontColor = ThemeManager.shared.active.logo.withAlphaComponent(0.6)
        tagline.verticalAlignmentMode = .center
        tagline.position = CGPoint(x: 0, y: centerY * 0.22 - 56)
        addChild(tagline)

        // Barre du haut : trophée (gauche) et engrenage (droite) dans les coins ;
        // le solde de pièces est centré juste en dessous (évite la dynamic island).
        addIconButton(systemName: "trophy.fill", name: "classement", at: CGPoint(x: -edgeX, y: topRowY))
        addIconButton(systemName: "gearshape.fill", name: "parametres", at: CGPoint(x: edgeX, y: topRowY))
        addCoinChip(atY: topRowY - 70)

        // Pile de boutons hiérarchisée, centrée
        let theme = ThemeManager.shared.active
        let gap: CGFloat = 26
        var topCursor: CGFloat = centerY * 0.06
        func stack(_ h: CGFloat) -> CGFloat {
            let c = topCursor - h / 2
            topCursor -= (h + gap)
            return c
        }

        // Jouer — action primaire (grand bouton accentué)
        addMenuButton(text: String(localized: "menu.new_game"), name: "newGame",
                      width: 290, height: 72, at: CGPoint(x: 0, y: stack(72)),
                      accent: theme.accent, fontSize: 25, bold: true)

        // Continuer — sous Jouer si une partie est en cours
        if GameState.exists {
            addMenuButton(text: String(localized: "menu.continue"), name: "continuer",
                          width: 210, height: 52, at: CGPoint(x: 0, y: stack(52)), fontSize: 18)
        }

        // Défi du jour — secondaire
        let dailyDone = DailyChallenge.isCompletedToday()
        let dailyButton = addMenuButton(
            text: String(localized: "menu.daily", defaultValue: "Défi du jour"),
            name: "dailyChallenge",
            width: 240, height: 60, at: CGPoint(x: 0, y: stack(60)),
            accent: dailyDone
                ? UIColor(red: 0.90, green: 0.90, blue: 0.89, alpha: 1)
                : UIColor(red: 0.86, green: 0.82, blue: 0.97, alpha: 1),
            fontSize: 21)
        if dailyDone {
            dailyButton.alpha = 0.5
            let check = SKLabelNode(text: "✓")
            check.fontName = "AvenirNext-Bold"
            check.fontSize = 22
            check.fontColor = UIColor(red: 0.45, green: 0.55, blue: 0.48, alpha: 1)
            check.verticalAlignmentMode = .center
            check.position = CGPoint(x: 98, y: 0)
            dailyButton.addChild(check)
        }

        // Boutique — tertiaire
        addMenuButton(text: String(localized: "menu.shop", defaultValue: "Boutique"), name: "boutique",
                      width: 200, height: 52, at: CGPoint(x: 0, y: stack(52)), fontSize: 18)

        // Bouton « pub récompensée » (gauche, 3/4 de la hauteur).
        addWatchAdButton()
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

    /// Couleur de texte lisible sur un fond (clair → foncé, sombre → blanc).
    private func contrastingText(on color: UIColor) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        color.getRed(&r, green: &g, blue: &b, alpha: &a)
        return (0.299 * r + 0.587 * g + 0.114 * b) > 0.6 ? UIColor(white: 0.2, alpha: 1) : .white
    }

    /// Solde de pièces dépensables (monnaie de la boutique).
    /// Pastille dorée + pièce vectorielle.
    private func addCoinChip(atY y: CGFloat) {
        let container = SKNode()
        container.position = CGPoint(x: 0, y: y)

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

    /// Bouton rond « regarder une pub → +10 pièces », à gauche, aux 3/4 de la
    /// hauteur en partant du bas.
    private func addWatchAdButton() {
        guard let v = view else { return }
        let scale = max(v.bounds.width / size.width, v.bounds.height / size.height)
        let visibleH = v.bounds.height / scale
        let visibleW = v.bounds.width / scale
        let y = -visibleH / 2 + visibleH * 0.75
        let x = -visibleW / 2 + 52

        let node = SKNode()
        node.name = "watchAd"
        node.position = CGPoint(x: x, y: y)
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
        let labelColor = accent != nil ? contrastingText(on: accent!) : theme.logo
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
                    if !UserDefaults.standard.bool(forKey: "hasSeenTutorial") {
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
            case "boutique":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToBoutique()
                }
                return
            case "classement":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToLeaderboard()
                }
                return
            case "watchAd":
                animateTap(node.parent ?? node)
                presentRewardedAd()
                return
            default: break
            }
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

    private func navigateToBoutique() {
        let scene = BoutiqueScene(size: size)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.3))
    }

    private func navigateToLeaderboard() {
        let scene = LeaderboardScene(size: size)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
    }

    private func navigateToTutorial() {
        let scene = TutorialScene(size: size)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.28))
    }
}
