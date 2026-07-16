//
//  TrailRenderer.swift
//  tenGO
//
//  Rendu de la ligne de tracé selon le style cosmétique (réutilisé par le jeu
//  et par les aperçus de la boutique).
//

import SpriteKit

enum TrailRenderer {

    static func make(path: CGPath, style: TrailStyle, accent: UIColor) -> SKNode {
        let container = SKNode()

        func stroke(_ color: UIColor, width: CGFloat, dashed: Bool = false) -> SKShapeNode {
            let p = dashed ? path.copy(dashingWithPhase: 0, lengths: [2, 14]) : path
            let line = SKShapeNode(path: p)
            line.strokeColor = color
            line.lineWidth = width
            line.lineCap = .round
            line.lineJoin = .round
            line.fillColor = .clear
            return line
        }

        switch style.kind {
        case .classic:
            container.addChild(stroke(UIColor(white: 1.0, alpha: 0.6), width: 6))
        case .neon:
            container.addChild(stroke(accent.withAlphaComponent(0.25), width: 16)) // halo
            container.addChild(stroke(accent, width: 5))
        case .ribbon:
            container.addChild(stroke(accent.withAlphaComponent(0.45), width: 14))
        case .dotted:
            container.addChild(stroke(UIColor(white: 1.0, alpha: 0.9), width: 7, dashed: true))
        case .ink:
            container.addChild(stroke(UIColor(white: 0.18, alpha: 0.7), width: 7))
        }
        return container
    }
}
