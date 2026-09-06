//
//  GridValidator.swift
//  tenGO
//
//  Validation et notation de difficulté d'une grille candidate (issue #21).
//
//  Pourquoi on ne valide pas « la grille est entièrement videable » comme le
//  suggère le GDD (§62) : chaque coup retire exactement 10 points de valeur,
//  donc vider une grille de 63 bulles suppose déjà que sa somme soit un
//  multiple de 10 (une grille aléatoire sur dix), et le prouver demande une
//  recherche sur 2^63 états. Ce n'est ni tenable sur l'appareil, ni le contrat
//  du Défi du jour, qui est un mode de score joué jusqu'au blocage — pas un
//  puzzle à vider (celui-là, c'est le mode Puzzles, dont les niveaux sont
//  petits et prouvés videables hors ligne par ios/tools/puzzle_gen.swift).
//
//  Le contrat de validation retenu est donc : la grille doit offrir une
//  partie riche et non triviale. On le MESURE en jouant réellement la grille
//  plusieurs fois au hasard, plutôt qu'en le supposant.
//

import Foundation

struct GridAssessment {
    /// Au moins un coup existe (critère minimal, déjà garanti par GridModel).
    let hasValidMove: Bool
    /// Paires immédiates (2 bulles) : beaucoup de paires = grille qui se lit
    /// toute seule.
    let easyPairs: Int
    /// Groupes courts (≤ 4 bulles) : la « matière » jouable de la grille.
    let shortGroups: Int
    /// Nombre de coups avant blocage, médiane sur plusieurs parties simulées.
    let medianMoves: Int
    /// Pire des parties simulées — une grille qui peut bloquer très tôt est
    /// frustrante même si sa médiane est bonne.
    let worstMoves: Int
    /// Score théorique médian d'une partie jouée au hasard.
    let medianScore: Int
    /// Difficulté 1-100 (cf. `difficultyScore`).
    let difficulty: Int

    /// Une grille est publiable si elle est jouable et offre une vraie partie.
    /// Le seuil de profondeur écarte les grilles qui bloquent presque
    /// immédiatement, que le seul `hasValidMove` laissait passer.
    var isPublishable: Bool {
        hasValidMove && worstMoves >= GridValidator.minimumWorstMoves && shortGroups >= GridValidator.minimumShortGroups
    }
}

enum GridValidator {

    /// En dessous, la grille peut se bloquer trop tôt pour être publiée.
    static let minimumWorstMoves = 4
    /// En dessous, il n'y a pas assez de matière jouable.
    static let minimumShortGroups = 6

    /// Longueur maximale explorée pendant les simulations. Les chaînes très
    /// longues sont rares et coûteuses à énumérer ; les plafonner ne change
    /// pas le classement relatif des grilles, qui est le seul usage ici.
    private static let simulationMaxLen = 5

    /// Joue `playouts` parties au hasard et en tire des métriques. Le RNG est
    /// injecté : à graine égale, l'évaluation d'une grille est reproductible.
    static func assess<G: RandomNumberGenerator>(_ grid: GridModel,
                                                 playouts: Int = 3,
                                                 using generator: inout G) -> GridAssessment {
        let easyPairs = grid.countSumTenGroups(maxLen: 2)
        let shortGroups = grid.countSumTenGroups(maxLen: 4)
        let hasMove = grid.hasValidMove()

        var moveCounts: [Int] = []
        var scores: [Int] = []
        for _ in 0..<max(1, playouts) {
            let result = simulate(grid, using: &generator)
            moveCounts.append(result.moves)
            scores.append(result.score)
        }
        moveCounts.sort()
        scores.sort()

        let median = moveCounts[moveCounts.count / 2]
        let worst = moveCounts.first ?? 0
        let medianScore = scores[scores.count / 2]

        return GridAssessment(
            hasValidMove: hasMove,
            easyPairs: easyPairs,
            shortGroups: shortGroups,
            medianMoves: median,
            worstMoves: worst,
            medianScore: medianScore,
            difficulty: difficultyScore(easyPairs: easyPairs, medianMoves: median, medianScore: medianScore)
        )
    }

    /// Une partie jouée au hasard jusqu'au blocage, avec les VRAIES règles
    /// (retrait du chemin puis gravité) — GridModel étant une struct, la copie
    /// locale ne touche pas la grille d'origine.
    private static func simulate<G: RandomNumberGenerator>(_ grid: GridModel,
                                                           using generator: inout G) -> (moves: Int, score: Int) {
        var board = grid
        var moves = 0
        var score = 0
        while true {
            let groups = board.sumTenGroups(maxLen: simulationMaxLen)
            guard let choice = groups.randomElement(using: &generator) else { break }
            score += scoreForPath(length: choice.count)
            board.removeBubbles(at: choice)
            _ = board.thawFrozenBubbles(adjacentTo: choice)
            _ = board.applyGravity()
            moves += 1
            if moves > 200 { break }   // garde-fou, jamais atteint en pratique
        }
        return (moves, score)
    }

    /// Miroir du barème de GameScene.scoreForPath — dupliqué ici pour que le
    /// validateur reste utilisable hors de la scène de jeu (outil d'audit).
    private static func scoreForPath(length: Int) -> Int {
        ScoreRules.points(forChain: length)
    }

    /// Difficulté 1-100. Trois composantes, toutes normalisées :
    /// - beaucoup de paires immédiates → grille facile à lire ;
    /// - beaucoup de coups avant blocage → grille indulgente ;
    /// - score théorique élevé → grille généreuse.
    ///
    /// Les bornes sont les 5e et 95e centiles MESURÉS sur 400 grilles
    /// candidates (twists compris) par ios/tools/grid_audit. Des bornes
    /// devinées donnaient un score saturé entre 74 et 95 pour toutes les
    /// grilles, donc inutilisable pour viser une difficulté.
    static func difficultyScore(easyPairs: Int, medianMoves: Int, medianScore: Int) -> Int {
        func normalized(_ value: Int, easy: Int, hard: Int) -> Double {
            // 0 = aussi facile que `easy`, 1 = aussi dur que `hard`.
            let clamped = min(max(Double(value), Double(min(easy, hard))), Double(max(easy, hard)))
            return abs(clamped - Double(easy)) / abs(Double(hard) - Double(easy))
        }

        let pairsPart = normalized(easyPairs, easy: 27, hard: 12)
        let movesPart = normalized(medianMoves, easy: 17, hard: 10)
        let scorePart = normalized(medianScore, easy: 620, hard: 230)

        let raw = 0.45 * pairsPart + 0.35 * movesPart + 0.20 * scorePart
        return min(100, max(1, Int((raw * 100).rounded())))
    }
}
