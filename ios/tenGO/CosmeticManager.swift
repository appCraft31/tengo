//
//  CosmeticManager.swift
//  tenGO
//
//  Cosmétiques achetables non bloquants (purement esthétiques) :
//  - matières de bulles (aspect : mat, brillant, verre, néon…)
//  - styles de tracé (la ligne de connexion : néon, ruban, pointillé, encre…)
//  Le style « classic » est gratuit et toujours possédé. Persistance locale.
//

import Foundation

enum CosmeticKind {
    case bubbleStyle
    case trail
}

struct BubbleStyle {
    enum Kind { case classic, matte, glossy, glass, neon }
    let id: String
    let price: Int
    let kind: Kind
    var nameKey: String { "style.\(id)" }
}

struct TrailStyle {
    enum Kind { case classic, neon, ribbon, dotted, ink }
    let id: String
    let price: Int
    let kind: Kind
    var nameKey: String { "trail.\(id)" }
}

final class CosmeticManager {

    static let shared = CosmeticManager()
    private init() {}

    private let defaults = UserDefaults.standard
    static let defaultID = "classic"

    // MARK: - Catalogues

    let bubbleStyles: [BubbleStyle] = [
        BubbleStyle(id: "classic", price: 0, kind: .classic),
        BubbleStyle(id: "matte", price: 100, kind: .matte),
        BubbleStyle(id: "glossy", price: 120, kind: .glossy),
        BubbleStyle(id: "glass", price: 150, kind: .glass),
        BubbleStyle(id: "neon", price: 180, kind: .neon),
    ]

    let trails: [TrailStyle] = [
        TrailStyle(id: "classic", price: 0, kind: .classic),
        TrailStyle(id: "dotted", price: 100, kind: .dotted),
        TrailStyle(id: "ink", price: 100, kind: .ink),
        TrailStyle(id: "ribbon", price: 120, kind: .ribbon),
        TrailStyle(id: "neon", price: 150, kind: .neon),
    ]

    // MARK: - Possession / sélection (générique par catégorie)

    private func ownedKey(_ kind: CosmeticKind) -> String {
        kind == .bubbleStyle ? AppConfig.UserDefaultsKey.ownedBubbleStyles : AppConfig.UserDefaultsKey.ownedTrails
    }
    private func activeKey(_ kind: CosmeticKind) -> String {
        kind == .bubbleStyle ? AppConfig.UserDefaultsKey.activeBubbleStyle : AppConfig.UserDefaultsKey.activeTrail
    }

    func owns(_ kind: CosmeticKind, _ id: String) -> Bool {
        id == CosmeticManager.defaultID || Set(defaults.stringArray(forKey: ownedKey(kind)) ?? []).contains(id)
    }

    @discardableResult
    func purchase(_ kind: CosmeticKind, id: String, price: Int) -> Bool {
        if owns(kind, id) { return true }
        guard CoinManager.shared.spend(price) else { return false }
        var owned = Set(defaults.stringArray(forKey: ownedKey(kind)) ?? [])
        owned.insert(id)
        defaults.set(Array(owned), forKey: ownedKey(kind))
        return true
    }

    func activeID(_ kind: CosmeticKind) -> String {
        defaults.string(forKey: activeKey(kind)) ?? CosmeticManager.defaultID
    }

    func setActive(_ kind: CosmeticKind, _ id: String) {
        guard owns(kind, id) else { return }
        defaults.set(id, forKey: activeKey(kind))
    }

    // MARK: - Accès rapides

    var activeBubbleStyle: BubbleStyle {
        bubbleStyles.first { $0.id == activeID(.bubbleStyle) } ?? bubbleStyles[0]
    }
    var activeTrail: TrailStyle {
        trails.first { $0.id == activeID(.trail) } ?? trails[0]
    }
}
