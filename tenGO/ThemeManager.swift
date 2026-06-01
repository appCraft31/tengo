//
//  ThemeManager.swift
//  tenGO
//
//  Thèmes visuels de l'app (palettes de couleurs) achetables dans la boutique
//  avec des pièces. Le thème « default » est gratuit et toujours possédé.
//  Palettes conçues par sous-agents, harmonisées et contrôlées en contraste.
//

import UIKit

private func rgb(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat) -> UIColor {
    UIColor(red: r, green: g, blue: b, alpha: 1)
}

struct Theme {
    let id: String
    let emoji: String
    let price: Int
    let background: UIColor
    let accent: UIColor       // bouton principal du menu
    let digit: UIColor        // chiffre sur les bulles
    let logo: UIColor         // titre / texte d'interface
    let bubbles: [UIColor]    // 9 couleurs, valeurs 1→9

    static let defaultID = "default"

    var nameKey: String { "theme.\(id)" }
    var isDefault: Bool { id == Theme.defaultID }

    func color(forValue value: Int) -> UIColor {
        guard value >= 1 && value <= bubbles.count else { return .gray }
        return bubbles[value - 1]
    }
}

final class ThemeManager {

    static let shared = ThemeManager()
    private init() {}

    private let defaults = UserDefaults.standard

