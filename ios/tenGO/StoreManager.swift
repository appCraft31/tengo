//
//  StoreManager.swift
//  tenGO
//
//  Achats intégrés (StoreKit 2) de packs de pièces (consommables, vraie monnaie).
//  Les produits doivent être créés dans App Store Connect avec ces identifiants.
//  Un fichier tenGO.storekit permet de tester localement (config dans le scheme).
//

import Foundation
import StoreKit

@MainActor
final class StoreManager {

    static let shared = StoreManager()

    /// Identifiants des packs (à créer dans App Store Connect, type « Consommable »).
    static let coinAmounts: [String: Int] = [
        "com.tengo.coins.tier1": 500,
        "com.tengo.coins.tier2": 1200,
        "com.tengo.coins.tier3": 3000,
        "com.tengo.coins.tier4": 6500,
    ]
    /// Mod « sans pub » (non-consommable).
    static let adFreeProductID = AdFreeManager.productID

    static var productIDs: [String] { Array(coinAmounts.keys) + [adFreeProductID] }

    /// Packs de pièces uniquement (consommés par la boutique).
    private(set) var products: [Product] = []
    /// Mod « sans pub », tenu à part pour ne pas polluer l'onglet Pièces.
    private(set) var adFreeProduct: Product?
    private var updatesTask: Task<Void, Never>?

    private init() {
        // Capte les transactions finalisées hors de l'app (ou non terminées).
        updatesTask = listenForTransactions()
    }

    deinit { updatesTask?.cancel() }

    /// Pièces créditées par ce produit.
    func coins(for product: Product) -> Int {
        StoreManager.coinAmounts[product.id] ?? 0
    }

    /// Charge les produits depuis l'App Store (ou la config StoreKit locale).
    /// Les packs de pièces sont triés par quantité croissante ; le mod sans pub
    /// est mis de côté (il ne s'affiche pas dans l'onglet Pièces).
    func loadProducts() async {
        do {
            let fetched = try await Product.products(for: StoreManager.productIDs)
            adFreeProduct = fetched.first { $0.id == StoreManager.adFreeProductID }
            products = fetched
                .filter { StoreManager.coinAmounts[$0.id] != nil }
                .sorted {
                    (StoreManager.coinAmounts[$0.id] ?? 0) < (StoreManager.coinAmounts[$1.id] ?? 0)
                }
        } catch {
            print("[Store] Échec chargement produits : \(error.localizedDescription)")
        }
    }

    /// Restaure les achats non-consommables (obligatoire Apple pour le mod sans pub).
    /// - Returns: true si le mod est possédé à l'issue de la restauration.
    @discardableResult
    func restorePurchases() async -> Bool {
        do {
            try await AppStore.sync()
        } catch {
            print("[Store] Échec restauration : \(error.localizedDescription)")
        }
        await AdFreeManager.shared.refreshEntitlements()
        return AdFreeManager.shared.isPurchased
    }

    /// Lance l'achat. Crédite les pièces et finalise la transaction si succès.
    @discardableResult
    func purchase(_ product: Product) async -> Bool {
        do {
            let result = try await product.purchase()
            switch result {
            case .success(let verification):
                guard case .verified(let transaction) = verification else { return false }
                credit(transaction)
                AnalyticsService.purchase(product: product, transaction: transaction)
                await transaction.finish()
                return true
            case .userCancelled, .pending:
                return false
            @unknown default:
                return false
            }
        } catch {
            print("[Store] Échec achat : \(error.localizedDescription)")
            return false
        }
    }

    // MARK: - Privé

    /// Applique le contenu d'une transaction vérifiée : pièces ou mod sans pub.
    /// Utilisé aussi bien à l'achat que pour les transactions reçues hors de
    /// l'app (autre appareil, restauration) — d'où le cas non-consommable ici.
    private func credit(_ transaction: Transaction) {
        if transaction.productID == StoreManager.adFreeProductID {
            // Une transaction révoquée (remboursement) arrive aussi par ici.
            AdFreeManager.shared.setPurchased(transaction.revocationDate == nil)
            return
        }
        let amount = StoreManager.coinAmounts[transaction.productID] ?? 0
        if amount > 0 { CoinManager.shared.add(amount) }
    }

    private func listenForTransactions() -> Task<Void, Never> {
        Task.detached {
            for await result in Transaction.updates {
                guard case .verified(let transaction) = result else { continue }
                await self.credit(transaction)
                await transaction.finish()
            }
        }
    }
}
