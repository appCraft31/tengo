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
    static let supportEmail = "support@tengo-app.com"

    /// Texte partagé lors d'un share
    static let shareMessage = """
    Découvre tenGO — un puzzle zen pour respirer.
    \(appStoreURL)
    """

    /// Version affichée dans le menu paramètres
    static var appVersion: String {
        let v = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        return "Version \(v)"
    }

    // MARK: - UserDefaults keys

    enum UserDefaultsKey {
        static let hasSeenTutorial = "hasSeenTutorial"
        static let soundMuted = "tenGO_soundMuted"
        static let hapticsEnabled = "tenGO_hapticsEnabled"
    }
}
