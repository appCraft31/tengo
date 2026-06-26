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

    /// Nom du symbole SF Symbols utilisé pour l'icône du bouton.
    var symbolName: String {
        switch self {
        case .hint:    return "lightbulb.fill"
        case .shuffle: return "shuffle"
        case .hammer:  return "hammer.fill"
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
