//
//  AppConfig.swift
//  tenGO
//
//  Constantes globales — URL, email, clés UserDefaults.
//  Centralisé ici pour faciliter les mises à jour post-launch.
//

import Foundation

enum AppConfig {

    /// URL App Store de tenGO — à renseigner après publication
    /// Format attendu : https://apps.apple.com/app/idXXXXXXXXXX
    static let appStoreURL = "https://apps.apple.com/app/id0000000000"

    /// Deep link utilisé pour ouvrir la page review directement
    /// Format : https://apps.apple.com/app/idXXXXXXXXXX?action=write-review
    static let appStoreReviewURL = "\(appStoreURL)?action=write-review"

    /// Email de contact du support
    static let supportEmail = "appcraft31@gmail.com"

    /// Texte partagé lors d'un share (localisé)
    static var shareMessage: String {
        String(format: String(localized: "app.share_message"), appStoreURL)
    }

    /// Version affichée dans le menu paramètres (localisée)
    static var appVersion: String {
        let v = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        return String(format: String(localized: "app.version_label"), v)
    }

    // MARK: - Game Center

    /// Identifiant du classement Game Center — à créer dans App Store Connect
    /// (Fonctionnalités → Game Center → Classements)
    static let gameCenterLeaderboardID = "com.tengo.leaderboard.global"

    /// Classement du Défi du jour — à créer dans App Store Connect.
    /// Idéalement configuré en réinitialisation quotidienne.
    static let gameCenterDailyLeaderboardID = "com.tengo.leaderboard.daily"

    /// Classement du mode Rush — à créer dans App Store Connect.
    static let gameCenterRushLeaderboardID = "com.tengo.leaderboard.rush"

    // MARK: - UserDefaults keys

    enum UserDefaultsKey {
        static let hasSeenTutorial = "hasSeenTutorial"
        /// Le guide d'achat en boutique a été mené à son terme (one-shot).
        static let hasSeenShopPurchaseGuide = "hasSeenShopPurchaseGuide"
        /// Nombre de fois où le joueur a repoussé le guide d'achat. Au-delà de
        /// `shopGuideMaxSnooze`, on cesse définitivement d'insister.
        static let shopGuideSnooze = "tenGO_shopGuideSnooze"
        /// Préfixe des coach-marks d'usage des boosters (+ rawValue du booster).
        /// Posé à la FERMETURE du coach-mark, jamais à son affichage.
        static let boosterCoachSeenPrefix = "tenGO_boosterCoachSeen_"
        static let soundMuted = "tenGO_soundMuted"
        static let hapticsEnabled = "tenGO_hapticsEnabled"
        /// Rendu calme : coupe secousses, particules lourdes et pulsations.
        /// N'affecte que le ressenti — aucune règle ni timing de jeu ne change.
        static let reducedEffects = "tenGO_reducedEffects"
        /// Dernier jour (clé AAAAMMJJ, UTC) où le Défi du jour a été complété.
        static let dailyLastCompletedDayKey = "tenGO_dailyLastCompleted"
        /// Série de jours consécutifs de jeu (rétention douce).
        static let streakCurrent = "tenGO_streakCurrent"
        static let streakBest = "tenGO_streakBest"
        static let streakLastPlayed = "tenGO_streakLastPlayed"
        /// Nombre de parties jouées (pour différer la demande de notifications).
        static let notifGamesPlayed = "tenGO_notifGamesPlayed"
        /// L'autorisation de notifications a déjà été demandée.
        static let notifRequested = "tenGO_notifRequested"
        /// Solde de pièces (monnaie de la boutique).
        static let coinsBalance = "tenGO_coins"
        /// Plus haut palier de série déjà récompensé (anti-farm), cf. StreakManager.
        static let streakRewardedMilestone = "tenGO_streakRewardedMilestone"
        /// Boucliers de série en poche (protègent un jour manqué), cf. StreakManager.
        static let streakShieldCount = "tenGO_streakShieldCount"
        /// Thèmes possédés (liste d'identifiants) ; le thème par défaut est toujours possédé.
        static let ownedThemes = "tenGO_ownedThemes"
        /// Identifiant du thème actif.
        static let activeTheme = "tenGO_activeTheme"
        /// Matières de bulles possédées / actives (cosmétique).
        static let ownedBubbleStyles = "tenGO_ownedBubbleStyles"
        static let activeBubbleStyle = "tenGO_activeBubbleStyle"
        /// Styles de tracé possédés / actifs (cosmétique).
        static let ownedTrails = "tenGO_ownedTrails"
        static let activeTrail = "tenGO_activeTrail"
        /// Inventaire des boosters : préfixe + identifiant du booster (ex. "tenGO_booster_hint").
        static let boosterInventoryPrefix = "tenGO_booster_"
        /// Plafond journalier des pubs récompensées : jour courant (AAAAMMJJ) + compteur.
        static let rewardedDayKey = "tenGO_rewardedDay"
        static let rewardedDayCount = "tenGO_rewardedDayCount"
        /// Cache local du mod « sans pub » (non-consommable). Sert à couper les
        /// pubs dès le lancement, avant la réponse de StoreKit ; la source de
        /// vérité reste Transaction.currentEntitlements (voir AdFreeManager).
        static let noAdsPurchased = "tenGO_noAdsPurchased"
        /// XP total cumulé (système de niveaux, cf. LevelManager).
        static let totalXP = "tenGO_totalXP"
        /// Jour (clé AAAAMMJJ) du jeu de missions quotidiennes en cours.
        static let missionsDayKey = "tenGO_missionsDayKey"
        /// État des missions du jour (JSON encodé), cf. MissionManager.
        static let missionsProgress = "tenGO_missionsProgress"
    }
}
