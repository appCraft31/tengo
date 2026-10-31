//
//  FirstRun.swift
//  tenGO
//
//  Parcours du tout premier lancement (fiche TEN-4).
//
//  Constat : 9,3 % des nouveaux joueurs revenaient à J7. Le premier lancement
//  enchaînait demande ATT + feuille Game Center sur le menu, un menu à quatre
//  onglets, puis un tutoriel en trois pages avant la première bulle touchée.
//  Désormais, tant qu'aucune partie n'est terminée :
//    - l'app s'ouvre sur un tutoriel court (2 pages) qui mène droit en partie ;
//    - ATT, Game Center et pubs attendent la fin de la première partie ;
//    - pas de demande d'avis sur la première partie, interstitielles plus
//      tardives pendant la première session.
//
//  Mesure : `first_game_start` puis `first_game_complete`, chacun une seule
//  fois par installation, avec le temps écoulé depuis la première ouverture.
//  Noms et paramètres à reprendre à l'identique sur Android.
//

import Foundation
import FirebaseAnalytics

extension Notification.Name {
    /// Posté (main thread) quand la toute première partie se termine.
    static let tenGOFirstGameCompleted = Notification.Name("tenGOFirstGameCompleted")
}

enum FirstRun {

    private enum Key {
        /// « pending » tant que la première partie n'est pas terminée, « done » ensuite.
        static let state = "tenGO_firstRunState"
        /// Date de la première ouverture (base des durées mesurées).
        static let firstOpenAt = "tenGO_firstOpenAt"
        static let startLogged = "tenGO_firstGameStartLogged"
    }

    private static var defaults: UserDefaults { .standard }

    /// L'état valait « pending » au lancement de l'app : c'est la première
    /// session de jeu. Reste vrai jusqu'au prochain lancement à froid.
    private(set) static var isFirstSession = false

    /// À appeler au lancement, avant toute scène.
    ///
    /// Un joueur déjà installé avant cette version n'a pas de clé d'état : on
    /// le reconnaît à ses traces (tutoriel vu, parties comptées, scores) et on
    /// le classe « done », sans rien mesurer, pour ne pas fausser l'événement.
    static func bootstrap() {
        if defaults.string(forKey: Key.state) == nil {
            let hasHistory = defaults.bool(forKey: AppConfig.UserDefaultsKey.hasSeenTutorial)
                || defaults.integer(forKey: AppConfig.UserDefaultsKey.notifGamesPlayed) > 0
                || !(defaults.array(forKey: "tengo_high_scores") ?? []).isEmpty
            defaults.set(hasHistory ? "done" : "pending", forKey: Key.state)
            if !hasHistory { defaults.set(Date(), forKey: Key.firstOpenAt) }
        }
        isFirstSession = isPending
    }

    /// Aucune partie terminée depuis l'installation.
    static var isPending: Bool {
        defaults.string(forKey: Key.state) == "pending"
    }

    private static var secondsSinceFirstOpen: Int {
        guard let first = defaults.object(forKey: Key.firstOpenAt) as? Date else { return -1 }
        return max(0, Int(Date().timeIntervalSince(first)))
    }

    /// Début d'une partie. Ne journalise que la première de l'installation.
    static func gameStarted(mode: String) {
        guard isPending, !defaults.bool(forKey: Key.startLogged) else { return }
        defaults.set(true, forKey: Key.startLogged)
        log("first_game_start", ["mode": mode, "seconds_since_first_open": secondsSinceFirstOpen])
    }

    /// Fin d'une partie. La première de l'installation journalise
    /// `first_game_complete`, passe l'état à « done » et prévient le
    /// contrôleur (consentement, Game Center, pubs).
    /// - Returns: `true` si c'était la toute première partie terminée.
    @discardableResult
    static func gameCompleted(mode: String, score: Int, won: Bool) -> Bool {
        guard isPending else { return false }
        defaults.set("done", forKey: Key.state)
        log("first_game_complete", [
            "mode": mode,
            AnalyticsParameterScore: score,
            AnalyticsParameterSuccess: won ? 1 : 0,
            "seconds_since_first_open": secondsSinceFirstOpen,
        ])
        // Propriété utilisateur : segmente la rétention J7 dans Firebase entre
        // joueurs ayant fini une partie ou non, cohorte par cohorte.
        Analytics.setUserProperty("yes", forName: "first_game_done")
        DispatchQueue.main.async {
            NotificationCenter.default.post(name: .tenGOFirstGameCompleted, object: nil)
        }
        return true
    }

    private static func log(_ name: String, _ parameters: [String: Any]) {
        #if DEBUG
        print("[Analytics] \(name) \(parameters)")
        #endif
        Analytics.logEvent(name, parameters: parameters)
    }
}
