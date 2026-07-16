//
//  CoinManager.swift
//  tenGO
//
//  Monnaie du jeu (« pièces ») dépensable dans la boutique de thèmes.
//  Gains : bonus à la complétion du Défi du jour + paliers de série atteints.
//  Persistance locale (UserDefaults), aucun backend.
//

import Foundation

final class CoinManager {

    static let shared = CoinManager()
    private init() {}

    private let defaults = UserDefaults.standard

    /// Pièces offertes pour avoir terminé le Défi du jour.
    static let dailyChallengeReward = 30
    /// Paliers de série → pièces (récompensés une seule fois chacun, anti-farm).
    static let streakMilestones: [(day: Int, coins: Int)] = [
        (3, 20), (7, 50), (14, 100), (30, 200)
    ]

    // MARK: - Solde

    var balance: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.coinsBalance)
    }

    func add(_ amount: Int) {
        guard amount > 0 else { return }
        defaults.set(balance + amount, forKey: AppConfig.UserDefaultsKey.coinsBalance)
    }

    /// Débite `amount` pièces si le solde le permet. Retourne false sinon.
    @discardableResult
    func spend(_ amount: Int) -> Bool {
        guard amount > 0, balance >= amount else { return false }
        defaults.set(balance - amount, forKey: AppConfig.UserDefaultsKey.coinsBalance)
        return true
    }

    // MARK: - Gains

    /// Récompense de fin de partie normale : forfait de base + bonus au score,
    /// plafonné. Garantit un gain motivant même sur une petite partie.
    /// pièces = min(scoreCoinMax, scoreCoinBase + score / scoreCoinDivisor)
    static let scoreCoinBase = 5
    static let scoreCoinDivisor = 120
    static let scoreCoinMax = 10

    /// Récompense la fin d'une partie normale (base + prorata du score, plafonné).
    /// Retourne le nombre de pièces créditées.
    @discardableResult
    func awardForScore(_ score: Int) -> Int {
        let raw = CoinManager.scoreCoinBase + max(0, score) / CoinManager.scoreCoinDivisor
        let gained = min(CoinManager.scoreCoinMax, raw)
        if gained > 0 { add(gained) }
        return gained
    }

    /// Récompense de complétion du Défi du jour (à n'appeler qu'une fois par défi).
    func awardDailyChallenge() {
        add(CoinManager.dailyChallengeReward)
    }

    /// Récompense les paliers de série nouvellement franchis (idempotent : chaque
    /// palier n'est payé qu'une fois dans la vie du joueur). Retourne les pièces gagnées.
    @discardableResult
    func awardStreakMilestones(currentStreak: Int) -> Int {
        let alreadyRewarded = defaults.integer(forKey: AppConfig.UserDefaultsKey.streakRewardedMilestone)
        var gained = 0
        var highest = alreadyRewarded
        for milestone in CoinManager.streakMilestones where milestone.day > alreadyRewarded && currentStreak >= milestone.day {
            gained += milestone.coins
            highest = max(highest, milestone.day)
        }
        if gained > 0 {
            add(gained)
            defaults.set(highest, forKey: AppConfig.UserDefaultsKey.streakRewardedMilestone)
        }
        return gained
    }
}
