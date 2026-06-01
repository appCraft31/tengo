//
//  DailyChallenge.swift
//  tenGO
//
//  Défi du jour : une grille identique pour tous les joueurs chaque jour,
//  générée de façon déterministe à partir de la date (UTC) — aucun backend.
//  Un « twist » perturbateur tourne chaque jour pour renouveler le défi.
//

import Foundation

/// Élément perturbateur du jour (rotation déterministe).
enum DailyTwist: Int, CaseIterable {
    case obstacles  // pierres inertes qui cassent l'adjacence et bloquent la gravité
    case anchored   // bulles qui ne tombent pas avec la gravité
    case frozen     // bulles givrées, débloquées par un chemin adjacent

    /// Nombre d'éléments perturbateurs placés sur la grille.
    var count: Int {
        switch self {
        case .obstacles: return 6
        case .anchored:  return 4
        case .frozen:    return 4
        }
    }
}

enum DailyChallenge {

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

    /// Génère la grille + twist et garantit qu'elle est jouable en dérivant la
    /// graine (key, key+1, …) jusqu'à obtenir une grille résoluble.
    private static func solvableGrid(dayKey: Int, twist: DailyTwist) -> GridModel {
        let base = UInt64(bitPattern: Int64(dayKey))
        for offset in 0..<200 {
            var generator = SeededGenerator(seed: base &+ UInt64(offset))
            var grid = GridModel(using: &generator)
            apply(twist, to: &grid, using: &generator)
            if grid.isSolvable() {
                return grid
            }
        }
        // Repli (quasi impossible) : grille sans twist, déjà validée jouable.
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

    // MARK: - Clé du jour (UTC pour l'équité entre fuseaux horaires)

    /// Date → entier AAAAMMJJ en UTC (ex. 2026-06-01 → 20260601).
    private static func dayKey(for date: Date) -> Int {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC") ?? .current
        let c = calendar.dateComponents([.year, .month, .day], from: date)
        return (c.year ?? 0) * 10000 + (c.month ?? 0) * 100 + (c.day ?? 0)
    }
}
