//
//  BoosterIcon.swift
//  tenGO
//
//  Fabrique les icônes de boosters à partir des assets vectoriels (SVG) du
//  catalogue, teintées dans la couleur de la DA. Rendu net à la taille voulue
//  (le vectoriel est rastérisé à l'échelle écran). Aucun emoji.
//

import SpriteKit
import UIKit

enum BoosterIcon {

    /// Nœud SpriteKit de l'icône d'un booster, dimensionné et teinté.
    static func make(_ booster: Booster, size: CGFloat, color: UIColor) -> SKNode {
        guard let base = UIImage(named: booster.assetName)?.withRenderingMode(.alwaysTemplate) else {
            return SKNode()
        }
        let dimension = CGSize(width: size, height: size)
        let renderer = UIGraphicsImageRenderer(size: dimension)
        let tinted = renderer.image { _ in
            base.withTintColor(color, renderingMode: .alwaysOriginal)
                .draw(in: CGRect(origin: .zero, size: dimension))
        }
        let sprite = SKSpriteNode(texture: SKTexture(image: tinted))
        sprite.size = dimension
        return sprite
    }
}
