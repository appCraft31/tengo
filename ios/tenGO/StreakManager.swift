//
//  StreakManager.swift
//  tenGO
//
//  Série de jours consécutifs de jeu — rétention « zen » : jouer un jour
//  prolonge la série, un jour manqué la remet simplement à 1, sans pénalité
//  ni message culpabilisant... sauf si un Streak Shield est en poche : il
//  absorbe alors la rupture automatiquement. Persistance locale
//  (UserDefaults), aucun backend.
//

import Foundation

final class StreakManager {

    static let shared = StreakManager()
    private init() {}

    private let defaults = UserDefaults.standard

    /// Paliers de récompense (jour → pièces + booster offert éventuel).
    /// Le GDD prévoit aussi thème exclusif / skin / effet légendaire / badges
    /// à 14/30/50/100/365 jours — différés à la Collection (#27) et aux
    /// Achievements (#11/#12), pas encore construits : les paliers ci-dessous
    /// substituent pièces + boosters en attendant, en conservant la même
    /// progression d'ampleur.
    /// Barème calibré sur le catalogue de la boutique (tous les thèmes réunis
    /// = 1 400 pièces, premier pack acheté = 500) : une première version
    /// distribuait 5 000 pièces sur l'année, ce qui rendait la boutique
    /// gratuite en une semaine et l'achat de pièces sans objet.
    static let milestones: [(day: Int, coins: Int, booster: Booster?, boosterQty: Int)] = [
        (3, 25, nil, 0),
        (7, 0, .hint, 1),
        (14, 30, .shuffle, 1),
        (21, 75, nil, 0),
        (30, 60, .hammer, 1),
        (50, 150, nil, 0),
        (100, 300, nil, 0),
        (365, 1000, nil, 0),
    ]

    /// Nombre maximum de boucliers détenus simultanément — le Shield doit
    /// rester rare, pas une réserve illimitée.
    static let maxShields = 2

    // MARK: - Série

    /// Série en cours (jours consécutifs).
    var current: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.streakCurrent)
    }

    /// Meilleure série atteinte.
    var best: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.streakBest)
    }

    /// Prochain palier de récompense à venir (nil si le dernier est dépassé).
    var nextMilestone: Int? {
        StreakManager.milestones.map(\.day).first { $0 > current }
    }

    /// Le joueur a-t-il déjà joué aujourd'hui (calendrier local) ? Utile pour
    /// savoir si la série est encore « à risque » aujourd'hui — cf. la
    /// notification de rappel de série (NotificationManager).
    func hasPlayedToday(_ date: Date = Date()) -> Bool {
        guard let lastInterval = lastPlayedInterval() else { return false }
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: date)
        let lastDay = calendar.startOfDay(for: Date(timeIntervalSinceReferenceDate: lastInterval))
        // <= 0 et non == 0 : une dernière partie « dans le futur » (horloge
        // reculée) doit compter comme jouée, pas déclencher un rappel.
        return (calendar.dateComponents([.day], from: lastDay, to: today).day ?? 0) <= 0
    }

    /// À appeler au lancement d'une partie. Met à jour la série selon le jour
    /// (calendrier local — c'est « aujourd'hui » du point de vue du joueur).
    /// Retourne `true` si un Streak Shield vient d'être consommé pour éviter
    /// la rupture de série (jour manqué mais bouclier en poche).
    @discardableResult
    func registerPlay(_ date: Date = Date()) -> Bool {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: date)

        guard let lastInterval = lastPlayedInterval() else {
            setCurrent(1, last: today)
            return false
        }
        let lastDay = calendar.startOfDay(for: Date(timeIntervalSinceReferenceDate: lastInterval))
        let gap = calendar.dateComponents([.day], from: lastDay, to: today).day ?? 0

        switch gap {
        case ..<0:
            // Horloge reculée ou changement de fuseau vers l'ouest : la
            // « dernière partie » est dans le futur. Ne rien casser et
            // surtout ne pas brûler un bouclier pour un artefact d'horloge.
            return false
        case 0:
            return false                  // déjà compté aujourd'hui
        case 1:
            setCurrent(current + 1, last: today)
            return false
        default:
            if consumeShield() {
                // Le jour manqué est absorbé : la série continue comme si
                // gap == 1, sans que le joueur n'ait rien eu à faire.
                setCurrent(current + 1, last: today)
                return true
            }
            setCurrent(1, last: today)    // série rompue → on repart à 1, sans drame
            return false
        }
    }

    /// Crédite les paliers nouvellement franchis (idempotent : chaque palier
    /// n'est payé qu'une fois dans la vie du joueur). Retourne les pièces gagnées.
    @discardableResult
    func awardMilestones(currentStreak: Int) -> Int {
        migrateRewardedMilestoneIfNeeded()
        let alreadyRewarded = defaults.integer(forKey: AppConfig.UserDefaultsKey.streakRewardedMilestone)
        var gainedCoins = 0
        var highest = alreadyRewarded
        for milestone in StreakManager.milestones where milestone.day > alreadyRewarded && currentStreak >= milestone.day {
            gainedCoins += milestone.coins
            if let booster = milestone.booster {
                BoosterManager.shared.grant(booster, quantity: milestone.boosterQty)
            }
            highest = max(highest, milestone.day)
        }
        if gainedCoins > 0 {
            CoinManager.shared.add(gainedCoins)
        }
        if highest != alreadyRewarded {
            defaults.set(highest, forKey: AppConfig.UserDefaultsKey.streakRewardedMilestone)
        }
        return gainedCoins
    }

    /// L'ancien barème (2.3.0) s'arrêtait à 30 jours et stockait le jour du
    /// dernier palier payé dans la même clé. Un joueur arrivé à 30 jours avant
    /// la mise à jour verrait donc le palier 21, qui n'existait pas chez lui,
    /// filtré à jamais par `milestone.day > alreadyRewarded`. On le ramène à 20
    /// pour qu'il touche ce palier comme tout le monde — sans rouvrir les
    /// paliers 3/7/14 qu'il a bien perçus.
    private func migrateRewardedMilestoneIfNeeded() {
        let key = AppConfig.UserDefaultsKey.streakRewardedMilestone
        guard defaults.integer(forKey: key) == 30,
              !defaults.bool(forKey: AppConfig.UserDefaultsKey.streakMilestoneMigrated) else { return }
        defaults.set(20, forKey: key)
        defaults.set(true, forKey: AppConfig.UserDefaultsKey.streakMilestoneMigrated)
    }

    // MARK: - Streak Shield

    /// Boucliers actuellement en poche.
    var shieldCount: Int {
        defaults.integer(forKey: AppConfig.UserDefaultsKey.streakShieldCount)
    }

    /// Ajoute des boucliers, plafonné à `maxShields` (rareté voulue).
    func addShield(_ count: Int = 1) {
        guard count > 0 else { return }
        let capped = min(StreakManager.maxShields, shieldCount + count)
        defaults.set(capped, forKey: AppConfig.UserDefaultsKey.streakShieldCount)
    }

    /// Consomme un bouclier s'il y en a un disponible. Retourne le succès.
    @discardableResult
    private func consumeShield() -> Bool {
        guard shieldCount > 0 else { return false }
        defaults.set(shieldCount - 1, forKey: AppConfig.UserDefaultsKey.streakShieldCount)
        return true
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
