//
//  ScoreRules.swift
//  tenGO
//
//  Barème du score, source unique. Il vivait en double — dans `GameScene` et
//  dans `GridValidator` — alors que le second sert justement à MESURER le
//  premier : deux copies qui divergent, et le ciblage de difficulté du Défi
//  du jour se met à mentir sans que rien n'échoue.
//
//  Principe (GDD §34) : une chaîne longue doit valoir bien plus que la somme
//  des courtes qu'elle remplace. Une propriété du jeu rend ce choix nécessaire
//  plutôt que décoratif — chaque coup retire exactement 10 points de valeur du
//  plateau, donc vider une grille demande toujours le même nombre de coups.
//  Sans récompense superlinéaire, jouer fin ne rapporterait rien de plus que
//  jouer au plus court.
//

import Foundation

enum ScoreRules {

    /// Points d'une chaîne de `length` bulles.
    ///
    /// Jusqu'à 4, le barème est inchangé depuis la sortie du jeu : ce sont les
    /// coups courants, et y toucher aurait décalé l'échelle de tous les scores
    /// existants — donc invalidé les records des joueurs. Au-delà, la
    /// progression devient quadratique là où elle était linéaire (+50 par
    /// bulle), ce qui était précisément le défaut : passé 4 bulles, allonger
    /// une chaîne ne rapportait plus rien de particulier.
    ///
    ///     2 → 10      5 → 200     8 → 800
    ///     3 → 30      6 → 350     9 → 1100
    ///     4 → 100     7 → 550    10 → 1450
    static func points(forChain length: Int) -> Int {
        switch length {
        case ..<2: return 0
        case 2:    return 10
        case 3:    return 30
        default:
            let k = length - 4
            return 100 + 100 * k + 25 * k * (k - 1)
        }
    }

    /// Multiplicateur affiché au joueur pour une chaîne, arrondi au dixième.
    /// C'est une lecture du barème ci-dessus, pas un second calcul : la
    /// référence est ce que vaudrait la même longueur payée 10 points la bulle.
    static func displayMultiplier(forChain length: Int) -> Double {
        guard length >= 2 else { return 0 }
        let flat = Double(length * 10)
        return (Double(points(forChain: length)) / flat * 10).rounded() / 10
    }
}
