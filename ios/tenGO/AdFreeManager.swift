//
//  AdFreeManager.swift
//  tenGO
//
//  Mod « sans pub » (achat non-consommable) : coupe la bannière et les
//  interstitielles. La pub récompensée volontaire (+ pièces) reste disponible.
//
//  Source de vérité = Transaction.currentEntitlements (survit à une
//  réinstallation, se propage entre appareils). UserDefaults ne sert que de
//  cache pour couper les pubs dès le lancement, avant la réponse de StoreKit.
//
//  Volontairement non isolé au main actor : les gardes de coupure des pubs sont
//  lues depuis les managers AdMob (contextes non isolés). L'état n'est écrit que
//  depuis le main thread (achat, restauration, listener StoreKit).
//

import Foundation
import StoreKit

final class AdFreeManager {

    static let shared = AdFreeManager()

    /// Identifiant du produit non-consommable (App Store Connect + tenGO.storekit).
    static let productID = "com.tengo.noads"

    private let defaults = UserDefaults.standard

    private init() {}

    /// Le mod est possédé. Lu depuis le cache local (réponse immédiate au
    /// lancement, hors ligne) ; rafraîchi par refreshEntitlements().
    var isPurchased: Bool {
        #if DEBUG
        // QA : simule l'achat sans passer par StoreKit (la config .storekit n'est
        // appliquée que par un Run depuis Xcode, pas par simctl).
        if ProcessInfo.processInfo.environment["ADFREE_FORCE"] == "1" { return true }
        #endif
        return defaults.bool(forKey: AppConfig.UserDefaultsKey.noAdsPurchased)
    }

    /// Re-dérive l'état depuis StoreKit. À appeler au lancement et après une
    /// restauration.
    ///
    /// Le cache est volontairement « collant » : une liste d'entitlements vide
    /// ne le remet PAS à false. Elle peut l'être parce que l'utilisateur est
    /// hors ligne ou déconnecté de l'App Store — dégrader ici ferait réapparaître
    /// les pubs chez un client payant. Seule une révocation explicite
    /// (remboursement) désactive le mod.
    @MainActor
    func refreshEntitlements() async {
        for await result in Transaction.currentEntitlements {
            guard case .verified(let transaction) = result,
                  transaction.productID == Self.productID else { continue }
            apply(transaction.revocationDate == nil)
            return
        }
    }

    /// Applique l'état d'une transaction (achat, restauration, remboursement).
    @MainActor
    func setPurchased(_ owned: Bool) {
        apply(owned)
    }

    @MainActor
    private func apply(_ owned: Bool) {
        guard owned != isPurchased else { return }
        defaults.set(owned, forKey: AppConfig.UserDefaultsKey.noAdsPurchased)
        print("[Store] Mod sans pub \(owned ? "activé" : "désactivé")")
        NotificationCenter.default.post(name: .tenGOAdFreeChanged, object: nil)
    }
}
