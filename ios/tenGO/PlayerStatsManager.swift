//
//  PlayerStatsManager.swift
//  tenGO
//
//  Statistiques de progression du joueur, cumulées sur toute la durée de
//  vie de l'app (contrairement aux stats de fin de partie, propres à une
//  seule partie). Socle du Profil (#11). Persistance locale (UserDefaults),
//  aucun backend — même pattern que CoinManager/StreakManager.
//

import Foundation

final class PlayerStatsManager {

    static let shared = PlayerStatsManager()
    private init() {}

    private let defaults = UserDefaults.standard

    // MARK: - Lecture

    /// Plus longue chaîne jamais réalisée, tous modes confondus.
    var bestChainEver: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.statsBestChain)
    }

    /// Nombre total de grilles entièrement vidées (mode normal + Rush).
    var perfectBoardsTotal: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.statsPerfectTotal)
    }

    /// Nombre total de chaînes validées (chemins commis), tous modes confondus.
    var totalChainsMade: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.statsTotalChains)
    }

    /// Nombre total de parties Rush terminées.
    var totalRushGames: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.statsTotalRushGames)
    }

    /// Nombre total de Défis du jour relevés.
    var totalDailyCompletions: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.statsTotalDaily)
    }

    // MARK: - Rapporté par GameScene

    /// Un chemin de `length` bulles vient d'être validé (tous modes hors démo).
    func reportChain(length: Int) {
        defaults.set(defaults.integer(forKey: AppConfig.UserDefaultsKey.statsTotalChains) + 1,
                     forKey: AppConfig.UserDefaultsKey.statsTotalChains)
        if length > bestChainEver {
            defaults.set(length, forKey: AppConfig.UserDefaultsKey.statsBestChain)
        }
    }

    /// Une grille vient d'être entièrement vidée. Séparé de la fin de partie :
    /// en Rush, le joueur peut vider plusieurs grilles dans la même partie.
    func reportPerfectBoard() {
        defaults.set(perfectBoardsTotal + 1, forKey: AppConfig.UserDefaultsKey.statsPerfectTotal)
    }

    /// Fin d'une partie (normal, daily ou rush). `isPerfect` : grille vidée.
    func reportGameEnded(mode: GameScene.Mode, isPerfect: Bool) {
        if isPerfect {
            reportPerfectBoard()
        }
        switch mode {
        case .rush:
            defaults.set(totalRushGames + 1, forKey: AppConfig.UserDefaultsKey.statsTotalRushGames)
        case .daily:
            defaults.set(totalDailyCompletions + 1, forKey: AppConfig.UserDefaultsKey.statsTotalDaily)
        case .normal, .demo:
            break
        }
    }
}
