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
        /// Difficulté mesurée de la grille retenue (1-100, cf. GridValidator).
        let difficulty: Int
    }

    /// Difficulté visée, en rotation sur la semaine (GDD §61 : Daily Easy /
    /// Medium / Hard). Déterministe : tous les joueurs ont la même cible le
    /// même jour, comme la grille elle-même.
    enum Target: Int, CaseIterable {
        case easy = 30, medium = 55, hard = 78

        static func forDay(_ dayKey: Int) -> Target {
            // 4 jours faciles/moyens pour 3 durs sur la semaine : le Défi doit
            // rester une habitude quotidienne, pas un mur.
            let cycle: [Target] = [.easy, .medium, .hard, .medium, .easy, .hard, .medium]
            return cycle[abs(dayKey) % cycle.count]
        }
    }

    // MARK: - API

    /// Construit le défi du jour pour la date donnée (par défaut : maintenant).
    /// Grille du jour déjà construite (la validation coûte quelques dizaines
    /// de millisecondes ; la refaire à chaque navigation serait gâché).
    /// Accès main-thread uniquement, comme tout le reste de la navigation.
    private static var cached: Today?

    static func make(for date: Date = Date()) -> Today {
        let key = dayKey(for: date)
        if let cached, cached.dayKey == key { return cached }
        let twist = DailyTwist.allCases[abs(key) % DailyTwist.allCases.count]
        let selected = solvableGrid(dayKey: key, twist: twist)
        let today = Today(grid: selected.grid, twist: twist, dayKey: key, difficulty: selected.difficulty)
        cached = today
        return today
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

    /// Génère des candidates déterministes, les VALIDE, et retient celle dont
    /// la difficulté mesurée est la plus proche de la cible du jour.
    ///
    /// L'ancienne version prenait systématiquement la plus retorse : toutes
    /// les grilles sortaient alors en haut de l'échelle (74-95 sur 100,
    /// mesuré), sans aucune respiration d'un jour à l'autre.
    ///
    /// Coût maîtrisé : le tri grossier (paires, matière) se fait sur les 80
    /// candidates ; les simulations de partie, elles, ne tournent que sur les
    /// meilleures — c'est ce qui rend la mesure abordable sur l'appareil.
    private static func solvableGrid(dayKey: Int, twist: DailyTwist) -> (grid: GridModel, difficulty: Int) {
        let base = UInt64(bitPattern: Int64(dayKey))
        let candidates = 80
        let finalists = 6
        let materialFloor = GridValidator.minimumShortGroups
        let target = Target.forDay(dayKey).rawValue

        var pool: [(grid: GridModel, easy: Int)] = []
        var anyPlayable: GridModel?

        for offset in 0..<candidates {
            var generator = SeededGenerator(seed: base &+ UInt64(offset))
            var grid = GridModel(using: &generator)
            apply(twist, to: &grid, using: &generator)
            guard grid.isSolvable() else { continue }
            if anyPlayable == nil { anyPlayable = grid }
            guard grid.countSumTenGroups(maxLen: 4) >= materialFloor else { continue }
            pool.append((grid, grid.countSumTenGroups(maxLen: 2)))
        }

        // Finalistes : un échantillon étalé sur toute la plage de « paires
        // évidentes », pour que la cible facile comme la cible dure trouve
        // chacune une candidate plausible.
        pool.sort { $0.easy < $1.easy }
        var picked: [GridModel] = []
        if !pool.isEmpty {
            let step = max(1, pool.count / finalists)
            for index in stride(from: 0, to: pool.count, by: step) where picked.count < finalists {
                picked.append(pool[index].grid)
            }
        }

        var best: (grid: GridModel, difficulty: Int)?
        var bestDistance = Int.max
        for grid in picked {
            var rng = SeededGenerator(seed: base &+ 0xD1FF)
            let assessment = GridValidator.assess(grid, playouts: 3, using: &rng)
            guard assessment.isPublishable else { continue }
            let distance = abs(assessment.difficulty - target)
            if distance < bestDistance {
                bestDistance = distance
                best = (grid, assessment.difficulty)
            }
        }

        if let best { return best }
        // Aucune candidate n'a passé la validation : on garde une grille
        // jouable plutôt que de priver le joueur de son défi.
        if let anyPlayable { return (anyPlayable, target) }
        var generator = SeededGenerator(seed: base)
        return (GridModel(using: &generator), target)
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
