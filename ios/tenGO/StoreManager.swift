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

    /// Bonus de bienvenue crédité avec l'achat « sans pub ».
    static let adFreeBonusCoins = 500

    static var productIDs: [String] { Array(coinAmounts.keys) + [adFreeProductID] }

    /// Issue d'un achat : chaque écran doit en donner un retour visible.
    enum PurchaseOutcome {
        case success
        case cancelled
        /// En attente de validation (« Demander l'autorisation d'achat », SCA) :
        /// la transaction arrivera plus tard par `Transaction.updates`.
        case pending
        case failed

        /// Message à afficher quand l'achat n'a pas abouti (nil = succès, que
        /// chaque écran célèbre à sa façon).
        var message: String? {
            switch self {
            case .success:
                return nil
            case .cancelled:
                return String(localized: "shop.purchase_cancelled", defaultValue: "Achat annulé")
            case .pending:
                return String(localized: "shop.purchase_pending",
                              defaultValue: "Achat en attente de validation. Il s'activera dès son approbation.")
            case .failed:
                return String(localized: "shop.purchase_failed",
                              defaultValue: "L'achat n'a pas abouti. Réessaie dans un instant.")
            }
        }
    }

    /// Packs de pièces uniquement (consommés par la boutique).
    private(set) var products: [Product] = []
    /// Mod « sans pub », tenu à part pour ne pas polluer l'onglet Pièces.
    private(set) var adFreeProduct: Product?
    /// Prix localisé du mod « sans pub », nil tant que StoreKit n'a pas répondu.
    /// Seule source de prix affichable : jamais de valeur en dur.
    var adFreeDisplayPrice: String? { adFreeProduct?.displayPrice }
    /// L'offre « sans pub » de fin de partie a déjà été montrée : elle ne
    /// revient pas avant le prochain lancement.
    var adFreeOfferShownThisSession = false
    private var loadTask: Task<Void, Never>?
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
    ///
    /// Appelé au lancement puis par la boutique : un appel concurrent attend le
    /// chargement en cours au lieu d'en lancer un second.
    func loadProducts() async {
        if let loadTask {
            await loadTask.value
            return
        }
        let task = Task { await fetchProducts() }
        loadTask = task
        await task.value
        loadTask = nil
    }

    #if DEBUG
    /// QA (`QA_STORE_FAIL=N`) : les N premiers chargements échouent, pour voir
    /// la boutique sans prix puis le nouvel essai aboutir.
    private var simulatedLoadFailures = Int(ProcessInfo.processInfo.environment["QA_STORE_FAIL"] ?? "") ?? 0
    #endif

    private func fetchProducts() async {
        #if DEBUG
        if simulatedLoadFailures > 0 {
            simulatedLoadFailures -= 1
            try? await Task.sleep(nanoseconds: 1_500_000_000)
            print("[Store] Échec chargement produits simulé (QA)")
            return
        }
        #endif
        do {
            let fetched = try await Product.products(for: StoreManager.productIDs)
            adFreeProduct = fetched.first { $0.id == StoreManager.adFreeProductID }
            products = fetched
                .filter { StoreManager.coinAmounts[$0.id] != nil }
                .sorted {
                    (StoreManager.coinAmounts[$0.id] ?? 0) < (StoreManager.coinAmounts[$1.id] ?? 0)
                }
            print("[Store] Produits chargés : \(fetched.count)/\(StoreManager.productIDs.count)")
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
    /// - Parameter source: écran d'origine, pour la mesure (`shop`, `game_over`).
    @discardableResult
    func purchase(_ product: Product, source: String) async -> PurchaseOutcome {
        AnalyticsService.iapTapped(productID: product.id, source: source)
        do {
            let result = try await product.purchase()
            switch result {
            case .success(let verification):
                guard case .verified(let transaction) = verification else {
                    AnalyticsService.iapFailed(productID: product.id, source: source, reason: "unverified")
                    return .failed
                }
                credit(transaction)
                AnalyticsService.purchase(product: product, transaction: transaction)
                await transaction.finish()
                print("[Store] Achat réussi : \(product.id)")
                return .success
            case .userCancelled:
                AnalyticsService.iapCancelled(productID: product.id, source: source)
                return .cancelled
            case .pending:
                AnalyticsService.iapPending(productID: product.id, source: source)
                return .pending
            @unknown default:
                AnalyticsService.iapFailed(productID: product.id, source: source, reason: "unknown_result")
                return .failed
            }
        } catch {
            print("[Store] Échec achat : \(error.localizedDescription)")
            AnalyticsService.iapFailed(productID: product.id, source: source,
                                       reason: String(describing: error))
            return .failed
        }
    }

    /// Achat du mod « sans pub », avec son bonus de pièces. Partagé par la
    /// boutique et l'offre de fin de partie pour que la promesse soit la même.
    func purchaseAdFree(source: String) async -> PurchaseOutcome {
        guard let product = adFreeProduct else { return .failed }
        let outcome = await purchase(product, source: source)
        switch outcome {
        case .success:
            CoinManager.shared.add(StoreManager.adFreeBonusCoins)
        case .pending:
            // Le bonus est dû à l'approbation : `credit` le versera à ce moment-là.
            UserDefaults.standard.set(true, forKey: AppConfig.UserDefaultsKey.noAdsBonusPending)
        case .cancelled, .failed:
            break
        }
        return outcome
    }

    // MARK: - Privé

    /// Applique le contenu d'une transaction vérifiée : pièces ou mod sans pub.
    /// Utilisé aussi bien à l'achat que pour les transactions reçues hors de
    /// l'app (autre appareil, restauration) — d'où le cas non-consommable ici.
    private func credit(_ transaction: Transaction) {
        if transaction.productID == StoreManager.adFreeProductID {
            // Une transaction révoquée (remboursement) arrive aussi par ici.
            AdFreeManager.shared.setPurchased(transaction.revocationDate == nil)
            // Achat resté en attente puis approuvé : le bonus promis est versé ici.
            let defaults = UserDefaults.standard
            if transaction.revocationDate == nil,
               defaults.bool(forKey: AppConfig.UserDefaultsKey.noAdsBonusPending) {
                defaults.set(false, forKey: AppConfig.UserDefaultsKey.noAdsBonusPending)
                CoinManager.shared.add(StoreManager.adFreeBonusCoins)
            }
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
