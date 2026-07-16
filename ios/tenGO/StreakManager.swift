//
//  StreakManager.swift
//  tenGO
//
//  Série de jours consécutifs de jeu — rétention « zen » : jouer un jour
//  prolonge la série, un jour manqué la remet simplement à 1, sans pénalité
//  ni message culpabilisant. Persistance locale (UserDefaults), aucun backend.
//

import Foundation

final class StreakManager {

    static let shared = StreakManager()
    private init() {}

    private let defaults = UserDefaults.standard

    /// Série en cours (jours consécutifs).
    var current: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.streakCurrent)
    }

    /// Meilleure série atteinte.
    var best: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.streakBest)
    }

    /// À appeler au lancement d'une partie. Met à jour la série selon le jour
    /// (calendrier local — c'est « aujourd'hui » du point de vue du joueur).
    func registerPlay(_ date: Date = Date()) {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: date)

        guard let lastInterval = lastPlayedInterval() else {
            setCurrent(1, last: today)
            return
        }
        let lastDay = calendar.startOfDay(for: Date(timeIntervalSinceReferenceDate: lastInterval))
        let gap = calendar.dateComponents([.day], from: lastDay, to: today).day ?? 0

        switch gap {
        case 0:
            return                       // déjà compté aujourd'hui
        case 1:
            setCurrent(current + 1, last: today)
        default:
            setCurrent(1, last: today)   // série rompue → on repart à 1, sans drame
        }
    }

    // MARK: - Private

    private func lastPlayedInterval() -> TimeInterval? {
        let stored = defaults.double(forKey: AppConfig.UserDefaultsKey.streakLastPlayed)
        return stored == 0 ? nil : stored
    }

    private func setCurrent(_ value: Int, last day: Date) {
        defaults.set(value, forKey: AppConfig.UserDefaultsKey.streakCurrent)
        defaults.set(max(value, best), forKey: AppConfig.UserDefaultsKey.streakBest)
        defaults.set(day.timeIntervalSinceReferenceDate, forKey: AppConfig.UserDefaultsKey.streakLastPlayed)
    }
}
