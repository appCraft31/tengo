//
//  PopEffects.swift
//  tenGO
//

import SpriteKit

/// Effets de destruction des bulles : éclats, onde de choc, flash.
/// Nœuds éphémères auto-détruits, indépendants des BubbleNode — leur durée
/// ne retarde jamais la gravité (le wait(0.26) de commitPath reste maître).
enum PopEffects {

    /// Éclatement à la position d'une bulle détruite.
    /// `intensity` = longueur de la chaîne validée (1 pour le marteau) :
    /// plus la chaîne est longue, plus l'éclat est fourni.
    static func makeBurst(color: UIColor, intensity: Int) -> SKNode {
        let container = SKNode()
        container.zPosition = 15

        // Flash doux au centre.
        let flash = SKShapeNode(circleOfRadius: BubbleNode.bubbleRadius * 0.9)
        flash.fillColor = UIColor(white: 1.0, alpha: 0.5)
        flash.strokeColor = .clear
        container.addChild(flash)
        flash.run(SKAction.sequence([
            SKAction.group([
                SKAction.scale(to: 1.6, duration: 0.15),
                SKAction.fadeOut(withDuration: 0.15)
            ]),
            SKAction.removeFromParent()
        ]))

        // Éclats radiaux — teinte de la bulle éclaircie aléatoirement (pastel).
        let count = 8 + 2 * min(intensity, 6)
        for _ in 0..<count {
            let dot = SKShapeNode(circleOfRadius: CGFloat.random(in: 3...7))
            dot.fillColor = color.mixedWithWhite(CGFloat.random(in: 0...0.35))
            dot.strokeColor = .clear
            dot.alpha = 0.9
            container.addChild(dot)

            let angle = CGFloat.random(in: 0..<(2 * .pi))
            let dist = CGFloat.random(in: 30...70)
            let duration = TimeInterval.random(in: 0.30...0.45)
            let fly = SKAction.move(to: CGPoint(x: cos(angle) * dist, y: sin(angle) * dist),
                                    duration: duration)
            fly.timingMode = .easeOut
            let fall = SKAction.moveBy(x: 0, y: -8, duration: duration)
            fall.timingMode = .easeIn
            dot.run(SKAction.sequence([
                SKAction.group([fly, fall, SKAction.fadeOut(withDuration: duration)]),
                SKAction.removeFromParent()
            ]))
        }

        // Onde annulaire pour les grandes chaînes.
        if intensity >= 4 {
            let ring = SKShapeNode(circleOfRadius: BubbleNode.bubbleRadius)
            ring.fillColor = .clear
            ring.strokeColor = color.mixedWithWhite(0.2)
            ring.lineWidth = 3
            ring.setScale(0.3)
            container.addChild(ring)
            let expand = SKAction.scale(to: 2.2, duration: 0.35)
            expand.timingMode = .easeOut
            ring.run(SKAction.sequence([
                SKAction.group([expand, SKAction.fadeOut(withDuration: 0.35)]),
                SKAction.removeFromParent()
            ]))
        }

        container.run(SKAction.sequence([
            SKAction.wait(forDuration: 0.5),
            SKAction.removeFromParent()
        ]))
        return container
    }
}

private extension UIColor {
    /// Mélange la couleur avec du blanc (0 = inchangée, 1 = blanc).
    func mixedWithWhite(_ amount: CGFloat) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        getRed(&r, green: &g, blue: &b, alpha: &a)
        return UIColor(red: r + (1 - r) * amount,
                       green: g + (1 - g) * amount,
                       blue: b + (1 - b) * amount,
                       alpha: a)
    }
}
