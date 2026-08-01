//
//  JuiceSettings.swift
//  tenGO
//
//  Niveau d'effets visuels (« juice »), croisé avec l'accessibilité système.
//  Point de lecture UNIQUE du réglage : personne d'autre ne lit la clé
//  UserDefaults, pour que le gating Reduce Motion ne puisse pas être contourné.
//

import UIKit

enum JuiceLevel {
    /// Rendu complet : escalade, secousses, particules, pulsations.
    case full
    /// Rendu calme : le jeu est strictement identique, seul le ressenti s'apaise.
    case reduced
}

enum JuiceSettings {

    // MARK: - Réglage utilisateur

    static var reducedEffectsPreference: Bool {
        get { UserDefaults.standard.bool(forKey: AppConfig.UserDefaultsKey.reducedEffects) }
        set {
            UserDefaults.standard.set(newValue, forKey: AppConfig.UserDefaultsKey.reducedEffects)
            invalidateCache()
        }
    }

    /// Mode démo (captures marketing) : le juice reste complet même si l'appareil
    /// est configuré en Reduce Motion, sinon les vidéos de la fiche store
    /// sortiraient ternes selon la configuration de la machine de capture.
    static var forceFull = false {
        didSet { invalidateCache() }
    }

    // MARK: - Niveau effectif

    /// Mis en cache : `level` est lu à chaque effet, et interroger
    /// UIAccessibility à cette fréquence est inutilement coûteux.
    private static var cached: JuiceLevel?

    static var level: JuiceLevel {
        if let cached { return cached }
        let value: JuiceLevel
        if forceFull {
            value = .full
        } else if UIAccessibility.isReduceMotionEnabled || reducedEffectsPreference {
            value = .reduced
        } else {
            value = .full
        }
        cached = value
        return value
    }

    /// Le système impose le rendu calme : le toggle utilisateur ne peut pas
    /// le contourner (règle d'accessibilité), il est alors affiché grisé.
    static var isForcedBySystem: Bool {
        !forceFull && UIAccessibility.isReduceMotionEnabled
    }

    /// Mouvement : secousses, squash, respiration, comète, confettis.
    static var motionEnabled: Bool { level == .full }

    /// Densité de particules — le mode calme sert aussi de filet de sécurité perf.
    static var particleScale: CGFloat { level == .full ? 1.0 : 0.35 }

    // MARK: - Cycle de vie

    static func invalidateCache() { cached = nil }

    /// À appeler une fois au lancement : Reduce Motion peut être basculé
    /// depuis le Centre de contrôle pendant que l'app tourne.
    static func startObservingAccessibility() {
        NotificationCenter.default.addObserver(
            forName: UIAccessibility.reduceMotionStatusDidChangeNotification,
            object: nil,
            queue: .main
        ) { _ in invalidateCache() }
    }
}
