//
//  DeepLink.swift
//  tenGO
//
//  Routage des liens entrants, schéma personnalisé (`tengo://daily`) comme
//  universal links (`https://appcraft31.app/tengo/daily`). Utilisé par les
//  événements intégrés App Store et tout lien externe.
//
//  Le fichier d'association du domaine ne couvre que `/tengo/*` : c'est le seul
//  préfixe qu'iOS acceptera de remettre à l'app. Le chemin est donc normalisé
//  ici, pour que les deux formes de lien empruntent exactement les mêmes
//  branches.
//

import Foundation

enum DeepLink {

    /// Un lien « daily » est arrivé mais n'a pas encore été traité (ex. lancement à froid).
    private(set) static var pendingDaily = false
    /// Code de duel reçu par lien, consommé à l'ouverture de l'écran Duel.
    private(set) static var pendingDuelCode: String?
    /// Lien de l'événement intégré (nouveautés de la version) en attente.
    private(set) static var pendingProgress = false

    /// Analyse une URL entrante. Retourne true si elle a été reconnue.
    @discardableResult
    static func handle(_ url: URL) -> Bool {
        let host = (url.host ?? "").lowercased()
        var path = url.path.lowercased()

        // `https://appcraft31.app/tengo/daily` et `tengo://daily` désignent la
        // même chose : on retire le préfixe du site avant de router.
        if path == "/tengo" {
            path = "/"
        } else if path.hasPrefix("/tengo/") {
            path.removeFirst("/tengo".count)
        }

        // tengo://duel/ABC123  ou  https://…/duel/ABC123
        if host == "duel" || path == "/duel" || path.hasPrefix("/duel/") {
            // Le code se lit dans le chemin normalisé, jamais dans le host : en
            // universal link celui-ci vaut « appcraft31.app », qui serait pris
            // pour un code de duel.
            let segments = path.split(separator: "/").map(String.init)
                .filter { $0 != "duel" }
            guard let code = segments.first, !code.isEmpty else { return false }
            pendingDuelCode = code.uppercased()
            NotificationCenter.default.post(name: .tenGOOpenDuel, object: nil)
            return true
        }

        // tengo://event  ou  https://appcraft31.app/tengo/event — lien profond de
        // l'événement intégré App Store, qui ouvre la Progression (XP, missions,
        // succès, série), la vitrine des nouveautés de la 3.0.
        if host == "event" || path == "/event" || host == "progress" || path == "/progress" {
            pendingProgress = true
            NotificationCenter.default.post(name: .tenGOOpenProgress, object: nil)
            return true
        }

        guard host == "daily" || path == "/daily" || path.hasPrefix("/daily/") else { return false }
        pendingDaily = true
        NotificationCenter.default.post(name: .tenGOOpenDaily, object: nil)
        return true
    }

    /// Consomme le lien en attente (remet à zéro).
    @discardableResult
    static func consumePendingDuelCode() -> String? {
        defer { pendingDuelCode = nil }
        return pendingDuelCode
    }

    @discardableResult
    static func consumePendingProgress() -> Bool {
        defer { pendingProgress = false }
        return pendingProgress
    }

    @discardableResult
    static func consumePendingDaily() -> Bool {
        defer { pendingDaily = false }
        return pendingDaily
    }
}
