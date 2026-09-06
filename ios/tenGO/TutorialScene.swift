//
//  TutorialScene.swift
//  tenGO
//

import UIKit
import SpriteKit

class TutorialScene: SKScene {

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

    private var currentStep = 0
    private let totalSteps = 3
    private var stepContainer: SKNode?
    private var dotsNodes: [SKShapeNode] = []
    private var visibleHeight: CGFloat = 1334

    override func didMove(to view: SKView) {
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        visibleHeight = view.bounds.height / scale
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        setupBackground()
        setupChrome()
        showStep(0, animated: false)
    }

    // MARK: - Fond animé

    private func setupBackground() {
        let configs: [(CGFloat, CGFloat, CGFloat, Int, Double)] = [
            (88, -280, 480, 0, 7.2), (62, 260, 350, 4, 9.5),
            (110, -180, -150, 6, 11.0), (74, 310, -320, 1, 8.3),
            (96, -60, 560, 3, 10.1), (54, 200, -500, 7, 6.8),
            (80, -320, -480, 2, 12.4), (68, 80, 200, 5, 9.0),
        ]
        for (r, x, y, c, d) in configs {
            let b = SKShapeNode(circleOfRadius: r)
            b.fillColor = bubbleColors[c].withAlphaComponent(0.18)
            b.strokeColor = .clear
            b.position = CGPoint(x: x, y: y)
            b.zPosition = -1
            addChild(b)
            let up = SKAction.moveBy(x: 0, y: 18, duration: d)
            let dn = SKAction.moveBy(x: 0, y: -18, duration: d)
            up.timingMode = .easeInEaseOut; dn.timingMode = .easeInEaseOut
            b.run(SKAction.repeatForever(SKAction.sequence([up, dn])))
        }
    }

    // MARK: - Chrome fixe (titre, dots, boutons)

