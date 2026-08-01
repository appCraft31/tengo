//
//  PopEffects.swift
//  tenGO
//
//  Effets de destruction des bulles : éclats, ondes, flash.
//  Nœuds éphémères auto-détruits, indépendants des BubbleNode — leur durée
//  ne retarde jamais la gravité (le wait(0.26) de commitPath reste maître).
//
//  Les éclats sont des SKSpriteNode partageant FXTextures.dot : SpriteKit les
//  batche en un seul draw call, là où autant de SKShapeNode en coûtaient un
//  chacun. C'est ce qui permet d'enrichir l'effet tout en allégeant le rendu.
//

import SpriteKit

enum PopEffects {

    /// Éclatement à la position d'une bulle détruite.
    /// Le palier porte toute l'escalade : une chaîne de 6 doit se voir
    /// immédiatement comme une chaîne de 6, pas comme deux paires.
    static func makeBurst(color: UIColor, tier: PopTier) -> SKNode {
        let container = SKNode()
        container.zPosition = 15

        addFlash(to: container, color: color, tier: tier)
        addSparks(to: container, color: color, tier: tier)
        addRings(to: container, color: color, tier: tier)

        container.run(SKAction.sequence([
            SKAction.wait(forDuration: 0.6),
            SKAction.removeFromParent()
        ]))
        return container
    }

    // MARK: - Flash central

    private static func addFlash(to container: SKNode, color: UIColor, tier: PopTier) {
        let flash = SKSpriteNode(texture: FXTextures.dot)
        flash.size = CGSize(width: BubbleNode.bubbleRadius * 2.2,
                            height: BubbleNode.bubbleRadius * 2.2)
        flash.color = .white
        flash.colorBlendFactor = 1
        flash.alpha = 0.55
        flash.blendMode = .add
        container.addChild(flash)
        flash.run(SKAction.sequence([
            SKAction.group([
                SKAction.scale(to: 1.6, duration: 0.15),
                SKAction.fadeOut(withDuration: 0.15)
            ]),
            SKAction.removeFromParent()
        ]))

        // Halo teinté : présent à TOUS les paliers — c'est ce qui donne du
        // corps aux chaînes courtes, qui représentent l'essentiel des coups.
        let haloSpan: CGFloat = tier == .small ? 2.6 : 3.4
        let halo = SKSpriteNode(texture: FXTextures.dot)
        halo.size = CGSize(width: BubbleNode.bubbleRadius * haloSpan,
                           height: BubbleNode.bubbleRadius * haloSpan)
        halo.color = color
        halo.colorBlendFactor = 1
        halo.alpha = tier == .small ? 0.32 : 0.40
        halo.blendMode = .add
        container.addChild(halo)
        let growTarget: CGFloat = {
            switch tier {
            case .small: return 1.4
            case .medium: return 1.6
            case .large: return 2.1
            }
        }()
        let grow = SKAction.scale(to: growTarget, duration: 0.28)
        grow.timingMode = .easeOut
        halo.run(SKAction.sequence([
            SKAction.group([grow, SKAction.fadeOut(withDuration: 0.28)]),
            SKAction.removeFromParent()
        ]))
    }

    // MARK: - Éclats radiaux

    private static func addSparks(to container: SKNode, color: UIColor, tier: PopTier) {
        let count = max(4, Int(CGFloat(tier.burstCount) * JuiceSettings.particleScale))
        for _ in 0..<count {
            let size = CGFloat.random(in: 6...14)
            let dot = SKSpriteNode(texture: FXTextures.dot)
            dot.size = CGSize(width: size, height: size)
            dot.color = color.mixedWithWhite(CGFloat.random(in: 0...0.35))
            dot.colorBlendFactor = 1
            dot.alpha = 0.9
            container.addChild(dot)

            let angle = CGFloat.random(in: 0..<(2 * .pi))
            // Les grandes chaînes projettent plus loin : l'énergie se lit
            // autant dans la portée que dans le nombre.
            let reach: ClosedRange<CGFloat> = {
                switch tier {
                case .small: return 30...66
                case .medium: return 38...84
                case .large: return 46...108
                }
            }()
            let dist = CGFloat.random(in: reach)
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
    }

    // MARK: - Ondes annulaires

    private static func addRings(to container: SKNode, color: UIColor, tier: PopTier) {
        guard tier.ringCount > 0 else { return }
        for i in 0..<tier.ringCount {
            let ring = SKSpriteNode(texture: FXTextures.ring)
            ring.size = CGSize(width: BubbleNode.bubbleRadius * 2,
                               height: BubbleNode.bubbleRadius * 2)
            ring.color = color.mixedWithWhite(0.2)
            ring.colorBlendFactor = 1
            ring.setScale(0.3)
            container.addChild(ring)

            let delay = TimeInterval(i) * 0.09
            let expand = SKAction.scale(to: i == 0 ? 2.2 : 3.0, duration: 0.35)
            expand.timingMode = .easeOut
            ring.run(SKAction.sequence([
                SKAction.wait(forDuration: delay),
                SKAction.group([expand, SKAction.fadeOut(withDuration: 0.35)]),
                SKAction.removeFromParent()
            ]))
        }
    }
}

extension UIColor {
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