    /// Catalogue. Le défaut en premier ; les autres dans l'ordre d'affichage boutique.
    let themes: [Theme] = [
        Theme(
            id: "default", emoji: "🎨", price: 0,
            background: rgb(0.97, 0.95, 0.92),
            accent: rgb(0.82, 0.95, 0.88),
            digit: rgb(0.25, 0.25, 0.25),
            logo: rgb(0.28, 0.28, 0.28),
            bubbles: [rgb(1.00, 0.62, 0.62), rgb(1.00, 0.78, 0.62), rgb(1.00, 0.96, 0.62), rgb(0.67, 0.95, 0.75), rgb(0.62, 0.86, 1.00), rgb(0.80, 0.72, 1.00), rgb(1.00, 0.72, 0.86), rgb(0.75, 0.91, 0.75), rgb(0.72, 0.76, 1.00)]
        ),
        Theme(
            id: "forest", emoji: "🌲", price: 150,
            background: rgb(0.93, 0.95, 0.88),
            accent: rgb(0.55, 0.68, 0.48),
            digit: rgb(0.20, 0.16, 0.12),
            logo: rgb(0.22, 0.32, 0.22),
            bubbles: [rgb(0.70, 0.80, 0.60), rgb(0.62, 0.74, 0.55), rgb(0.80, 0.82, 0.62), rgb(0.74, 0.68, 0.50), rgb(0.66, 0.72, 0.60), rgb(0.82, 0.72, 0.56), rgb(0.58, 0.70, 0.58), rgb(0.76, 0.78, 0.66), rgb(0.68, 0.66, 0.52)]
        ),
        Theme(
            id: "ocean", emoji: "🌊", price: 200,
            background: rgb(0.91, 0.96, 0.97),
            accent: rgb(0.36, 0.72, 0.78),
            digit: rgb(0.08, 0.22, 0.33),
            logo: rgb(0.10, 0.28, 0.40),
            bubbles: [rgb(0.62, 0.85, 0.86), rgb(0.46, 0.78, 0.80), rgb(0.40, 0.74, 0.72), rgb(0.52, 0.80, 0.66), rgb(0.66, 0.84, 0.72), rgb(0.45, 0.71, 0.83), rgb(0.38, 0.62, 0.80), rgb(0.50, 0.68, 0.86), rgb(0.63, 0.78, 0.88)]
        ),
        Theme(
            id: "desert", emoji: "🏜️", price: 200,
            background: rgb(0.96, 0.91, 0.83),
            accent: rgb(0.85, 0.55, 0.40),
            digit: rgb(0.30, 0.18, 0.12),
            logo: rgb(0.42, 0.26, 0.18),
            bubbles: [rgb(0.94, 0.84, 0.68), rgb(0.93, 0.78, 0.62), rgb(0.91, 0.72, 0.55), rgb(0.90, 0.68, 0.58), rgb(0.89, 0.62, 0.50), rgb(0.87, 0.58, 0.45), rgb(0.85, 0.66, 0.62), rgb(0.82, 0.55, 0.48), rgb(0.80, 0.50, 0.42)]
        ),
        Theme(
            id: "candy", emoji: "🍬", price: 250,
            background: rgb(0.98, 0.94, 0.95),
            accent: rgb(0.96, 0.55, 0.72),
            digit: rgb(0.28, 0.16, 0.30),
            logo: rgb(0.30, 0.18, 0.33),
            bubbles: [rgb(0.97, 0.62, 0.74), rgb(0.98, 0.66, 0.62), rgb(0.99, 0.79, 0.63), rgb(0.98, 0.90, 0.58), rgb(0.80, 0.91, 0.62), rgb(0.62, 0.89, 0.74), rgb(0.64, 0.85, 0.93), rgb(0.74, 0.74, 0.93), rgb(0.86, 0.68, 0.91)]
        ),
        Theme(
            id: "space", emoji: "🌌", price: 300,
            background: rgb(0.09, 0.08, 0.18),
            accent: rgb(0.55, 0.45, 0.85),
            digit: rgb(0.12, 0.10, 0.22),
            logo: rgb(0.93, 0.94, 0.99),
            bubbles: [rgb(0.62, 0.74, 0.95), rgb(0.78, 0.72, 0.96), rgb(0.94, 0.72, 0.88), rgb(0.72, 0.88, 0.92), rgb(0.69, 0.83, 0.78), rgb(0.96, 0.84, 0.70), rgb(0.95, 0.76, 0.74), rgb(0.84, 0.80, 0.95), rgb(0.74, 0.79, 0.96)]
        ),
        Theme(
            id: "night", emoji: "🌙", price: 300,
            background: rgb(0.09, 0.11, 0.20),
            accent: rgb(0.55, 0.62, 0.85),
            digit: rgb(0.10, 0.13, 0.26),
            logo: rgb(0.90, 0.92, 0.98),
            bubbles: [rgb(0.78, 0.84, 0.94), rgb(0.72, 0.80, 0.92), rgb(0.68, 0.78, 0.86), rgb(0.74, 0.82, 0.80), rgb(0.80, 0.82, 0.74), rgb(0.86, 0.80, 0.78), rgb(0.84, 0.76, 0.86), rgb(0.76, 0.74, 0.90), rgb(0.70, 0.72, 0.88)]
        ),
    ]

    // MARK: - Possession

    var ownedIDs: Set<String> {
        let stored = defaults.stringArray(forKey: AppConfig.UserDefaultsKey.ownedThemes) ?? []
        return Set(stored).union([Theme.defaultID])
    }

    func owns(_ id: String) -> Bool { ownedIDs.contains(id) }

    /// Tente d'acheter un thème (débite les pièces). Retourne false si fonds insuffisants.
    @discardableResult
    func purchase(_ theme: Theme) -> Bool {
        if owns(theme.id) { return true }
        guard CoinManager.shared.spend(theme.price) else { return false }
        var owned = ownedIDs
        owned.insert(theme.id)
        defaults.set(Array(owned), forKey: AppConfig.UserDefaultsKey.ownedThemes)
        return true
    }

    // MARK: - Sélection

    var activeID: String {
        defaults.string(forKey: AppConfig.UserDefaultsKey.activeTheme) ?? Theme.defaultID
    }

    func setActive(_ id: String) {
        guard owns(id) else { return }
        defaults.set(id, forKey: AppConfig.UserDefaultsKey.activeTheme)
    }

    var active: Theme { theme(id: activeID) ?? themes[0] }

    func theme(id: String) -> Theme? { themes.first { $0.id == id } }
}
