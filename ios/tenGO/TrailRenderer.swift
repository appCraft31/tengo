//
//  TrailRenderer.swift
//  tenGO
//
//  Rendu de la ligne de tracé selon le style cosmétique (réutilisé par le jeu
//  et par les aperçus de la boutique).
//

import SpriteKit

enum TrailRenderer {

    /// `tension` ∈ [0,1] = progression du chemin vers 10. Le trait s'épaissit
    /// et s'éclaircit à mesure qu'on approche.
    ///
    /// La rampe est volontairement NON linéaire (`pow(t, 2.2)`) : l'écart reste
    /// imperceptible jusqu'aux trois quarts, et ne devient lisible que dans le
    /// dernier quart. Sans cela, le trait deviendrait un compteur déguisé et
    /// ferait le calcul à la place du joueur — ce que le jeu refuse par principe
    /// (cf. `updateSumLabel`, volontairement vide).
    ///
    /// La valeur par défaut existe pour les aperçus de la boutique
    /// (`BoutiqueScene.addTrailCard`), qui n'ont pas de notion de tension.
    static func make(path: CGPath,
                     style: TrailStyle,
                     accent: UIColor,
                     tension: CGFloat = 0) -> SKNode {
        let container = SKNode()
        let t = pow(min(1, max(0, tension)), 2.2)
        let extraWidth = 2.5 * t
        let lift = 0.35 * t

        func stroke(_ color: UIColor, width: CGFloat, dashed: Bool = false) -> SKShapeNode {
            let p = dashed ? path.copy(dashingWithPhase: 0, lengths: [2, 14]) : path
            let line = SKShapeNode(path: p)
            line.strokeColor = color.mixedWithWhite(lift)
            line.lineWidth = width + extraWidth
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
