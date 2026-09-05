//
//  DeepLink.swift
//  tenGO
//
//  Routage des liens entrants (schéma personnalisé `tengo://daily` et universal
//  links `https://.../daily`) vers le Défi du jour. Utilisé par les événements
//  intégrés App Store et tout lien externe.
//

import Foundation

enum DeepLink {

    /// Un lien « daily » est arrivé mais n'a pas encore été traité (ex. lancement à froid).
    private(set) static var pendingDaily = false
    /// Code de duel reçu par lien, consommé à l'ouverture de l'écran Duel.
    private(set) static var pendingDuelCode: String?

    /// Analyse une URL entrante. Retourne true si elle a été reconnue.
    @discardableResult
    static func handle(_ url: URL) -> Bool {
        let host = (url.host ?? "").lowercased()
        let path = url.path.lowercased()

        // tengo://duel/ABC123  ou  https://…/duel/ABC123
        if host == "duel" || path == "/duel" || path.hasPrefix("/duel/") {
            let segments = ([host] + url.pathComponents).filter { $0 != "/" && $0.lowercased() != "duel" }
            guard let code = segments.first, !code.isEmpty else { return false }
            pendingDuelCode = code.uppercased()
            NotificationCenter.default.post(name: .tenGOOpenDuel, object: nil)
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
    static func consumePendingDaily() -> Bool {
        defer { pendingDaily = false }
        return pendingDaily
    }
}
