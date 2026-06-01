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
    static var productIDs: [String] { Array(coinAmounts.keys) }

    private(set) var products: [Product] = []
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
    /// Triés par quantité de pièces croissante.
    func loadProducts() async {
        do {
            let fetched = try await Product.products(for: StoreManager.productIDs)
            products = fetched.sorted {
                (StoreManager.coinAmounts[$0.id] ?? 0) < (StoreManager.coinAmounts[$1.id] ?? 0)
            }
        } catch {
            print("[Store] Échec chargement produits : \(error.localizedDescription)")
        }
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

    private func credit(_ transaction: Transaction) {
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
