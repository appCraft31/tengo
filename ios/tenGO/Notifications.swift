//
//  Notifications.swift
//  tenGO
//

import Foundation

extension Notification.Name {
    static let tenGOSceneChanged = Notification.Name("tenGOSceneChanged")
    static let tenGOShowGameCenter = Notification.Name("tenGOShowGameCenter")
    static let tenGOOpenDaily = Notification.Name("tenGOOpenDaily")
    /// Un lien tengo://duel/CODE vient d'être ouvert.
    static let tenGOOpenDuel = Notification.Name("tenGOOpenDuel")
    /// Le mod « sans pub » vient d'être activé (achat ou restauration).
    static let tenGOAdFreeChanged = Notification.Name("tenGOAdFreeChanged")
}
