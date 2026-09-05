//
//  MissionManager.swift
//  tenGO
//
//  Missions quotidiennes : 3 missions + 1 super mission, tirées chaque jour
//  (bascule à 5h locale, comme le Défi du jour) dans un catalogue fixe,
//  sélection déterministe (même graine que le Défi du jour → mêmes missions
//  si l'app est réinstallée le même jour). Persistance locale (UserDefaults),
//  aucun backend — même pattern que CoinManager/StreakManager/LevelManager.
//

import Foundation

/// Ce qui est compté par une mission.
enum MissionKind: String, Codable {
    /// Atteindre une chaîne d'au moins `param` bulles, `target` fois dans la journée.
    case chainAtLeast
    /// Score cumulé sur la journée, toutes parties confondues.
    case cumulativeScore
    /// Nombre de parties terminées dans la journée.
    case gamesPlayed
    /// Nombre de grilles entièrement vidées dans la journée.
    case perfectBoards
    /// Nombre de chaînes validées (chemins commis) dans la journée.
    case movesPlayed
}

/// Définition figée d'une mission (le catalogue ne change qu'entre versions
/// de l'app ; seul l'état — valeur atteinte, réclamée ou non — est persisté).
struct MissionDefinition {
    let id: String
    let kind: MissionKind
    /// Seuil de chaîne pour `.chainAtLeast` ; ignoré pour les autres natures.
    let param: Int
    let target: Int
    let coinReward: Int
    let isSuper: Bool
    /// Clé de localisation du libellé. Format attendu selon `kind` :
    /// chainAtLeast → (target, param) ; les autres → (target).
    let titleKey: String
}

/// Récompenses calibrées sur l'économie existante (~60 pièces/jour en 2.3.0)
/// et sur le catalogue boutique (1 400 pièces pour tous les thèmes) : une
/// première version distribuait jusqu'à 195 pièces/jour, rendant l'achat de
/// pièces sans objet.
enum MissionCatalog {
    static let regular: [MissionDefinition] = [
        MissionDefinition(id: "chain5x2", kind: .chainAtLeast, param: 5, target: 2, coinReward: 12, isSuper: false, titleKey: "mission.chain_at_least"),
        MissionDefinition(id: "chain7x1", kind: .chainAtLeast, param: 7, target: 1, coinReward: 12, isSuper: false, titleKey: "mission.chain_at_least"),
        MissionDefinition(id: "score3000", kind: .cumulativeScore, param: 0, target: 3000, coinReward: 10, isSuper: false, titleKey: "mission.cumulative_score"),
        MissionDefinition(id: "score6000", kind: .cumulativeScore, param: 0, target: 6000, coinReward: 18, isSuper: false, titleKey: "mission.cumulative_score"),
        MissionDefinition(id: "games2", kind: .gamesPlayed, param: 0, target: 2, coinReward: 10, isSuper: false, titleKey: "mission.games_played"),
        MissionDefinition(id: "games3", kind: .gamesPlayed, param: 0, target: 3, coinReward: 15, isSuper: false, titleKey: "mission.games_played"),
        MissionDefinition(id: "perfect1", kind: .perfectBoards, param: 0, target: 1, coinReward: 20, isSuper: false, titleKey: "mission.perfect_boards"),
        MissionDefinition(id: "moves20", kind: .movesPlayed, param: 0, target: 20, coinReward: 12, isSuper: false, titleKey: "mission.moves_played"),
        MissionDefinition(id: "moves35", kind: .movesPlayed, param: 0, target: 35, coinReward: 18, isSuper: false, titleKey: "mission.moves_played"),
    ]

    static let superMissions: [MissionDefinition] = [
        MissionDefinition(id: "chain15x1", kind: .chainAtLeast, param: 15, target: 1, coinReward: 45, isSuper: true, titleKey: "mission.chain_at_least"),
        MissionDefinition(id: "score15000", kind: .cumulativeScore, param: 0, target: 15000, coinReward: 45, isSuper: true, titleKey: "mission.cumulative_score"),
    ]

    static func definition(for id: String) -> MissionDefinition? {
        (regular + superMissions).first { $0.id == id }
    }
}

final class MissionManager {

    static let shared = MissionManager()
    private init() {}

    private let defaults = UserDefaults.standard
    /// Même bascule journalière que le Défi du jour : cohérence d'une seule
    /// notion de « jour » dans tout le jeu.
    private static let resetHour = DailyChallenge.resetHour

    static let missionsPerDay = 3
    static let xpReward = 15
    static let xpSuperReward = 40

    private struct Entry: Codable {
        let id: String
        var value: Int
        var claimed: Bool
    }

    struct DisplayMission {
        let definition: MissionDefinition
        let progress: Int
        var isCompleted: Bool { progress >= definition.target }
        let claimed: Bool
    }

    // MARK: - Lecture pour l'UI

    /// Missions du jour (3 régulières + 1 super), dans un ordre stable.
    var todaysMissions: [DisplayMission] {
        rolloverIfNeeded()
        return loadEntries().compactMap { entry in
            guard let def = MissionCatalog.definition(for: entry.id) else { return nil }
            return DisplayMission(definition: def, progress: entry.value, claimed: entry.claimed)
        }
    }

    /// Au moins une mission complétée mais pas encore réclamée → badge Home.
    var hasUnclaimedReward: Bool {
        todaysMissions.contains { $0.isCompleted && !$0.claimed }
    }

