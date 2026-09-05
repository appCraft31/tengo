//
//  LevelManager.swift
//  tenGO
//
//  Progression du joueur : XP cumulée et niveau (1 à 100). Socle de données
//  pour les Missions, le Profil et les Achievements (GDD Ten Go 3.0).
//  Persistance locale (UserDefaults), aucun backend — même pattern que
//  CoinManager et StreakManager.
//

import Foundation

final class LevelManager {

    static let shared = LevelManager()
    private init() {}

    private let defaults = UserDefaults.standard

    /// Niveau maximum atteignable. Au-delà, l'XP continue de s'accumuler
    /// dans totalXP mais n'affecte plus le niveau affiché.
    static let maxLevel = 100

    // MARK: - Barème de gain d'XP

    static let xpGameCompleted = 10
    static let xpChainTier5 = 5
    static let xpChainTier10 = 15
    /// XP par chaîne validée. Le GDD parle de « +10 par combo » (§22) en
    /// pensant à un événement rare ; dans tenGO une « chaîne validée » est
    /// simplement un coup, et il y en a une vingtaine par partie. À 10 XP
    /// pièce, la première partie faisait gagner ~3 niveaux d'un coup et vidait
    /// la progression de son sens. 1 XP par coup, plafonné, garde le geste
    /// récompensé sans écraser la courbe.
    static let xpPerChain = 1
    static let xpChainsCap = 25
    static let xpPerfectBoard = 50
    static let xpDailyChallenge = 30
    static let xpDailyChallengePerfect = 75
    static let xpMission = 15
    static let xpSuperMission = 40
    static let xpRush = 20
    static let xpPuzzleSolved = 40

    /// Titres de niveau (palier → clé de localisation), du plus bas au plus
    /// haut. `currentTitle` retient le plus haut palier atteint.
    private static let titleThresholds: [(level: Int, key: String)] = [
        (1, "level.title.beginner"),
        (5, "level.title.learner"),
        (10, "level.title.thinker"),
        (20, "level.title.strategist"),
        (30, "level.title.expert"),
        (50, "level.title.master"),
        (75, "level.title.zen_master"),
        (100, "level.title.ten_master")
    ]

    // MARK: - État

    /// XP total cumulé depuis le début (ne diminue jamais).
    var totalXP: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.totalXP)
    }

    /// Niveau courant (1...maxLevel), dérivé de totalXP via une courbe
    /// croissante (chaque niveau coûte un peu plus d'XP que le précédent).
    var level: Int {
        levelBreakdown(for: totalXP).level
    }

    /// XP déjà consommée dans le niveau courant (utile pour une barre de
    /// progression). 0 si le niveau max est atteint.
    var xpIntoCurrentLevel: Int {
        levelBreakdown(for: totalXP).xpIntoLevel
    }

    /// XP nécessaire pour passer au niveau suivant. 0 si le niveau max est
    /// atteint (il n'y a pas de "niveau 101").
    var xpForNextLevel: Int {
        let breakdown = levelBreakdown(for: totalXP)
        return breakdown.level >= LevelManager.maxLevel ? 0 : LevelManager.xpRequired(forLevel: breakdown.level)
    }

    /// Titre localisé du niveau courant (Débutant, Penseur, Maître Zen…).
    var currentTitle: String {
        LevelManager.title(forLevel: level)
    }

    // MARK: - Résultat d'un gain d'XP

    struct GainResult {
        let xpGained: Int
        let previousLevel: Int
        let newLevel: Int
        var leveledUp: Bool { newLevel > previousLevel }
    }

    // MARK: - Gains

    /// Fin d'une partie normale. Réutilise les données déjà trackées par
    /// GameScene (plus longue chaîne, nombre de coups, grille vidée ou non).
    @discardableResult
    func awardForGame(score: Int, longestChain: Int, chainsCommitted: Int, isPerfect: Bool) -> GainResult {
        var xp = LevelManager.xpGameCompleted
        if longestChain >= 10 {
            xp += LevelManager.xpChainTier10
        } else if longestChain >= 5 {
            xp += LevelManager.xpChainTier5
        }
        xp += min(LevelManager.xpChainsCap, max(0, chainsCommitted) * LevelManager.xpPerChain)
        if isPerfect {
            xp += LevelManager.xpPerfectBoard
        }
        return add(xp)
    }

    /// Fin du Défi du jour. À n'appeler qu'une seule fois par jour (même
    /// garde anti-double-crédit que CoinManager.awardDailyChallenge).
    @discardableResult
    func awardForDailyChallenge(isPerfect: Bool) -> GainResult {
        let xp = isPerfect ? LevelManager.xpDailyChallengePerfect : LevelManager.xpDailyChallenge
        return add(xp)
    }

    /// Réclamation d'une mission quotidienne (cf. MissionManager.claim).
    @discardableResult
    func awardForMission(isSuper: Bool) -> GainResult {
        add(isSuper ? LevelManager.xpSuperMission : LevelManager.xpMission)
    }

    /// Fin d'une partie Rush (forfait fixe, indépendant du score — cf. GDD §22).
    @discardableResult
    func awardForRush() -> GainResult {
        add(LevelManager.xpRush)
    }

    /// Fin d'un niveau de puzzle : plein tarif s'il est résolu, consolation
    /// s'il est abandonné en cours (le joueur a quand même joué).
    @discardableResult
    func awardForPuzzle(completed: Bool) -> GainResult {
        add(completed ? LevelManager.xpPuzzleSolved : LevelManager.xpGameCompleted)
    }

    /// Déblocage d'un succès (cf. AchievementManager.checkForNewUnlocks).
    @discardableResult
    func awardForAchievement(xp: Int) -> GainResult {
        add(xp)
    }

    // MARK: - Private

    @discardableResult
    private func add(_ amount: Int) -> GainResult {
        let before = level
        guard amount > 0 else { return GainResult(xpGained: 0, previousLevel: before, newLevel: before) }
        defaults.set(totalXP + amount, forKey: AppConfig.UserDefaultsKey.totalXP)
        return GainResult(xpGained: amount, previousLevel: before, newLevel: level)
    }

    /// XP requise pour passer du niveau `level` au niveau `level + 1`.
    /// Croissance linéaire simple : pas de mur de progression.
    private static func xpRequired(forLevel level: Int) -> Int {
        60 + (level - 1) * 12
    }

    /// Consomme `xp` à travers les paliers cumulés pour en déduire le
    /// niveau courant et la progression dans ce niveau.
    private func levelBreakdown(for xp: Int) -> (level: Int, xpIntoLevel: Int) {
        var remaining = max(0, xp)
        var currentLevel = 1
        while currentLevel < LevelManager.maxLevel {
            let required = LevelManager.xpRequired(forLevel: currentLevel)
            guard remaining >= required else { break }
            remaining -= required
            currentLevel += 1
        }
        return (currentLevel, remaining)
    }

    /// Titre localisé correspondant au plus haut palier atteint par `level`.
    private static func title(forLevel level: Int) -> String {
        let key = titleThresholds.last { $0.level <= level }?.key ?? titleThresholds[0].key
        return String(localized: String.LocalizationValue(key))
    }
}
