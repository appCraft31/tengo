//
//  AnalyticsService.swift
//  tenGO
//
//  Point d'entrée unique pour le tracking Firebase Analytics.
//  Objectif : alimenter les rapports Firebase et permettre l'import de
//  conversions (achat, engagement) dans Google Ads pour piloter les campagnes.
//
//  Les événements standard (purchase, level_end, tutorial_complete,
//  ad_impression…) utilisent les constantes Firebase afin d'être reconnus
//  automatiquement par la console et par Google Ads.
//
//  NB : consent mode Google. Par défaut (Info.plist GOOGLE_ANALYTICS_DEFAULT_ALLOW_*),
//  analytics_storage est accordé mais ad_storage / ad_user_data /
//  ad_personalization sont refusés. ConsentManager n'accorde les signaux
//  publicitaires que si l'utilisateur a autorisé l'ATT (et le consentement
//  UMP le cas échéant).
//

import Foundation
import FirebaseAnalytics
import StoreKit
import GoogleMobileAds

enum AnalyticsService {

    // MARK: - Funnel d'engagement

    /// Le joueur a terminé le tutoriel (signal d'activation).
    static func tutorialComplete() {
        Analytics.logEvent(AnalyticsEventTutorialComplete, parameters: nil)
    }

    /// Fin d'une partie (normale ou défi). `won` distingue victoire / game over.
    static func levelEnd(mode: String, score: Int, won: Bool) {
        Analytics.logEvent(AnalyticsEventLevelEnd, parameters: [
            AnalyticsParameterLevelName: mode,
            AnalyticsParameterSuccess: won ? 1 : 0,
            AnalyticsParameterScore: score,
        ])
    }

    /// Le défi du jour a été joué (mesure de rétention / récurrence).
    static func dailyChallengePlayed(score: Int) {
        Analytics.logEvent("daily_challenge_played", parameters: [
            AnalyticsParameterScore: score,
        ])
    }

    // MARK: - Son
    //
    // Mesure l'intérêt réel du retour sonore : part des joueurs qui jouent
    // avec le son, et bascules dans les réglages. Noms identiques sur Android.

    /// Le joueur active ou coupe le son depuis les réglages.
    static func soundToggled(enabled: Bool) {
        Analytics.logEvent("sound_toggled", parameters: ["enabled": enabled ? 1 : 0])
        setSoundProperty(enabled: enabled)
    }

    /// Propriété utilisateur `sound_on` (« on » / « off ») : permet de
    /// segmenter tous les rapports selon que le joueur a le son ou non.
    static func setSoundProperty(enabled: Bool) {
        Analytics.setUserProperty(enabled ? "on" : "off", forName: "sound_on")
    }

    /// Propriété utilisateur `store_env` : `production` (App Store), `sandbox`
    /// (TestFlight, vérification Apple), `xcode` / `debug` (tests du développeur).
    /// Les essais d'achat en bac à sable — presque toujours annulés — se
    /// mêlaient jusqu'ici aux vrais joueurs dans l'entonnoir d'achat (TEN-2) :
    /// filtrer les rapports `iap_*` sur `store_env = production`.
    static func setStoreEnvironmentProperty() {
        #if DEBUG
        Analytics.setUserProperty("debug", forName: "store_env")
        #else
        guard #available(iOS 16.0, *) else { return }
        Task.detached(priority: .utility) {
            // `AppTransaction.shared` ne sollicite jamais l'utilisateur
            // (contrairement à `refresh()`).
            guard case .verified(let appTransaction)? = try? await AppTransaction.shared else { return }
            Analytics.setUserProperty(appTransaction.environment.rawValue.lowercased(), forName: "store_env")
        }
        #endif
    }

    // MARK: - Boosters
    //
    // Funnel visé :
    //   shop_guide:shown → opened → spend_virtual_currency
    //   → booster_coach_shown → booster_used
    // Les noms d'événements et de paramètres doivent rester IDENTIQUES sur
    // Android, sinon les rapports Firebase ne sont pas comparables.

    /// Achat d'un booster avec la monnaie du jeu.
    static func boosterPurchased(_ id: String, quantity: Int, cost: Int, source: String) {
        Analytics.logEvent(AnalyticsEventSpendVirtualCurrency, parameters: [
            AnalyticsParameterItemName: id,
            AnalyticsParameterVirtualCurrencyName: "coins",
            AnalyticsParameterValue: cost,
            "quantity": quantity,
            "source": source,
        ])
    }

    /// Remboursement du tutoriel d'achat (le premier indice est offert).
    static func boosterRefunded(_ id: String, amount: Int) {
        Analytics.logEvent(AnalyticsEventEarnVirtualCurrency, parameters: [
            AnalyticsParameterVirtualCurrencyName: "coins",
            AnalyticsParameterValue: amount,
            "item_name": id,
            "reason": "tutorial_refund",
        ])
    }

    /// Booster consommé en partie.
    static func boosterUsed(_ id: String) {
        Analytics.logEvent("booster_used", parameters: ["item_name": id])
    }

    /// Coach-mark d'usage affiché à la première possession.
    static func boosterCoachShown(_ id: String) {
        Analytics.logEvent("booster_coach_shown", parameters: ["item_name": id])
    }

    /// Étape du tutoriel d'achat : shown | opened | purchased | refunded | later | gaveup
    static func shopGuide(step: String) {
        Analytics.logEvent("shop_guide", parameters: ["step": step])
    }

    /// Boutique ouverte depuis la partie : empty_bar | game_over
    static func shopOpenedFromGame(reason: String) {
        Analytics.logEvent("shop_from_game", parameters: ["reason": reason])
    }

