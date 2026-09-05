//
//  AchievementManager.swift
//  tenGO
//
//  Succès déblocables : jalons de compétence (chaînes, grilles parfaites,
//  score, série). Moteur générique — un succès est une condition dérivée
//  de statistiques déjà trackées (PlayerStatsManager, GameState,
//  StreakManager), jamais un état à part : le déblocage se déduit, seule
//  la liste des succès déjà crédités/notifiés est persistée (anti-farm et
//  anti-spam de toast). Aucun backend.
//
//  Catégorie Duel absente du catalogue : le mode Duel (#23) n'est pas
//  encore construit.
//

import Foundation

enum AchievementCategory: String, CaseIterable {
    case chain, perfect, score, daily

    var titleKey: String {
        switch self {
        case .chain: return "achievement.category.chain"
        case .perfect: return "achievement.category.perfect"
        case .score: return "achievement.category.score"
        case .daily: return "achievement.category.daily"
        }
    }

    var icon: String {
        switch self {
        case .chain: return "link"
        case .perfect: return "sparkles"
        case .score: return "star.fill"
        case .daily: return "flame.fill"
        }
    }
}

struct AchievementDefinition {
    let id: String
    let category: AchievementCategory
    let target: Int
    /// Clé de format (%d = target) pour le titre et la description.
    let titleKey: String
    let descKey: String
    let coinReward: Int
    let xpReward: Int
    /// Valeur actuelle de la statistique suivie — recalculée à la volée,
    /// jamais persistée par ce moteur (la source de vérité reste le manager
    /// d'origine : PlayerStatsManager, GameState, StreakManager…).
    let currentValue: () -> Int
}

enum AchievementManager {

    static let catalog: [AchievementDefinition] = {
        let chainTargets = [5, 10, 15, 20, 25]
        let perfectTargets = [1, 5, 10, 25, 50]
        let scoreZenTargets = [1000, 5000, 10000, 20000, 50000]
        let scoreRushTargets = [1000, 3000, 6000, 10000]
        let streakTargets = [7, 14, 30, 100]
        let dailyTotalTargets = [10, 30, 100]

        var list: [AchievementDefinition] = []

        for target in chainTargets {
            list.append(AchievementDefinition(
                id: "chain\(target)", category: .chain, target: target,
                titleKey: "achievement.chain.title", descKey: "achievement.chain.desc",
                coinReward: 10 + target, xpReward: target,
                currentValue: { PlayerStatsManager.shared.bestChainEver }))
        }
        for target in perfectTargets {
            list.append(AchievementDefinition(
                id: "perfect\(target)", category: .perfect, target: target,
                titleKey: "achievement.perfect.title", descKey: "achievement.perfect.desc",
                coinReward: 20 + target, xpReward: target * 2,
                currentValue: { PlayerStatsManager.shared.perfectBoardsTotal }))
        }
        for target in scoreZenTargets {
            list.append(AchievementDefinition(
                id: "scoreZen\(target)", category: .score, target: target,
                titleKey: "achievement.score_zen.title", descKey: "achievement.score_zen.desc",
                coinReward: 30, xpReward: 25,
                currentValue: { GameState.highScores().first ?? 0 }))
        }
        for target in scoreRushTargets {
            list.append(AchievementDefinition(
                id: "scoreRush\(target)", category: .score, target: target,
                titleKey: "achievement.score_rush.title", descKey: "achievement.score_rush.desc",
                coinReward: 30, xpReward: 25,
                currentValue: { GameState.rushBest() }))
        }
        for target in streakTargets {
            list.append(AchievementDefinition(
                id: "streak\(target)", category: .daily, target: target,
                titleKey: "achievement.streak.title", descKey: "achievement.streak.desc",
                coinReward: 20 + target, xpReward: target,
                currentValue: { StreakManager.shared.best }))
        }
        for target in dailyTotalTargets {
            list.append(AchievementDefinition(
                id: "dailyTotal\(target)", category: .daily, target: target,
                titleKey: "achievement.daily_total.title", descKey: "achievement.daily_total.desc",
                coinReward: 20 + target, xpReward: target,
                currentValue: { PlayerStatsManager.shared.totalDailyCompletions }))
        }
        return list
    }()

    struct DisplayAchievement {
        let definition: AchievementDefinition
        let currentValue: Int
        var isUnlocked: Bool { currentValue >= definition.target }
    }

    static func achievements(category: AchievementCategory? = nil) -> [DisplayAchievement] {
        catalog
            .filter { category == nil || $0.category == category }
            .map { DisplayAchievement(definition: $0, currentValue: $0.currentValue()) }
    }

    static var unlockedCount: Int { achievements().filter(\.isUnlocked).count }
    static var totalCount: Int { catalog.count }

    static func title(for def: AchievementDefinition) -> String {
        String(format: String(localized: String.LocalizationValue(def.titleKey)), def.target)
    }

    static func description(for def: AchievementDefinition) -> String {
        String(format: String(localized: String.LocalizationValue(def.descKey)), def.target)
    }

    // MARK: - Déblocage

    private static var unlockedIDs: Set<String> {
        get { Set(UserDefaults.standard.stringArray(forKey: AppConfig.UserDefaultsKey.achievementsUnlocked) ?? []) }
        set { UserDefaults.standard.set(Array(newValue), forKey: AppConfig.UserDefaultsKey.achievementsUnlocked) }
    }

    /// À appeler après toute mise à jour de statistiques (chaîne, fin de
    /// partie…). Crédite pièces + XP des succès nouvellement atteints et les
    /// retourne, pour affichage d'un toast — idempotent (chaque succès n'est
    /// crédité qu'une fois dans la vie du joueur).
    @discardableResult
    static func checkForNewUnlocks() -> [AchievementDefinition] {
        var unlocked = unlockedIDs
        var newly: [AchievementDefinition] = []
        for def in catalog where !unlocked.contains(def.id) {
            guard def.currentValue() >= def.target else { continue }
            unlocked.insert(def.id)
            newly.append(def)
            if def.coinReward > 0 { CoinManager.shared.add(def.coinReward) }
            if def.xpReward > 0 { LevelManager.shared.awardForAchievement(xp: def.xpReward) }
        }
        if !newly.isEmpty { unlockedIDs = unlocked }
        return newly
    }
}
