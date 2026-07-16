//
//  DailyChallenge.swift
//  tenGO
//
//  Défi du jour : une nouvelle grille déterministe chaque jour, disponible à
//  partir de 5h du matin (heure locale) — aucun backend.
//  Un « twist » perturbateur tourne chaque jour pour renouveler le défi.
//

import Foundation

/// Élément perturbateur du jour (rotation déterministe).
enum DailyTwist: Int, CaseIterable {
    case obstacles  // pierres inertes qui cassent l'adjacence et bloquent la gravité
    case anchored   // bulles qui ne tombent pas avec la gravité
    case frozen     // bulles givrées, débloquées par un chemin adjacent

    /// Nombre d'éléments perturbateurs placés sur la grille (intensité « corsée »).
    var count: Int {
        switch self {
        case .obstacles: return 9
        case .anchored:  return 6
        case .frozen:    return 6
        }
    }
}

enum DailyChallenge {

    /// Heure (locale) à laquelle un nouveau défi devient disponible.
    static let resetHour = 5

    /// Grille du jour + twist appliqué, prête à jouer.
    struct Today {
        let grid: GridModel
        let twist: DailyTwist
        let dayKey: Int
    }

    // MARK: - API

    /// Construit le défi du jour pour la date donnée (par défaut : maintenant).
    static func make(for date: Date = Date()) -> Today {
        let key = dayKey(for: date)
        let twist = DailyTwist.allCases[abs(key) % DailyTwist.allCases.count]
        let grid = solvableGrid(dayKey: key, twist: twist)
        return Today(grid: grid, twist: twist, dayKey: key)
    }

    /// Le défi du jour a-t-il déjà été complété aujourd'hui ?
    static func isCompletedToday(_ date: Date = Date()) -> Bool {
        let completed = UserDefaults.standard.integer(forKey: AppConfig.UserDefaultsKey.dailyLastCompletedDayKey)
        return completed == dayKey(for: date)
    }

    /// Marque le défi du jour comme complété.
    static func markCompleted(_ date: Date = Date()) {
        UserDefaults.standard.set(dayKey(for: date), forKey: AppConfig.UserDefaultsKey.dailyLastCompletedDayKey)
    }

    // MARK: - Génération déterministe validée

    /// Génère plusieurs candidates (graine dérivée déterministe) et sélectionne
    /// la plus RETORSE : le moins de paires/coups évidents (somme 10 en ≤ 2 cases),
    /// tout en gardant assez de matière pour scorer (« corsé mais juste »).
    /// Le joueur doit alors construire des chemins longs plutôt que cueillir des paires.
    private static func solvableGrid(dayKey: Int, twist: DailyTwist) -> GridModel {
        let base = UInt64(bitPattern: Int64(dayKey))
        let candidates = 80
        let materialFloor = 6   // nb min de coups courts (≤4) → la grille reste jouable

        var best: GridModel?
        var bestEasy = Int.max
        var bestTotal = 0
        var anyPlayable: GridModel?   // repli si aucune candidate n'atteint le seuil

        for offset in 0..<candidates {
            var generator = SeededGenerator(seed: base &+ UInt64(offset))
            var grid = GridModel(using: &generator)
            apply(twist, to: &grid, using: &generator)
            guard grid.isSolvable() else { continue }
            if anyPlayable == nil { anyPlayable = grid }

            let total = grid.countSumTenGroups(maxLen: 4)
            guard total >= materialFloor else { continue }
            let easy = grid.countSumTenGroups(maxLen: 2)   // paires évidentes
            // Le moins de paires possible ; à égalité, on garde le plus de matière.
            if easy < bestEasy || (easy == bestEasy && total > bestTotal) {
                bestEasy = easy
                bestTotal = total
                best = grid
            }
        }

        if let best { return best }
        if let anyPlayable { return anyPlayable }
        // Repli ultime (quasi impossible) : grille sans twist, déjà jouable.
        var generator = SeededGenerator(seed: base)
        return GridModel(using: &generator)
    }

    private static func apply<G: RandomNumberGenerator>(_ twist: DailyTwist, to grid: inout GridModel, using generator: inout G) {
        switch twist {
        case .obstacles: grid.placeObstacles(count: twist.count, using: &generator)
        case .anchored:  grid.anchorBubbles(count: twist.count, using: &generator)
        case .frozen:    grid.freezeBubbles(count: twist.count, using: &generator)
        }
    }

    // MARK: - Clé du jour (bascule à 5h, heure locale)

    /// Date → entier AAAAMMJJ. La « journée de défi » bascule à `resetHour`
    /// (heure locale) : avant 5h on est encore sur le défi de la veille.
    private static func dayKey(for date: Date) -> Int {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        let shifted = calendar.date(byAdding: .hour, value: -resetHour, to: date) ?? date
        let c = calendar.dateComponents([.year, .month, .day], from: shifted)
        return (c.year ?? 0) * 10000 + (c.month ?? 0) * 100 + (c.day ?? 0)
    }
}
