//
//  MenuScene.swift
//  tenGO
//

import SpriteKit

class MenuScene: SKScene {

    // Couleurs pastel du jeu (valeurs 1–9)
    private let bubbleColors: [UIColor] = [
        UIColor(red: 0.98, green: 0.72, blue: 0.68, alpha: 1), // corail
        UIColor(red: 0.99, green: 0.84, blue: 0.70, alpha: 1), // pêche
        UIColor(red: 0.99, green: 0.95, blue: 0.72, alpha: 1), // jaune beurre
        UIColor(red: 0.78, green: 0.94, blue: 0.82, alpha: 1), // menthe
        UIColor(red: 0.72, green: 0.88, blue: 0.98, alpha: 1), // ciel
        UIColor(red: 0.82, green: 0.78, blue: 0.97, alpha: 1), // lavande
        UIColor(red: 0.98, green: 0.78, blue: 0.88, alpha: 1), // rose
        UIColor(red: 0.80, green: 0.91, blue: 0.80, alpha: 1), // sauge
        UIColor(red: 0.76, green: 0.82, blue: 0.97, alpha: 1), // pervenche
    ]

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        setupBackground()
        setupUI()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": true])
    }

    // MARK: - Fond animé

    private func setupBackground() {
        let configs: [(radius: CGFloat, x: CGFloat, y: CGFloat, colorIdx: Int, duration: Double)] = [
            (88,  -280,  480, 0, 7.2),
            (62,   260,  350, 4, 9.5),
            (110, -180, -150, 6, 11.0),
            (74,   310, -320, 1, 8.3),
            (96,   -60,  560, 3, 10.1),
            (54,   200,  -500, 7, 6.8),
            (80,  -320, -480, 2, 12.4),
            (68,    80,  200, 5, 9.0),
            (50,  -200,  -30, 8, 7.6),
        ]

        for cfg in configs {
            let bubble = SKShapeNode(circleOfRadius: cfg.radius)
            bubble.fillColor = bubbleColors[cfg.colorIdx].withAlphaComponent(0.18)
            bubble.strokeColor = .clear
            bubble.position = CGPoint(x: cfg.x, y: cfg.y)
            bubble.zPosition = -1
            addChild(bubble)

            let floatUp = SKAction.moveBy(x: 0, y: 18, duration: cfg.duration)
            let floatDown = SKAction.moveBy(x: 0, y: -18, duration: cfg.duration)
            floatUp.timingMode = .easeInEaseOut
            floatDown.timingMode = .easeInEaseOut
            bubble.run(SKAction.repeatForever(SKAction.sequence([floatUp, floatDown])))
        }
    }

    // MARK: - Setup UI

    private func setupUI() {
        let centerY = size.height / 2

        // Titre — "TEN" + bulle colorée + "GO"
        addLogo(atY: centerY * 0.45)

        let tagline = SKLabelNode(text: "Relie. Additionne. Libère.")
        tagline.fontName = "AvenirNext-Light"
        tagline.fontSize = 22
        tagline.fontColor = UIColor(white: 0.48, alpha: 1)
        tagline.verticalAlignmentMode = .center
        tagline.position = CGPoint(x: 0, y: centerY * 0.45 - 62)
        addChild(tagline)

        // Boutons
        let hasSaved = GameState.exists
        var buttonY: CGFloat = 40

        addMenuButton(text: "Nouvelle partie", name: "newGame",
                      width: 270, height: 62, at: CGPoint(x: 0, y: buttonY),
                      accent: UIColor(red: 0.82, green: 0.95, blue: 0.88, alpha: 1))
        buttonY -= 86

        if hasSaved {
            addMenuButton(text: "Continuer", name: "continuer",
                          width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))
            buttonY -= 80
        }

        addMenuButton(text: "Classement", name: "classement",
                      width: 210, height: 56, at: CGPoint(x: 0, y: buttonY))

    }

    private func addLogo(atY y: CGFloat) {
        // "TEN" à gauche
        let ten = SKLabelNode(text: "TEN")
        ten.fontName = "AvenirNext-Heavy"
        ten.fontSize = 72
        ten.fontColor = UIColor(white: 0.28, alpha: 1)
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
        go.fontColor = UIColor(white: 0.28, alpha: 1)
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

    private func addMenuButton(text: String, name: String, width: CGFloat, height: CGFloat,
                               at position: CGPoint, accent: UIColor? = nil) {
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
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        for node in nodes(at: point) {
            guard let name = node.parent?.name ?? node.name else { continue }
            switch name {
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
            case "continuer":
                animateTap(node.parent ?? node)
                run(SKAction.wait(forDuration: 0.12)) {
                    self.navigateToGame(savedState: GameState.load())
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
