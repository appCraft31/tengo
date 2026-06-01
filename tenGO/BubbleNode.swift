//
//  BubbleNode.swift
//  tenGO
//

import SpriteKit

class BubbleNode: SKNode {
    let value: Int
    private let circle: SKShapeNode
    private let digitLabel: SKLabelNode

    // Défi du jour — twists visuels (rendu sobre, monochrome).
    private(set) var isFrozen = false
    private(set) var isAnchored = false
    private var frostOverlay: SKNode?

    static let bubbleRadius: CGFloat = 39

    /// Couleur d'une valeur selon le thème actif.
    static func color(for value: Int) -> UIColor {
        ThemeManager.shared.active.color(forValue: value)
    }

    init(value: Int) {
        self.value = value

        let color = BubbleNode.color(for: value)
        circle = SKShapeNode(circleOfRadius: BubbleNode.bubbleRadius)
        circle.fillColor = color
        circle.strokeColor = color.darkened(by: 0.12)
        circle.lineWidth = 1.5

        digitLabel = SKLabelNode(text: "\(value)")
        digitLabel.fontName = "AvenirNext-Medium"
        digitLabel.fontSize = 36
        digitLabel.fontColor = ThemeManager.shared.active.digit
        digitLabel.verticalAlignmentMode = .center
        digitLabel.horizontalAlignmentMode = .center

        super.init()
        addChild(circle)
        addChild(digitLabel)
    }

    required init?(coder: NSCoder) { fatalError() }

    // MARK: - Twists (Défi du jour)

    /// Bulle ancrée : ne tombe pas. Repère sobre = petit point fixe sous le chiffre.
    func setAnchored(_ anchored: Bool) {
        isAnchored = anchored
        guard anchored else { return }
        let mark = SKShapeNode(circleOfRadius: 4)
        mark.fillColor = UIColor(white: 0.45, alpha: 0.9)
        mark.strokeColor = .clear
        mark.position = CGPoint(x: 0, y: -BubbleNode.bubbleRadius + 12)
        mark.zPosition = 2
        addChild(mark)
    }

    /// Bulle gelée : inutilisable tant que le givre n'a pas fondu.
    /// Voile glacé translucide + chiffre atténué.
    func setFrozen(_ frozen: Bool) {
        guard frozen else { return }
        isFrozen = true
        let overlay = SKNode()
        let ice = SKShapeNode(circleOfRadius: BubbleNode.bubbleRadius)
        ice.fillColor = UIColor(red: 0.82, green: 0.90, blue: 0.98, alpha: 0.55)
        ice.strokeColor = UIColor(white: 1.0, alpha: 0.7)
        ice.lineWidth = 2
        overlay.addChild(ice)
        overlay.zPosition = 1
        addChild(overlay)
        frostOverlay = overlay
        digitLabel.alpha = 0.45
    }

    /// Fait fondre le givre (un chemin adjacent a été validé).
    func thaw() {
        guard isFrozen else { return }
        isFrozen = false
        digitLabel.run(SKAction.fadeAlpha(to: 1.0, duration: 0.3))
        frostOverlay?.run(SKAction.sequence([
            SKAction.group([
                SKAction.fadeOut(withDuration: 0.3),
                SKAction.scale(to: 1.25, duration: 0.3)
            ]),
            SKAction.removeFromParent()
        ]))
        frostOverlay = nil
    }

    /// Bloc-obstacle inerte (case bloquée). Pierre pastel sans chiffre.
    static func makeObstacle(cellSize: CGFloat) -> SKNode {
        let side = cellSize * 0.66
        let stone = SKShapeNode(rectOf: CGSize(width: side, height: side), cornerRadius: 13)
        stone.fillColor = UIColor(white: 0.80, alpha: 1)
        stone.strokeColor = UIColor(white: 0.62, alpha: 0.8)
        stone.lineWidth = 1.5
        return stone
    }

    // MARK: - State

    func setSelected(_ selected: Bool) {
        removeAction(forKey: "select")
        let color = BubbleNode.color(for: value)
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
        let color = BubbleNode.color(for: value)
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
