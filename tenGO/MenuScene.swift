//
//  MenuScene.swift
//  tenGO
//

import SpriteKit

class MenuScene: SKScene {

    // Palette du thème actif (valeurs 1–9) pour les bulles décoratives du fond.
    private var bubbleColors: [UIColor] { ThemeManager.shared.active.bubbles }

    private var settingsOverlay: SettingsOverlay?

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        setupBackground()
        setupUI()
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

        // Titre — "TEN" + bulle colorée + "GO"
        addLogo(atY: centerY * 0.45)

        let tagline = SKLabelNode(text: String(localized: "menu.tagline"))
        tagline.fontName = "AvenirNext-Light"
        tagline.fontSize = 22
        tagline.fontColor = ThemeManager.shared.active.logo.withAlphaComponent(0.6)
        tagline.verticalAlignmentMode = .center
        tagline.position = CGPoint(x: 0, y: centerY * 0.45 - 62)
        addChild(tagline)

        addCoinChip(atY: centerY * 0.78)

        // Boutons
        let hasSaved = GameState.exists
        var buttonY: CGFloat = 40

        addMenuButton(text: String(localized: "menu.new_game"), name: "newGame",
                      width: 270, height: 62, at: CGPoint(x: 0, y: buttonY),
                      accent: ThemeManager.shared.active.accent)
        buttonY -= 86

        // Défi du jour — grille unique partagée par tous, une seule fois par jour.
        let dailyDone = DailyChallenge.isCompletedToday()
        let dailyButton = addMenuButton(
            text: String(localized: "menu.daily", defaultValue: "Défi du jour"),
            name: "dailyChallenge",
            width: 240, height: 58, at: CGPoint(x: 0, y: buttonY),
            accent: dailyDone
                ? UIColor(red: 0.90, green: 0.90, blue: 0.89, alpha: 1)   // grisé : déjà fait
                : UIColor(red: 0.86, green: 0.82, blue: 0.97, alpha: 1))  // lavande : jouable
        if dailyDone {
            dailyButton.alpha = 0.5   // désactivé jusqu'au lendemain
            let check = SKLabelNode(text: "✓")
            check.fontName = "AvenirNext-Bold"
            check.fontSize = 24
            check.fontColor = UIColor(red: 0.45, green: 0.55, blue: 0.48, alpha: 1)
            check.verticalAlignmentMode = .center
            check.position = CGPoint(x: 98, y: 0)
            dailyButton.addChild(check)
        }
        buttonY -= 82

        addMenuButton(text: String(localized: "menu.shop", defaultValue: "Boutique"), name: "boutique",
                      width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))
        buttonY -= 80

        if hasSaved {
            addMenuButton(text: String(localized: "menu.continue"), name: "continuer",
                          width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))
            buttonY -= 80
        }

        addMenuButton(text: String(localized: "menu.leaderboard"), name: "classement",
                      width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))
        buttonY -= 80

        addMenuButton(text: String(localized: "menu.settings"), name: "parametres",
                      width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))
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
                               at position: CGPoint, accent: UIColor? = nil) -> SKNode {
        let node = SKNode()
        node.name = name
        node.position = position

        let fillColor = accent ?? UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)

        // Ombre simulée — nœud identique décalé, semi-transparent
        let shadow = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        shadow.fillColor = UIColor(white: 0.0, alpha: 0.06)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 1, y: -4)
        shadow.zPosition = -1
        node.addChild(shadow)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: height / 2)
        bg.fillColor = fillColor
        bg.strokeColor = UIColor(white: 0.68, alpha: 0.35)
        bg.lineWidth = 1
        node.addChild(bg)

        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 20
        label.fontColor = UIColor(white: 0.32, alpha: 1)
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
