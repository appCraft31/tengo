//
//  BubbleNode.swift
//  tenGO
//

import SpriteKit

class BubbleNode: SKNode {
    let value: Int
    private let circle: SKShapeNode
    private let digitLabel: SKLabelNode

    static let bubbleRadius: CGFloat = 39

    // Pastel color per digit value (index 0 unused)
    private static let colors: [UIColor] = [
        .clear,
        UIColor(red: 1.00, green: 0.62, blue: 0.62, alpha: 1), // 1 coral
        UIColor(red: 1.00, green: 0.78, blue: 0.62, alpha: 1), // 2 peach
        UIColor(red: 1.00, green: 0.96, blue: 0.62, alpha: 1), // 3 butter
        UIColor(red: 0.67, green: 0.95, blue: 0.75, alpha: 1), // 4 mint
        UIColor(red: 0.62, green: 0.86, blue: 1.00, alpha: 1), // 5 sky
        UIColor(red: 0.80, green: 0.72, blue: 1.00, alpha: 1), // 6 lavender
        UIColor(red: 1.00, green: 0.72, blue: 0.86, alpha: 1), // 7 rose
        UIColor(red: 0.75, green: 0.91, blue: 0.75, alpha: 1), // 8 sage
        UIColor(red: 0.72, green: 0.76, blue: 1.00, alpha: 1), // 9 periwinkle
    ]

    static func color(for value: Int) -> UIColor { colors[value] }

    init(value: Int) {
        self.value = value

        let color = BubbleNode.colors[value]
        circle = SKShapeNode(circleOfRadius: BubbleNode.bubbleRadius)
        circle.fillColor = color
        circle.strokeColor = color.darkened(by: 0.12)
        circle.lineWidth = 1.5

        digitLabel = SKLabelNode(text: "\(value)")
        digitLabel.fontName = "AvenirNext-Medium"
        digitLabel.fontSize = 36
        digitLabel.fontColor = UIColor(white: 0.25, alpha: 1)
        digitLabel.verticalAlignmentMode = .center
        digitLabel.horizontalAlignmentMode = .center

        super.init()
        addChild(circle)
        addChild(digitLabel)
    }

    required init?(coder: NSCoder) { fatalError() }

    // MARK: - State

    func setSelected(_ selected: Bool) {
        removeAction(forKey: "select")
        let color = BubbleNode.colors[value]
        if selected {
            circle.strokeColor = UIColor(white: 1.0, alpha: 0.9)
            circle.lineWidth = 2.5
            run(SKAction.scale(to: 1.1, duration: 0.1), withKey: "select")
        } else {
            circle.strokeColor = color.darkened(by: 0.12)
            circle.lineWidth = 1.5
            run(SKAction.scale(to: 1.0, duration: 0.08), withKey: "select")
        }
    }

    // MARK: - Animations

    func playPopAnimation(completion: @escaping () -> Void) {
        if let parentNode = parent {
            let burst = makeBurst()
            burst.position = position
            parentNode.addChild(burst)
        }
        let seq = SKAction.sequence([
            SKAction.scale(to: 1.2, duration: 0.08),
            SKAction.group([
                SKAction.scale(to: 0.0, duration: 0.15),
                SKAction.fadeOut(withDuration: 0.15)
            ]),
            SKAction.run(completion),
            SKAction.removeFromParent()
        ])
        run(seq)
    }

    func playFallAnimation(toY: CGFloat, duration: TimeInterval, completion: @escaping () -> Void) {
        let move = SKAction.moveTo(y: toY, duration: duration)
        move.timingMode = .easeIn
        run(SKAction.sequence([move, SKAction.run(completion)]), withKey: "fall")
    }

    // MARK: - Private

    private func makeBurst() -> SKNode {
        let container = SKNode()
        let color = BubbleNode.colors[value]
        let count = 6
        for i in 0..<count {
            let angle = CGFloat(i) / CGFloat(count) * .pi * 2
            let dot = SKShapeNode(circleOfRadius: 5)
            dot.fillColor = color
            dot.strokeColor = .clear
            let dist: CGFloat = 32
            dot.run(SKAction.sequence([
                SKAction.group([
                    SKAction.move(to: CGPoint(x: cos(angle) * dist, y: sin(angle) * dist), duration: 0.22),
                    SKAction.fadeOut(withDuration: 0.22)
                ]),
                SKAction.removeFromParent()
            ]))
            container.addChild(dot)
        }
        container.run(SKAction.sequence([
            SKAction.wait(forDuration: 0.25),
            SKAction.removeFromParent()
        ]))
        return container
    }
}

private extension UIColor {
    func darkened(by factor: CGFloat) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        getRed(&r, green: &g, blue: &b, alpha: &a)
        return UIColor(red: max(0, r - factor), green: max(0, g - factor), blue: max(0, b - factor), alpha: a)
    }
}
