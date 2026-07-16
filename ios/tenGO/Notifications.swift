//
//  Notifications.swift
//  tenGO
//

import Foundation

extension Notification.Name {
    static let tenGOSceneChanged = Notification.Name("tenGOSceneChanged")
    static let tenGOShowGameCenter = Notification.Name("tenGOShowGameCenter")
    static let tenGOOpenDaily = Notification.Name("tenGOOpenDaily")
    /// Le mod « sans pub » vient d'être activé (achat ou restauration).
    static let tenGOAdFreeChanged = Notification.Name("tenGOAdFreeChanged")
}