    // MARK: - Entonnoir d'achat
    //
    //   shop_opened / noads_offer_view → iap_tapped
    //   → purchase | iap_cancelled | iap_pending | iap_failed

    /// Boutique affichée. `source` : tab | game
    static func shopOpened(source: String) {
        logFunnel("shop_opened", parameters: ["source": source])
    }

    /// Offre « sans pub » vue. `source` : game_over (pastille après une
    /// interstitielle) | shop (carte en tête de boutique).
    /// `adsSeen` : interstitielles vues depuis la précédente offre (série qui
    /// a déclenché celle-ci), `adsToday` : interstitielles vues aujourd'hui.
    static func noAdsOfferView(source: String, adsSeen: Int? = nil, adsToday: Int? = nil) {
        var parameters: [String: Any] = ["source": source]
        if let adsSeen { parameters["ads_seen"] = adsSeen }
        if let adsToday { parameters["ads_today"] = adsToday }
        logFunnel("noads_offer_view", parameters: parameters)
    }

    /// Offre « sans pub » refusée (« Non merci »).
    static func noAdsOfferDeclined(source: String) {
        logFunnel("noads_offer_decline", parameters: ["source": source])
    }

    /// Achat en vraie monnaie abouti (transaction vérifiée). Complète
    /// l'entonnoir `iap_tapped` → `iap_success | iap_cancelled | iap_failed |
    /// iap_pending`, avec l'écran d'origine que `purchase` ne porte pas.
    /// `source = transaction_update` : achat approuvé hors de la feuille
    /// (« Demander l'autorisation », SCA, autre appareil).
    static func iapSucceeded(productID: String, source: String, environment: String?) {
        var parameters: [String: Any] = [
            AnalyticsParameterItemID: productID,
            "source": source,
        ]
        if let environment { parameters["store_env"] = environment }
        logFunnel("iap_success", parameters: parameters)
    }

    /// Le joueur a lancé un achat en vraie monnaie.
    static func iapTapped(productID: String, source: String) {
        logFunnel("iap_tapped", parameters: [
            AnalyticsParameterItemID: productID,
            "source": source,
        ])
    }

    /// Achat échoué (erreur StoreKit, transaction non vérifiée).
    static func iapFailed(productID: String, source: String, reason: String) {
        logFunnel("iap_failed", parameters: [
            AnalyticsParameterItemID: productID,
            "source": source,
            // Firebase tronque les valeurs de paramètre au-delà de 100 caractères.
            "reason": String(reason.prefix(100)),
        ])
    }

    /// Feuille d'achat refermée sans payer.
    static func iapCancelled(productID: String, source: String) {
        logFunnel("iap_cancelled", parameters: [
            AnalyticsParameterItemID: productID,
            "source": source,
        ])
    }

    /// Achat en attente de validation (autorisation parentale, SCA).
    static func iapPending(productID: String, source: String) {
        logFunnel("iap_pending", parameters: [
            AnalyticsParameterItemID: productID,
            "source": source,
        ])
    }

    /// Envoi d'un événement de l'entonnoir, tracé en console en Debug pour
    /// pouvoir le suivre pendant un test sur appareil.
    private static func logFunnel(_ name: String, parameters: [String: Any]) {
        #if DEBUG
        print("[Analytics] \(name) \(parameters)")
        #endif
        Analytics.logEvent(name, parameters: parameters)
    }

    // MARK: - Monétisation (conversion clé pour Google Ads)

    /// Achat d'un pack de pièces validé. Loggue la valeur réelle pour le ROAS.
    static func purchase(product: Product, transaction: Transaction) {
        let value = NSDecimalNumber(decimal: product.price).doubleValue
        let currency = product.priceFormatStyle.currencyCode
        Analytics.logEvent(AnalyticsEventPurchase, parameters: [
            AnalyticsParameterValue: value,
            AnalyticsParameterCurrency: currency,
            AnalyticsParameterTransactionID: String(transaction.id),
            AnalyticsParameterItems: [[
                AnalyticsParameterItemID: product.id,
                AnalyticsParameterItemName: product.displayName,
            ]],
        ])
    }

    /// Impression publicitaire valorisée (LTV pub). Alimente le revenu par cohorte.
    static func adImpression(adValue: AdValue, format: String, unitName: String) {
        Analytics.logEvent(AnalyticsEventAdImpression, parameters: [
            AnalyticsParameterAdPlatform: "AdMob",
            AnalyticsParameterAdSource: "AdMob",
            AnalyticsParameterAdFormat: format,
            AnalyticsParameterAdUnitName: unitName,
            AnalyticsParameterValue: adValue.value.doubleValue,
            AnalyticsParameterCurrency: adValue.currencyCode,
        ])
    }

    // MARK: - Vidéo récompensée
    //
    //   rewarded_offer_view → rewarded_started → rewarded_completed
    //   `placement` : menu_coins | hint | second_chance

    /// Proposition de vidéo affichée (indice gratuit, seconde chance).
    static func rewardedOfferView(placement: String) {
        logFunnel("rewarded_offer_view", parameters: ["placement": placement])
    }

    /// Le joueur a accepté : la vidéo démarre.
    static func rewardedStarted(placement: String) {
        logFunnel("rewarded_started", parameters: ["placement": placement])
    }

    /// Vidéo regardée jusqu'au bout : récompense versée.
    static func rewardedCompleted(placement: String) {
        logFunnel("rewarded_completed", parameters: ["placement": placement])
    }
}
