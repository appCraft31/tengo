//
//  LeaderboardScene.swift
//  tenGO
//

import SpriteKit
import GameKit

class LeaderboardScene: SKScene {

    private let bubbleColors: [UIColor] = [
        UIColor(red: 0.98, green: 0.72, blue: 0.68, alpha: 1),
        UIColor(red: 0.99, green: 0.84, blue: 0.70, alpha: 1),
        UIColor(red: 0.99, green: 0.95, blue: 0.72, alpha: 1),
        UIColor(red: 0.78, green: 0.94, blue: 0.82, alpha: 1),
        UIColor(red: 0.72, green: 0.88, blue: 0.98, alpha: 1),
        UIColor(red: 0.82, green: 0.78, blue: 0.97, alpha: 1),
        UIColor(red: 0.98, green: 0.78, blue: 0.88, alpha: 1),
        UIColor(red: 0.80, green: 0.91, blue: 0.80, alpha: 1),
        UIColor(red: 0.76, green: 0.82, blue: 0.97, alpha: 1),
    ]

    // Arc-en-ciel pastel par rang (couleurs des bulles du jeu, alpha réduit pour les pills)
    // #1 rose, #2 bleu ciel, #3 menthe, #4 jaune, #5 lavande, #6 pêche, #7 sauge
    private let rankColors: [UIColor] = [
        UIColor(red: 0.98, green: 0.78, blue: 0.88, alpha: 1), // rose
        UIColor(red: 0.72, green: 0.88, blue: 0.98, alpha: 1), // ciel
        UIColor(red: 0.78, green: 0.94, blue: 0.82, alpha: 1), // menthe
        UIColor(red: 0.99, green: 0.95, blue: 0.72, alpha: 1), // jaune
        UIColor(red: 0.82, green: 0.78, blue: 0.97, alpha: 1), // lavande
        UIColor(red: 0.99, green: 0.84, blue: 0.70, alpha: 1), // pêche
        UIColor(red: 0.80, green: 0.91, blue: 0.80, alpha: 1), // sauge
    ]

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        setupBackground()
        setupUI()
        NotificationCenter.default.post(name: .tenGOSceneChanged, object: nil, userInfo: ["isMenu": false])
    }

    // MARK: - Fond animé

    private func setupBackground() {
        let configs: [(radius: CGFloat, x: CGFloat, y: CGFloat, colorIdx: Int, duration: Double)] = [
            (88,  -280,  480, 0, 7.2),
            (62,   260,  350, 4, 9.5),
            (110, -180, -150, 6, 11.0),
            (74,   310, -320, 1, 8.3),
            (96,   -60,  560, 3, 10.1),
            (54,   200, -500, 7, 6.8),
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
            let up = SKAction.moveBy(x: 0, y: 18, duration: cfg.duration)
            let down = SKAction.moveBy(x: 0, y: -18, duration: cfg.duration)
            up.timingMode = .easeInEaseOut
            down.timingMode = .easeInEaseOut
            bubble.run(SKAction.repeatForever(SKAction.sequence([up, down])))
        }
    }

    // MARK: - UI

    private func setupUI() {
        guard let view = view else { return }
        let topY = size.height / 2

        // Même logique que TutorialScene : avec aspectFill l'iPad rogne le haut/bas.
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let visibleHalfH = view.bounds.height / scale / 2
        let safeBottomInset = view.safeAreaInsets.bottom / scale
        let bottomY = -visibleHalfH + safeBottomInset

        // Titre
        let title = SKLabelNode(text: String(localized: "leaderboard.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 44
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        let scores = GameState.highScores()

        if scores.isEmpty {
            let emoji = SKLabelNode(text: "✦")
            emoji.fontName = "AvenirNext-UltraLight"
            emoji.fontSize = 36
            emoji.fontColor = UIColor(white: 0.65, alpha: 1)
            emoji.verticalAlignmentMode = .center
            emoji.position = CGPoint(x: 0, y: 60)
            addChild(emoji)

            let empty = SKLabelNode(text: String(localized: "leaderboard.empty_title"))
            empty.fontName = "AvenirNext-Medium"
            empty.fontSize = 22
            empty.fontColor = UIColor(white: 0.32, alpha: 1)
            empty.verticalAlignmentMode = .center
            empty.position = CGPoint(x: 0, y: 0)
            addChild(empty)

            let sub = SKLabelNode(text: String(localized: "leaderboard.empty_subtitle"))
            sub.fontName = "AvenirNext-UltraLight"
            sub.fontSize = 17
            sub.fontColor = UIColor(white: 0.55, alpha: 1)
            sub.verticalAlignmentMode = .center
            sub.position = CGPoint(x: 0, y: -34)
            addChild(sub)
        } else {
            addTopScore(scores[0], atY: topY - 230)
            let rest = Array(scores.dropFirst().prefix(6))
            if !rest.isEmpty {
                addScoreList(rest, startingAt: 2, topY: topY - 410)
            }
        }

        // Bouton retour — style bulle jeu
        addBackButton(atY: bottomY + 90)

        // Boutons Game Center : Défi du jour + Mondial, côte à côte
        if GKLocalPlayer.local.isAuthenticated {
            addLeaderboardButton(name: "dailyLeaderboard",
                                 title: String(localized: "leaderboard.daily_button", defaultValue: "Défi du jour"),
                                 color: UIColor(red: 0.86, green: 0.82, blue: 0.97, alpha: 1),
                                 at: CGPoint(x: -88, y: bottomY + 190))
            addLeaderboardButton(name: "worldLeaderboard",
                                 title: String(localized: "leaderboard.world_button"),
                                 color: UIColor(red: 0.72, green: 0.88, blue: 0.98, alpha: 1),
                                 at: CGPoint(x: 88, y: bottomY + 190))
        }
    }

    // Grande bulle pour le #1
    private func addTopScore(_ score: Int, atY y: CGFloat) {
        let radius: CGFloat = 70

        // Halo rose derrière
        let halo = SKShapeNode(circleOfRadius: radius + 6)
        halo.fillColor = rankColors[0].withAlphaComponent(0.55)
        halo.strokeColor = .clear
        halo.position = CGPoint(x: 0, y: y)
        addChild(halo)

        let circle = SKShapeNode(circleOfRadius: radius)
        circle.fillColor = UIColor(red: 0.96, green: 0.93, blue: 0.90, alpha: 1)
        circle.strokeColor = UIColor(white: 0.70, alpha: 0.50)
        circle.lineWidth = 1.5
        circle.position = CGPoint(x: 0, y: y)
        addChild(circle)

        // Petite bulle rose — marqueur #1 (comme le logo)
        let dot = SKShapeNode(circleOfRadius: 10)
        dot.fillColor = bubbleColors[6] // rose
        dot.strokeColor = .clear
        dot.position = CGPoint(x: 0, y: y + radius - 12)
        dot.zPosition = 1
        addChild(dot)

        let ptsLabel = SKLabelNode(text: String(localized: "game.points_label"))
        ptsLabel.fontName = "AvenirNext-UltraLight"
        ptsLabel.fontSize = 14
        ptsLabel.fontColor = UIColor(white: 0.58, alpha: 1)
        ptsLabel.verticalAlignmentMode = .center
        ptsLabel.position = CGPoint(x: 0, y: y + 20)
        addChild(ptsLabel)

        let scoreLabel = SKLabelNode(text: "\(score)")
        scoreLabel.fontName = "AvenirNext-Heavy"
        scoreLabel.fontSize = 42
        scoreLabel.fontColor = UIColor(white: 0.28, alpha: 1)
        scoreLabel.verticalAlignmentMode = .center
        scoreLabel.position = CGPoint(x: 0, y: y - 12)
        addChild(scoreLabel)
    }

    // Pills pour les scores #2 et suivants
    private func addScoreList(_ scores: [Int], startingAt rankStart: Int, topY: CGFloat) {
        let pillW: CGFloat = 300
        let pillH: CGFloat = 52
        let gap: CGFloat = 14

        for (i, score) in scores.enumerated() {
            let rank = rankStart + i
            let y = topY - CGFloat(i) * (pillH + gap)

            let pill = SKNode()
            pill.position = CGPoint(x: 0, y: y)
            addChild(pill)

            let fillColor = rankColors[min(i + 1, rankColors.count - 1)]
            let bg = SKShapeNode(rectOf: CGSize(width: pillW, height: pillH), cornerRadius: pillH / 2)
            bg.fillColor = fillColor
            bg.strokeColor = UIColor(white: 0.68, alpha: 0.35)
            bg.lineWidth = 1
            pill.addChild(bg)

            let rankLabel = SKLabelNode(text: "#\(rank)")
            rankLabel.fontName = "AvenirNext-UltraLight"
            rankLabel.fontSize = 15
            rankLabel.fontColor = UIColor(white: 0.60, alpha: 1)
            rankLabel.horizontalAlignmentMode = .left
            rankLabel.verticalAlignmentMode = .center
            rankLabel.position = CGPoint(x: -pillW / 2 + 24, y: 0)
            pill.addChild(rankLabel)

            let scoreLabel = SKLabelNode(text: "\(score) \(String(localized: "game.points_label"))")
            scoreLabel.fontName = "AvenirNext-Medium"
            scoreLabel.fontSize = 20
            scoreLabel.fontColor = UIColor(white: 0.30, alpha: 1)
            scoreLabel.horizontalAlignmentMode = .right
            scoreLabel.verticalAlignmentMode = .center
            scoreLabel.position = CGPoint(x: pillW / 2 - 24, y: 0)
            pill.addChild(scoreLabel)
        }
    }

    private func addLeaderboardButton(name: String, title: String, color: UIColor, at position: CGPoint) {
        let btn = SKNode()
        btn.name = name
        btn.position = position
        addChild(btn)

        let pillW: CGFloat = 165
        let pillH: CGFloat = 52
        let bg = SKShapeNode(rectOf: CGSize(width: pillW, height: pillH), cornerRadius: pillH / 2)
        bg.fillColor = color
        bg.strokeColor = UIColor(white: 0.60, alpha: 0.35)
        bg.lineWidth = 1
        btn.addChild(bg)

        let label = SKLabelNode(text: title)
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 17
        label.fontColor = UIColor(white: 0.25, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .center
        btn.addChild(label)
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
            if node.name == "back" || node.parent?.name == "back" {
                let menu = MenuScene(size: size)
                menu.scaleMode = .aspectFill
                view?.presentScene(menu, transition: SKTransition.fade(withDuration: 0.28))
                return
            }
            if node.name == "worldLeaderboard" || node.parent?.name == "worldLeaderboard" {
                NotificationCenter.default.post(name: .tenGOShowGameCenter, object: nil,
                                                userInfo: ["leaderboardID": AppConfig.gameCenterLeaderboardID])
                return
            }
            if node.name == "dailyLeaderboard" || node.parent?.name == "dailyLeaderboard" {
                NotificationCenter.default.post(name: .tenGOShowGameCenter, object: nil,
                                                userInfo: ["leaderboardID": AppConfig.gameCenterDailyLeaderboardID])
                return
            }
        }
    }
}
