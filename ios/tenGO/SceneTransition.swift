//
//  SceneTransition.swift
//  tenGO
//
//  Transitions entre écrans, au même endroit pour tout le jeu.
//
//  Deux règles, et une seule exception :
//
//  1. Changer d'écran fond par le fond du thème actif. `SKTransition.fade
//     (withDuration:)` passe par le NOIR — d'où le clignotement sombre sur un
//     jeu posé sur du crème. On lui donne donc explicitement la couleur du
//     thème : la transition traverse la teinte que les deux écrans partagent
//     déjà, et rien ne s'assombrit.
//
//  2. Les deux scènes restent animées pendant la transition. Sinon le fond à
//     bulles se fige le temps du changement, et le mouvement paraît saccadé
//     alors qu'il ne l'est pas.
//
//  L'exception, ce sont les onglets : ils ne s'animent pas du tout (cf.
//  `TabBar.present`). Les quatre écrans de premier niveau forment un même
//  espace, pas une succession de pages — les animer les ferait paraître plus
//  éloignés qu'ils ne le sont.
//

import SpriteKit

enum SceneTransition {

    /// Fondu passant par le fond du thème actif. Transition par défaut du jeu.
    static func fade(_ duration: TimeInterval = 0.28) -> SKTransition {
        let transition = SKTransition.fade(with: ThemeManager.shared.active.background,
                                           duration: duration)
        transition.pausesIncomingScene = false
        transition.pausesOutgoingScene = false
        return transition
    }
}
