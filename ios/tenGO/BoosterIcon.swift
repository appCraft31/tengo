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
        // Même fabrique que le reste des icônes du jeu (cf. VectorIcon).
        VectorIconRenderer.make(assetName: booster.assetName, size: size, color: color)
    }
}
