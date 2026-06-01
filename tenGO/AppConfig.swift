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

    // MARK: - UserDefaults keys

    enum UserDefaultsKey {
        static let hasSeenTutorial = "hasSeenTutorial"
        static let soundMuted = "tenGO_soundMuted"
        static let hapticsEnabled = "tenGO_hapticsEnabled"
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
    }
}
