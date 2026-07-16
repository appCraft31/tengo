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

    /// Analyse une URL entrante. Retourne true si elle a été reconnue.
    @discardableResult
    static func handle(_ url: URL) -> Bool {
        let host = (url.host ?? "").lowercased()
        let path = url.path.lowercased()
        guard host == "daily" || path == "/daily" || path.hasPrefix("/daily/") else { return false }
        pendingDaily = true
        NotificationCenter.default.post(name: .tenGOOpenDaily, object: nil)
        return true
    }

    /// Consomme le lien en attente (remet à zéro).
    @discardableResult
    static func consumePendingDaily() -> Bool {
        defer { pendingDaily = false }
        return pendingDaily
    }
}
