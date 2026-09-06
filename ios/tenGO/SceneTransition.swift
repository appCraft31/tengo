//
//  SceneTransition.swift
//  tenGO
//
//  Transitions entre écrans, au même endroit pour tout le jeu.
//
//  `SKTransition.fade(withDuration:)` ne fait PAS un fondu enchaîné : il
//  fond vers le noir, puis depuis le noir. D'où le clignotement sombre à
//  chaque changement d'écran, d'autant plus visible que le jeu est posé sur
//  un fond crème. `crossFade` croise directement les deux scènes.
//
//  Les deux scènes restent animées pendant la transition (`pausesIncomingScene`
//  et `pausesOutgoingScene` à false) : sinon le fond à bulles se fige le temps
//  du changement, et le mouvement paraît saccadé alors qu'il ne l'est pas.
//

import SpriteKit

enum SceneTransition {

    /// Fondu enchaîné : les deux écrans se croisent, sans passage au noir.
    /// C'est la transition par défaut du jeu.
    static func crossFade(_ duration: TimeInterval = 0.28) -> SKTransition {
        live(SKTransition.crossFade(withDuration: duration))
    }

    /// Glissement horizontal, réservé à la navigation par onglets : le sens du
    /// mouvement dit où l'on se déplace dans la rangée.
    static func slide(from direction: SKTransitionDirection,
                      duration: TimeInterval = 0.26) -> SKTransition {
        live(SKTransition.push(with: direction, duration: duration))
    }

    private static func live(_ transition: SKTransition) -> SKTransition {
        transition.pausesIncomingScene = false
        transition.pausesOutgoingScene = false
        return transition
    }
}
