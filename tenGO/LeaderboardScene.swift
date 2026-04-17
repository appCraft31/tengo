//
//  LeaderboardScene.swift
//  tenGO
//

import SpriteKit

class LeaderboardScene: SKScene {

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        setupUI()
    }

    private func setupUI() {
        let centerX: CGFloat = 0
        let topY = size.height / 2

        // Titre
        let title = SKLabelNode(text: "Classement")
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 44
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: centerX, y: topY - 100)
        addChild(title)

        // Scores
        let scores = GameState.highScores()

        if scores.isEmpty {
            let empty = SKLabelNode(text: "Aucune partie jouée")
            empty.fontName = "AvenirNext-UltraLight"
            empty.fontSize = 20
            empty.fontColor = UIColor(white: 0.55, alpha: 1)
            empty.verticalAlignmentMode = .center
            empty.position = CGPoint(x: centerX, y: 0)
            addChild(empty)
        } else {
            let rowHeight: CGFloat = 52
            let startY = min(CGFloat(scores.count - 1), 4) * rowHeight / 2 + 80

            for (i, score) in scores.enumerated() {
                let y = startY - CGFloat(i) * rowHeight

                // Rangée
                let rankLabel = SKLabelNode(text: "#\(i + 1)")
                rankLabel.fontName = "AvenirNext-UltraLight"
                rankLabel.fontSize = 16
                rankLabel.fontColor = UIColor(white: 0.55, alpha: 1)
                rankLabel.horizontalAlignmentMode = .right
                rankLabel.verticalAlignmentMode = .center
                rankLabel.position = CGPoint(x: centerX - 60, y: y)
                addChild(rankLabel)

                let scoreLabel = SKLabelNode(text: "\(score) pts")
                scoreLabel.fontName = i == 0 ? "AvenirNext-Medium" : "AvenirNext-UltraLight"
                scoreLabel.fontSize = i == 0 ? 28 : 22
                scoreLabel.fontColor = i == 0
                    ? UIColor(red: 0.92, green: 0.62, blue: 0.18, alpha: 1)
                    : UIColor(white: 0.35, alpha: 1)
                scoreLabel.horizontalAlignmentMode = .left
                scoreLabel.verticalAlignmentMode = .center
                scoreLabel.position = CGPoint(x: centerX - 40, y: y)
                addChild(scoreLabel)
            }
        }

        // Bouton retour
        let back = SKNode()
        back.name = "back"
        back.position = CGPoint(x: centerX, y: -topY + 80)
        addChild(back)

        let backCircle = SKShapeNode(circleOfRadius: 36)
        backCircle.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        backCircle.strokeColor = UIColor(white: 0.68, alpha: 0.45)
        backCircle.lineWidth = 1
        back.addChild(backCircle)

        let backIcon = SKLabelNode(text: "‹")
        backIcon.fontName = "AvenirNext-Medium"
        backIcon.fontSize = 32
        backIcon.fontColor = UIColor(white: 0.45, alpha: 1)
        backIcon.verticalAlignmentMode = .center
        backIcon.horizontalAlignmentMode = .center
        back.addChild(backIcon)
    }

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
        }
    }
}
