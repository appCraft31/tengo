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

    /// Pièces gagnées par tranche de score en partie normale (1 pièce / 200 pts).
    static let scoreCoinDivisor = 200

    /// Récompense la fin d'une partie normale au prorata du score réalisé.
    /// Retourne le nombre de pièces créditées (0 si score trop faible).
    @discardableResult
    func awardForScore(_ score: Int) -> Int {
        let gained = max(0, score / CoinManager.scoreCoinDivisor)
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
