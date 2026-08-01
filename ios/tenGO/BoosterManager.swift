//
//  BoosterManager.swift
//  tenGO
//
//  Boosters consommables utilisables en cours de partie. Achetés avec des
//  pièces (CoinManager) et stockés en inventaire local (UserDefaults).
//
//  - Indice  : surligne un chemin valide (somme 10) — s'appuie sur PathSolver.
//  - Mélange : régénère les valeurs de la grille quand on est coincé.
//  - Marteau : supprime une bulle au choix, puis la gravité s'applique.
//
//  Aucun backend : l'inventaire est purement local, comme le solde de pièces.
//

import Foundation

enum Booster: String, CaseIterable {
    case hint    // indice
    case shuffle // mélange
    case hammer  // marteau

    /// Prix d'achat unitaire en pièces.
    var price: Int {
        switch self {
        case .hint:    return 30
        case .shuffle: return 50
        case .hammer:  return 80
        }
    }

    /// Lot vendu en boutique (quantité + prix remisé : 5 pour le prix de 4).
    var bundleQuantity: Int { 5 }
    var bundlePrice: Int { price * 4 }

    /// Nom de l'asset vectoriel (SVG) de l'icône dans Assets.xcassets.
    var assetName: String {
        switch self {
        case .hint:    return "BoosterHint"
        case .shuffle: return "BoosterShuffle"
        case .hammer:  return "BoosterHammer"
        }
    }

    /// Libellé court localisable (clé de String Catalog).
    var titleKey: String.LocalizationValue {
        switch self {
        case .hint:    return "booster.hint"
        case .shuffle: return "booster.shuffle"
        case .hammer:  return "booster.hammer"
        }
    }

    var defaultTitle: String {
        switch self {
        case .hint:    return "Indice"
        case .shuffle: return "Mélange"
        case .hammer:  return "Marteau"
        }
    }

    /// Libellé résolu — unique point de vérité, à utiliser partout plutôt que
    /// de refaire un switch (la boutique en avait un doublon).
    var title: String {
        switch self {
        case .hint:    return String(localized: "booster.hint", defaultValue: "Indice")
        case .shuffle: return String(localized: "booster.shuffle", defaultValue: "Mélange")
        case .hammer:  return String(localized: "booster.hammer", defaultValue: "Marteau")
        }
    }

    /// Phrase affichée sur la carte boutique : ce que fait le booster.
    var details: String {
        switch self {
        case .hint:
            return String(localized: "booster.hint_desc",
                          defaultValue: "Fait clignoter un chemin gagnant quand tu es bloqué.")
        case .shuffle:
            return String(localized: "booster.shuffle_desc",
                          defaultValue: "Redistribue toutes les valeurs de la grille. Ton score est conservé.")
        case .hammer:
            return String(localized: "booster.hammer_desc",
                          defaultValue: "Fait éclater la bulle de ton choix, les autres retombent.")
        }
    }

    /// Phrase du coach-mark affiché en jeu à la première possession.
    var coachText: String {
        switch self {
        case .hint:
            return String(localized: "booster.hint_coach",
                          defaultValue: "Bloqué ? L'indice fait clignoter un chemin gagnant.")
        case .shuffle:
            return String(localized: "booster.shuffle_coach",
                          defaultValue: "Le mélange redistribue toutes les valeurs. Ton score est conservé.")
        case .hammer:
            return String(localized: "booster.hammer_coach",
                          defaultValue: "Touche le marteau, puis la bulle à faire éclater.")
        }
    }
}

final class BoosterManager {

    static let shared = BoosterManager()
    private init() {}

    private let defaults = UserDefaults.standard

    private func key(for booster: Booster) -> String {
        AppConfig.UserDefaultsKey.boosterInventoryPrefix + booster.rawValue
    }

    // MARK: - Inventaire

    /// Quantité possédée d'un booster.
    func count(_ booster: Booster) -> Int {
        defaults.integer(forKey: key(for: booster))
    }

    /// Crédite l'inventaire (achat ou récompense).
    func grant(_ booster: Booster, quantity: Int = 1) {
        guard quantity > 0 else { return }
        defaults.set(count(booster) + quantity, forKey: key(for: booster))
    }

    // MARK: - Achat

    /// Achète un booster en débitant le solde de pièces. Retourne false si solde insuffisant.
    @discardableResult
    func purchase(_ booster: Booster, quantity: Int = 1) -> Bool {
        let cost = booster.price * max(quantity, 1)
        guard CoinManager.shared.spend(cost) else { return false }
        grant(booster, quantity: max(quantity, 1))
        return true
    }

    /// Achète un lot (quantité remisée) en boutique. Retourne false si solde insuffisant.
    @discardableResult
    func purchaseBundle(_ booster: Booster) -> Bool {
        guard CoinManager.shared.spend(booster.bundlePrice) else { return false }
        grant(booster, quantity: booster.bundleQuantity)
        return true
    }

    // MARK: - Consommation

    /// Consomme une unité du booster si disponible. Retourne false si l'inventaire est vide.
    @discardableResult
    func consume(_ booster: Booster) -> Bool {
        let current = count(booster)
        guard current > 0 else { return false }
        defaults.set(current - 1, forKey: key(for: booster))
        return true
    }
}