    /// Réclame la récompense d'une mission complétée. Retourne les pièces
    /// créditées (0 si déjà réclamée ou pas encore complétée).
    @discardableResult
    func claim(_ id: String) -> Int {
        rolloverIfNeeded()
        var entries = loadEntries()
        guard let index = entries.firstIndex(where: { $0.id == id }),
              !entries[index].claimed,
              let def = MissionCatalog.definition(for: id),
              entries[index].value >= def.target else { return 0 }

        entries[index].claimed = true
        saveEntries(entries)
        CoinManager.shared.add(def.coinReward)
        LevelManager.shared.awardForMission(isSuper: def.isSuper)
        // Le Streak Shield doit rester rare : seule la super mission peut en
        // offrir un, et seulement 1 fois sur 4.
        if def.isSuper && Int.random(in: 0..<4) == 0 {
            StreakManager.shared.addShield(1)
        }
        return def.coinReward
    }

    // MARK: - Rapporté par GameScene, en direct pendant la partie

    /// Un chemin d'au moins `length` bulles vient d'être validé.
    func reportChain(length: Int) {
        rolloverIfNeeded()
        updateEntries { def, value in
            guard def.kind == .chainAtLeast, length >= def.param, value < def.target else { return value }
            return value + 1
        }
    }

    /// Un chemin (quel qu'il soit) vient d'être validé.
    func reportMove() {
        rolloverIfNeeded()
        updateEntries { def, value in
            def.kind == .movesPlayed ? value + 1 : value
        }
    }

    /// `delta` points viennent d'être marqués (appelé au fil de la partie,
    /// pas seulement à la fin — la mission progresse en direct).
    func reportScore(_ delta: Int) {
        guard delta > 0 else { return }
        rolloverIfNeeded()
        updateEntries { def, value in
            def.kind == .cumulativeScore ? value + delta : value
        }
    }

    /// Une grille vient d'être entièrement vidée. Séparé de la fin de partie :
    /// en Rush, le joueur peut vider plusieurs grilles dans la même partie.
    func reportPerfectBoard() {
        rolloverIfNeeded()
        updateEntries { def, value in
            def.kind == .perfectBoards ? value + 1 : value
        }
    }

    /// Fin de partie (quel que soit le mode) : compte pour gamesPlayed et,
    /// si la grille a été vidée, pour perfectBoards.
    func reportGameEnded(isPerfect: Bool) {
        rolloverIfNeeded()
        updateEntries { def, value in
            switch def.kind {
            case .gamesPlayed: return value + 1
            case .perfectBoards: return isPerfect ? value + 1 : value
            default: return value
            }
        }
    }

    // MARK: - Private : état persisté

    private func updateEntries(_ transform: (MissionDefinition, Int) -> Int) {
        var entries = loadEntries()
        var changed = false
        for i in entries.indices {
            guard let def = MissionCatalog.definition(for: entries[i].id), !entries[i].claimed else { continue }
            let newValue = min(def.target, transform(def, entries[i].value))
            if newValue != entries[i].value {
                entries[i].value = newValue
                changed = true
            }
        }
        if changed { saveEntries(entries) }
    }

    private func loadEntries() -> [Entry] {
        guard let data = defaults.data(forKey: AppConfig.UserDefaultsKey.missionsProgress),
              let entries = try? JSONDecoder().decode([Entry].self, from: data) else { return [] }
        return entries
    }

    private func saveEntries(_ entries: [Entry]) {
        guard let data = try? JSONEncoder().encode(entries) else { return }
        defaults.set(data, forKey: AppConfig.UserDefaultsKey.missionsProgress)
    }

    /// Régénère le jeu de missions du jour si la clé du jour a changé.
    /// Les missions non réclamées de la veille sont simplement perdues —
    /// cohérent avec l'esprit « one day, one set » du Défi du jour.
    private func rolloverIfNeeded(_ date: Date = Date()) {
        let key = MissionManager.dayKey(for: date)
        let storedKey = defaults.integer(forKey: AppConfig.UserDefaultsKey.missionsDayKey)
        // STRICTEMENT supérieur : avec une simple inégalité, reculer l'horloge
        // d'un jour régénérait le même jeu de missions avec `claimed: false`,
        // et les récompenses (dont les 100 pièces de la super mission)
        // devenaient farmables à l'infini.
        guard key > storedKey else { return }

        var generator = SeededGenerator(seed: UInt64(bitPattern: Int64(key)) &+ 0x4D_49_53)
        let chosenRegular = Array(MissionCatalog.regular.shuffled(using: &generator).prefix(MissionManager.missionsPerDay))
        let chosenSuper = MissionCatalog.superMissions.randomElement(using: &generator)

        let entries = (chosenRegular + [chosenSuper].compactMap { $0 })
            .map { Entry(id: $0.id, value: 0, claimed: false) }

        saveEntries(entries)
        defaults.set(key, forKey: AppConfig.UserDefaultsKey.missionsDayKey)
    }

    /// Date → entier AAAAMMJJ, bascule à `resetHour` (même logique que DailyChallenge).
    private static func dayKey(for date: Date) -> Int {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        let shifted = calendar.date(byAdding: .hour, value: -resetHour, to: date) ?? date
        let c = calendar.dateComponents([.year, .month, .day], from: shifted)
        return (c.year ?? 0) * 10000 + (c.month ?? 0) * 100 + (c.day ?? 0)
    }
}
