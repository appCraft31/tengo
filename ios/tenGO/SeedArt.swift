import SpriteKit
import UIKit

/// Dessins de la graine de dix (pousse, fleur, horloge) — mêmes formes que
/// l'app Android, pour l'accueil et l'écran de fin.
enum SeedArt {

    static let gold = UIColor(red: 0.98, green: 0.76, blue: 0.22, alpha: 1)
    static let leaf = UIColor(red: 0.40, green: 0.66, blue: 0.30, alpha: 1)
    static let gaugeEmpty = UIColor(white: 0, alpha: 0.08)

    /// Pousse : terre, tige, deux feuilles. `size` = côté du carré occupé.
    static func sprout(size w: CGFloat, alpha: CGFloat = 1) -> SKNode {
        let node = SKNode()
        // Repère : origine au centre, y vers le haut.
        let soil = SKShapeNode(ellipseOf: CGSize(width: w * 0.6, height: w * 0.14))
        soil.fillColor = UIColor(red: 0.55, green: 0.40, blue: 0.25, alpha: 0.35 * alpha)
        soil.strokeColor = .clear
        soil.position = CGPoint(x: 0, y: -w * 0.39)
        node.addChild(soil)

        let stem = SKShapeNode()
        let path = CGMutablePath()
        path.move(to: CGPoint(x: 0, y: -w * 0.38))
        path.addLine(to: CGPoint(x: 0, y: w * 0.1))
        stem.path = path
        stem.strokeColor = leaf.withAlphaComponent(alpha)
        stem.lineWidth = w * 0.07
        stem.lineCap = .round
        node.addChild(stem)

        for (dx, dy) in [(-0.18, 0.1), (0.18, 0.2)] as [(CGFloat, CGFloat)] {
            let l = SKShapeNode(ellipseOf: CGSize(width: w * 0.36, height: w * 0.2))
            l.fillColor = leaf.withAlphaComponent(alpha)
            l.strokeColor = .clear
            l.position = CGPoint(x: w * dx, y: w * dy)
            node.addChild(l)
        }
        return node
    }

    /// Fleur éclose : dix pétales, cœur crème, pistil orangé.
    static func flower(size w: CGFloat, petal: UIColor = gold) -> SKNode {
        let node = SKNode()
        let r = w * 0.3
        for i in 0..<10 {
            let a = CGFloat(i) * .pi / 5
            let p = SKShapeNode(circleOfRadius: r * 0.26)
            p.fillColor = petal.withAlphaComponent(0.9)
            p.strokeColor = .clear
            p.position = CGPoint(x: cos(a) * r * 0.98, y: sin(a) * r * 0.98)
            node.addChild(p)
        }
        let heart = SKShapeNode(circleOfRadius: r)
        heart.fillColor = UIColor(red: 1, green: 0.97, blue: 0.88, alpha: 1)
        heart.strokeColor = .clear
        node.addChild(heart)
        let core = SKShapeNode(circleOfRadius: r * 0.47)
        core.fillColor = UIColor(red: 0.93, green: 0.62, blue: 0.15, alpha: 1)
        core.strokeColor = .clear
        node.addChild(core)
        return node
    }

    /// Petite horloge (temps restant).
    static func clock(size w: CGFloat, color: UIColor) -> SKNode {
        let node = SKNode()
        let ring = SKShapeNode(circleOfRadius: w * 0.45)
        ring.strokeColor = color
        ring.lineWidth = w * 0.11
        ring.fillColor = .clear
        node.addChild(ring)
        let hands = SKShapeNode()
        let path = CGMutablePath()
        path.move(to: CGPoint(x: 0, y: w * 0.3))
        path.addLine(to: .zero)
        path.addLine(to: CGPoint(x: w * 0.22, y: 0))
        hands.path = path
        hands.strokeColor = color
        hands.lineWidth = w * 0.11
        hands.lineCap = .round
        hands.lineJoin = .round
        node.addChild(hands)
        return node
    }

    /// Pastille dorée « ×N ».
    static func powerBadge(_ power: Int, fontSize: CGFloat = 16) -> SKNode {
        let label = SKLabelNode(text: "×\(power)")
        label.fontName = "AvenirNext-Heavy"
        label.fontSize = fontSize
        label.fontColor = UIColor(red: 0.36, green: 0.22, blue: 0, alpha: 1)
        label.verticalAlignmentMode = .center
        let w = label.frame.width + 16, h = fontSize + 10
        let bg = SKShapeNode(rectOf: CGSize(width: w, height: h), cornerRadius: h / 2)
        bg.fillColor = gold
        bg.strokeColor = .clear
        let node = SKNode()
        node.addChild(bg)
        node.addChild(label)
        return node
    }

    /// Légère pulsation (si les animations sont actives).
    static func pulse(_ node: SKNode, to scale: CGFloat = 1.08) {
        guard JuiceSettings.motionEnabled else { return }
        node.run(.repeatForever(.sequence([
            .scale(to: scale, duration: 0.8), .scale(to: 1, duration: 0.8),
        ])))
    }
}
