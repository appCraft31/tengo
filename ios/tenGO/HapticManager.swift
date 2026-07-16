//
//  HapticManager.swift
//  tenGO
//
//  Wrapper sur UIImpactFeedbackGenerator avec toggle persisté.
//

import UIKit

enum HapticManager {

    static var isEnabled: Bool {
        get { UserDefaults.standard.object(forKey: "tenGO_hapticsEnabled") as? Bool ?? true }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_hapticsEnabled") }
    }

    /// Impact léger — sélection/connexion
    static let lightGenerator = UIImpactFeedbackGenerator(style: .light)

    /// Impact medium — validation combo
    static let mediumGenerator = UIImpactFeedbackGenerator(style: .medium)

    static func prepare() {
        lightGenerator.prepare()
        mediumGenerator.prepare()
    }

    static func light() {
        guard isEnabled else { return }
        lightGenerator.impactOccurred()
    }

    static func medium() {
        guard isEnabled else { return }
        mediumGenerator.impactOccurred()
    }
}
