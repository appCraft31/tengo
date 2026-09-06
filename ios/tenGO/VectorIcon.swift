//
//  VectorIcon.swift
//  tenGO
//
//  Rendu des icônes vectorielles (SVG du catalogue) en nœuds SpriteKit,
//  teintées à la couleur voulue. Aucun emoji dans l'interface : un emoji
//  change de dessin d'une version d'iOS à l'autre et ignore la direction
//  artistique du jeu.
//
//  SpriteKit ne sait pas afficher un SVG directement. Le catalogue d'assets,
//  lui, conserve la représentation vectorielle (`preserves-vector-representation`)
//  et rend l'image nette à la taille demandée — c'est ce que fait cette
//  fabrique, déjà éprouvée par les icônes de boosters.
//

import SpriteKit
import UIKit

enum VectorIcon: String {
    case play     = "IconPlay"
    case progress = "IconProgress"
    case social   = "IconSocial"
    case shop     = "IconShop"
    case rush     = "IconRush"
    case duel     = "IconDuel"
    case puzzle   = "IconPuzzle"
    case flame    = "IconFlame"
    case shield   = "IconShield"

    /// Nœud dimensionné et teinté, prêt à être positionné.
    func node(size: CGFloat, color: UIColor) -> SKNode {
        VectorIconRenderer.make(assetName: rawValue, size: size, color: color)
    }
}

enum VectorIconRenderer {

    static func make(assetName: String, size: CGFloat, color: UIColor) -> SKNode {
        guard let base = UIImage(named: assetName)?.withRenderingMode(.alwaysTemplate) else {
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
