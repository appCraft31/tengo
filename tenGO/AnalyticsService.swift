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
}