    private func setupChrome() {
        let topY = visibleHeight / 2

        // Titre
        let title = SKLabelNode(text: String(localized: "tutorial.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = UIColor(white: 0.26, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        // Bouton Fermer (×)
        let closeNode = SKNode()
        closeNode.name = "close"
        closeNode.position = CGPoint(x: size.width / 2 - 60, y: topY - 110)
        addChild(closeNode)

        let closeCircle = SKShapeNode(circleOfRadius: 24)
        closeCircle.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        closeCircle.strokeColor = UIColor(white: 0.68, alpha: 0.4)
        closeCircle.lineWidth = 1
        closeNode.addChild(closeCircle)

        let closeLabel = SKLabelNode(text: "×")
        closeLabel.fontName = "AvenirNext-Medium"
        closeLabel.fontSize = 26
        closeLabel.fontColor = UIColor(white: 0.45, alpha: 1)
        closeLabel.verticalAlignmentMode = .center
        closeNode.addChild(closeLabel)

        // Dots de progression
        setupDots(atY: -visibleHeight / 2 + 180)

        // Boutons navigation
        setupNavButtons(atY: -visibleHeight / 2 + 110)
    }

    private func setupDots(atY y: CGFloat) {
        let gap: CGFloat = 20
        let totalW = CGFloat(totalSteps - 1) * gap
        for i in 0..<totalSteps {
            let dot = SKShapeNode(circleOfRadius: 5)
            dot.position = CGPoint(x: -totalW / 2 + CGFloat(i) * gap, y: y)
            dot.strokeColor = .clear
            dot.zPosition = 1
            addChild(dot)
            dotsNodes.append(dot)
        }
        updateDots()
    }

    private func updateDots() {
        for (i, dot) in dotsNodes.enumerated() {
            dot.fillColor = i == currentStep
                ? UIColor(white: 0.30, alpha: 1)
                : UIColor(white: 0.72, alpha: 1)
            dot.run(SKAction.scale(to: i == currentStep ? 1.4 : 1.0, duration: 0.15))
        }
    }

    private func setupNavButtons(atY y: CGFloat) {
        // Précédent
        let prev = SKNode()
        prev.name = "prev"
        prev.position = CGPoint(x: -120, y: y)
        addChild(prev)

        let prevBg = SKShapeNode(rectOf: CGSize(width: 140, height: 52), cornerRadius: 26)
        prevBg.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        prevBg.strokeColor = UIColor(white: 0.68, alpha: 0.35)
        prevBg.lineWidth = 1
        prev.addChild(prevBg)

        let prevLabel = SKLabelNode(text: String(localized: "tutorial.previous_button"))
        prevLabel.fontName = "AvenirNext-UltraLight"
        prevLabel.fontSize = 17
        prevLabel.fontColor = UIColor(white: 0.40, alpha: 1)
        prevLabel.verticalAlignmentMode = .center
        prev.addChild(prevLabel)

        // Suivant / Commencer
        let next = SKNode()
        next.name = "next"
        next.position = CGPoint(x: 120, y: y)
        addChild(next)

        let nextBg = SKShapeNode(rectOf: CGSize(width: 180, height: 52), cornerRadius: 26)
        nextBg.name = "nextBg"
        nextBg.fillColor = UIColor(red: 0.82, green: 0.95, blue: 0.88, alpha: 1)
        nextBg.strokeColor = UIColor(white: 0.68, alpha: 0.35)
        nextBg.lineWidth = 1
        next.addChild(nextBg)

        let nextLabel = SKLabelNode(text: String(localized: "tutorial.next_button"))
        nextLabel.name = "nextLabel"
        nextLabel.fontName = "AvenirNext-Medium"
        nextLabel.fontSize = 17
        nextLabel.fontColor = UIColor(white: 0.26, alpha: 1)
        nextLabel.verticalAlignmentMode = .center
        next.addChild(nextLabel)
    }

    // MARK: - Steps

    private func showStep(_ step: Int, animated: Bool) {
        let old = stepContainer
        let newContainer = SKNode()
        newContainer.position = CGPoint(x: animated ? size.width : 0, y: 40)
        addChild(newContainer)
        stepContainer = newContainer

        switch step {
        case 0: buildStep0(in: newContainer)
        case 1: buildStep1(in: newContainer)
        case 2: buildStep2(in: newContainer)
        default: break
        }

        updateDots()
        updateNextButton()

        if animated {
            newContainer.run(SKAction.moveTo(x: 0, duration: 0.28))
            old?.run(SKAction.sequence([
                SKAction.moveTo(x: -size.width, duration: 0.28),
                SKAction.removeFromParent()
            ]))
        } else {
            old?.removeFromParent()
        }
    }

    private func updateNextButton() {
        guard let next = childNode(withName: "next") else { return }
        let label = next.childNode(withName: "nextLabel") as? SKLabelNode
        let bg    = next.childNode(withName: "nextBg") as? SKShapeNode
        if currentStep == totalSteps - 1 {
            label?.text = String(localized: "tutorial.play_button")
            bg?.fillColor = UIColor(red: 0.82, green: 0.95, blue: 0.88, alpha: 1)
        } else {
            label?.text = String(localized: "tutorial.next_button")
        }
    }

    // MARK: - Étape 0 : Relier des bulles

    private func buildStep0(in parent: SKNode) {
        addStepTitle(String(localized: "tutorial.step0_title"), to: parent, y: 240)
        addStepSubtitle(String(localized: "tutorial.step0_subtitle"), to: parent, y: 178)

        // Grille 3×4 démo
        let values = [[3, 7, 2], [4, 2, 6], [1, 5, 3], [8, 4, 5]]
        let colorIdx = [[2, 4, 1], [3, 1, 5], [0, 3, 2], [7, 3, 4]]
        let cellSize: CGFloat = 90
        let r: CGFloat = 36

        var bubblePositions: [CGPoint] = []
        for row in 0..<4 {
            for col in 0..<3 {
                let x = CGFloat(col - 1) * cellSize
                let y = CGFloat(1 - row) * cellSize - 20
                let pos = CGPoint(x: x, y: y)

                let circle = SKShapeNode(circleOfRadius: r)
                circle.fillColor = bubbleColors[colorIdx[row][col]]
                circle.strokeColor = .clear
                circle.position = pos
                parent.addChild(circle)

                let lbl = SKLabelNode(text: "\(values[row][col])")
                lbl.fontName = "AvenirNext-Medium"
                lbl.fontSize = 28
                lbl.fontColor = UIColor(white: 0.30, alpha: 1)
                lbl.verticalAlignmentMode = .center
                lbl.position = pos
                parent.addChild(lbl)

                if (row == 1 && col == 0) || (row == 1 && col == 1) || (row == 2 && col == 2) {
                    bubblePositions.append(pos)
                }
            }
        }

        // Chemin animé (4 + 2 + 1 = ... en démo)
        let pathPositions: [(row: Int, col: Int)] = [(1,0), (1,1), (2,2)]
        animatePath(positions: pathPositions.map {
            CGPoint(x: CGFloat($0.col - 1) * cellSize, y: CGFloat(1 - $0.row) * cellSize - 20)
        }, in: parent)

        // Texte somme
        let sumLabel = SKLabelNode(text: "4 + 2 + … = 10 ?")
        sumLabel.fontName = "AvenirNext-UltraLight"
        sumLabel.fontSize = 18
        sumLabel.fontColor = UIColor(white: 0.45, alpha: 1)
        sumLabel.verticalAlignmentMode = .center
        sumLabel.position = CGPoint(x: 0, y: -210)
        sumLabel.alpha = 0
        parent.addChild(sumLabel)
        sumLabel.run(SKAction.sequence([
            SKAction.wait(forDuration: 1.6),
            SKAction.fadeIn(withDuration: 0.4)
        ]))
    }

    // MARK: - Étape 1 : La somme fait 10

    private func buildStep1(in parent: SKNode) {
        addStepTitle(String(localized: "tutorial.step1_title"), to: parent, y: 240)
        addStepSubtitle(String(localized: "tutorial.step1_subtitle"), to: parent, y: 178)

        // Exemples de combinaisons
        let examples: [(String, [Int], [Int])] = [
            ("= 10 ✓", [1, 9],    [0, 4]),
            ("= 10 ✓", [3, 3, 4], [2, 7, 3]),
            ("= 10 ✓", [2, 3, 2, 3], [1, 5, 2, 6]),
        ]

        var y: CGFloat = 100
        for (label, vals, colors) in examples {
            addComboRow(values: vals, colorIdxs: colors, resultText: label, atY: y, in: parent)
            y -= 100
        }

        // Hint
        let hint = SKLabelNode(text: String(localized: "tutorial.step1_hint"))
        hint.fontName = "AvenirNext-UltraLight"
        hint.fontSize = 16
        hint.fontColor = UIColor(white: 0.50, alpha: 1)
        hint.verticalAlignmentMode = .center
        hint.numberOfLines = 2
        hint.preferredMaxLayoutWidth = 420
        hint.position = CGPoint(x: 0, y: -210)
        parent.addChild(hint)
    }

    // MARK: - Étape 2 : Scoring

    private func buildStep2(in parent: SKNode) {
        addStepTitle(String(localized: "tutorial.step2_title"), to: parent, y: 252)

        let pts = String(localized: "game.points_label")
        let rows: [(String, String)] = [
            (String(localized: "tutorial.step2_2_bubbles"),     "10 \(pts)"),
            (String(localized: "tutorial.step2_3_bubbles"),     "30 \(pts)"),
            (String(localized: "tutorial.step2_4_bubbles"),     "100 \(pts)"),
            (String(localized: "tutorial.step2_5plus_bubbles"), "100 + 50 × extra"),
        ]
        let rowH: CGFloat = 62
        var y: CGFloat = 110
        for (left, right) in rows {
            addScoreRow(left: left, right: right, atY: y, in: parent)
            y -= rowH
        }

        // Perfect clear
        let pc = SKNode()
        pc.position = CGPoint(x: 0, y: y - 20)
        parent.addChild(pc)

        let pcBg = SKShapeNode(rectOf: CGSize(width: 400, height: 58), cornerRadius: 29)
        pcBg.fillColor = UIColor(red: 0.99, green: 0.95, blue: 0.78, alpha: 1)
        pcBg.strokeColor = UIColor(white: 0.72, alpha: 0.35)
        pcBg.lineWidth = 1
        pc.addChild(pcBg)

        let pcLabel = SKLabelNode(text: String(localized: "tutorial.step2_perfect_clear"))
        pcLabel.fontName = "AvenirNext-Medium"
        pcLabel.fontSize = 16
        pcLabel.fontColor = UIColor(red: 0.75, green: 0.52, blue: 0.12, alpha: 1)
        pcLabel.verticalAlignmentMode = .center
        pc.addChild(pcLabel)
    }

    // MARK: - Helpers UI

    private func addStepTitle(_ text: String, to parent: SKNode, y: CGFloat) {
        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-Heavy"
        label.fontSize = 26
        label.fontColor = UIColor(white: 0.24, alpha: 1)
        label.verticalAlignmentMode = .center
        label.numberOfLines = 2
        label.preferredMaxLayoutWidth = 460
        label.position = CGPoint(x: 0, y: y)
        parent.addChild(label)
    }

    private func addStepSubtitle(_ text: String, to parent: SKNode, y: CGFloat) {
        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-UltraLight"
        label.fontSize = 18
        label.fontColor = UIColor(white: 0.42, alpha: 1)
        label.verticalAlignmentMode = .center
        label.numberOfLines = 2
        label.preferredMaxLayoutWidth = 460
        label.position = CGPoint(x: 0, y: y)
        parent.addChild(label)
    }

    private func addComboRow(values: [Int], colorIdxs: [Int], resultText: String,
                             atY y: CGFloat, in parent: SKNode) {
        let r: CGFloat = 28
        let gap: CGFloat = 70
        let totalW = CGFloat(values.count - 1) * gap
        let startX = -totalW / 2

        for (i, val) in values.enumerated() {
            let x = startX + CGFloat(i) * gap
            let circle = SKShapeNode(circleOfRadius: r)
            circle.fillColor = bubbleColors[colorIdxs[i]]
            circle.strokeColor = .clear
            circle.position = CGPoint(x: x, y: y)
            parent.addChild(circle)

            let lbl = SKLabelNode(text: "\(val)")
            lbl.fontName = "AvenirNext-Medium"
            lbl.fontSize = 22
            lbl.fontColor = UIColor(white: 0.28, alpha: 1)
            lbl.verticalAlignmentMode = .center
            lbl.position = CGPoint(x: x, y: y)
            parent.addChild(lbl)

            if i < values.count - 1 {
                let plus = SKLabelNode(text: "+")
                plus.fontName = "AvenirNext-UltraLight"
                plus.fontSize = 18
                plus.fontColor = UIColor(white: 0.55, alpha: 1)
                plus.verticalAlignmentMode = .center
                plus.position = CGPoint(x: x + gap / 2, y: y)
                parent.addChild(plus)
            }
        }

        let result = SKLabelNode(text: resultText)
        result.fontName = "AvenirNext-Medium"
        result.fontSize = 17
        result.fontColor = UIColor(red: 0.30, green: 0.68, blue: 0.42, alpha: 1)
        result.verticalAlignmentMode = .center
        result.horizontalAlignmentMode = .left
        result.position = CGPoint(x: totalW / 2 + r + 16, y: y)
        parent.addChild(result)
    }

    private func addScoreRow(left: String, right: String, atY y: CGFloat, in parent: SKNode) {
        let row = SKNode()
        row.position = CGPoint(x: 0, y: y)
        parent.addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: 400, height: 50), cornerRadius: 25)
        bg.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.30)
        bg.lineWidth = 1
        row.addChild(bg)

        let leftLabel = SKLabelNode(text: left)
        leftLabel.fontName = "AvenirNext-UltraLight"
        leftLabel.fontSize = 18
        leftLabel.fontColor = UIColor(white: 0.36, alpha: 1)
        leftLabel.verticalAlignmentMode = .center
        leftLabel.horizontalAlignmentMode = .left
        leftLabel.position = CGPoint(x: -180, y: 0)
        row.addChild(leftLabel)

        let rightLabel = SKLabelNode(text: right)
        rightLabel.fontName = "AvenirNext-Medium"
        rightLabel.fontSize = 18
        rightLabel.fontColor = UIColor(white: 0.28, alpha: 1)
        rightLabel.verticalAlignmentMode = .center
        rightLabel.horizontalAlignmentMode = .right
        rightLabel.position = CGPoint(x: 180, y: 0)
        row.addChild(rightLabel)
    }

    // MARK: - Animation chemin

    private func animatePath(positions: [CGPoint], in parent: SKNode) {
        guard positions.count >= 2 else { return }

        var actions: [SKAction] = [SKAction.wait(forDuration: 0.5)]
        for i in 1..<positions.count {
            let p0 = positions[i - 1]
            let p1 = positions[i]
            actions.append(SKAction.run {
                let line = SKShapeNode()
                let path = CGMutablePath()
                path.move(to: p0)
                path.addLine(to: p1)
                line.path = path
                line.strokeColor = UIColor(white: 1.0, alpha: 0.75)
                line.lineWidth = 7
                line.lineCap = .round
                line.zPosition = 2
                parent.addChild(line)
            })
            actions.append(SKAction.wait(forDuration: 0.35))
        }

        // Highlight sélection
        for pos in positions {
            let ring = SKShapeNode(circleOfRadius: 39)
            ring.strokeColor = UIColor(white: 1.0, alpha: 0.9)
            ring.lineWidth = 3
            ring.fillColor = .clear
            ring.position = pos
            ring.zPosition = 3
            ring.alpha = 0
            parent.addChild(ring)
            ring.run(SKAction.sequence([
                SKAction.wait(forDuration: 0.4),
                SKAction.fadeIn(withDuration: 0.2)
            ]))
        }

        parent.run(SKAction.sequence(actions + [
            SKAction.wait(forDuration: 1.2),
            SKAction.run { [weak self] in
                parent.children
                    .filter { $0 is SKShapeNode && ($0 as! SKShapeNode).lineWidth == 7 }
                    .forEach { $0.removeFromParent() }
            }
        ]))
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        for node in nodes(at: point) {
            let name = node.name ?? node.parent?.name
            switch name {
            case "close":
                navigateToMenu()
            case "next":
                if currentStep < totalSteps - 1 {
                    currentStep += 1
                    showStep(currentStep, animated: true)
                } else {
                    UserDefaults.standard.set(true, forKey: AppConfig.UserDefaultsKey.hasSeenTutorial)
                    AnalyticsService.tutorialComplete()
                    navigateToNewGame()
                }
            case "prev":
                if currentStep > 0 {
                    currentStep -= 1
                    showStep(currentStep, animated: true)
                }
            default: break
            }
        }
    }

    private func navigateToMenu() {
        let menu = MenuScene(size: size)
        menu.scaleMode = .aspectFill
        view?.presentScene(menu, transition: SceneTransition.crossFade(0.28))
    }

    private func navigateToNewGame() {
        GameState.clear()
        let scene = GameScene(size: size, savedState: nil)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SceneTransition.crossFade(0.35))
    }
}
