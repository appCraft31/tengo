//
//  ConsentManager.swift
//  tenGO
//
//  Gestion du consentement : App Tracking Transparency (ATT) uniquement,
//  sans formulaire UMP. Doit être exécuté avant d'initialiser AdMob.
//
//  Flow (guideline Apple 5.1.1(iv) : aucun tracking après un refus ATT) :
//    1. ATT requestTrackingAuthorization (iOS)
//    2. Selon le résultat : pubs personnalisées, ou non personnalisées
//       (publisherPrivacyPersonalizationState = .disabled + npa=1) ;
//       consentement Firebase (Analytics.setConsent) aligné
//    3. Callback pour lancer MobileAds.start()
//

import Foundation
import UIKit
import AppTrackingTransparency
import GoogleMobileAds
import FirebaseAnalytics

/// Résultat du flux de consentement, consommé par GameViewController
/// (démarrage des pubs) et Request.consentAware() (npa).
enum AdConsentOutcome {
    /// ATT autorisé → pubs personnalisées.
    case personalizedAds
    /// ATT refusé → pubs non personnalisées uniquement.
    case nonPersonalizedAds
}

final class ConsentManager {
    static let shared = ConsentManager()
    private init() {}

    /// Dernier résultat du flux, lu par Request.consentAware().
    private(set) var lastOutcome: AdConsentOutcome = .nonPersonalizedAds

    /// Orchestration du flux de consentement.
    /// - Parameter completion : appelé (main thread) avec le mode publicitaire autorisé
    func gatherConsent(completion: @escaping (AdConsentOutcome) -> Void) {
        requestATT { [weak self] attAuthorized in
            self?.finish(attAuthorized: attAuthorized, completion: completion)
        }
    }

    /// Demande l'autorisation ATT. Seul .authorized accorde le tracking ;
    /// .denied / .restricted / .notDetermined (prompt non présentable) → refus.
    private func requestATT(completion: @escaping (Bool) -> Void) {
        // requestTrackingAuthorization doit impérativement être appelé depuis
        // le main thread, fenêtre active (sinon le statut reste .notDetermined
        // et iOS re-proposera le prompt au prochain lancement).
        DispatchQueue.main.async {
            ATTrackingManager.requestTrackingAuthorization { status in
                DispatchQueue.main.async { completion(status == .authorized) }
            }
        }
    }

    /// Détermine le mode publicitaire et aligne AdMob + Firebase dessus.
    private func finish(attAuthorized: Bool,
                        completion: @escaping (AdConsentOutcome) -> Void) {
        let outcome: AdConsentOutcome
        if attAuthorized {
            outcome = .personalizedAds
        } else {
            outcome = .nonPersonalizedAds
            // Force le non-personnalisé pour toute la session (à poser avant
            // MobileAds.start / premier load).
            MobileAds.shared.requestConfiguration.publisherPrivacyPersonalizationState = .disabled
        }

        // Consent mode Firebase : les signaux publicitaires ne sont accordés
        // qu'en mode personnalisé (défauts refusés via Info.plist
        // GOOGLE_ANALYTICS_DEFAULT_ALLOW_*).
        let adConsent: ConsentStatus = (outcome == .personalizedAds) ? .granted : .denied
        Analytics.setConsent([
            .analyticsStorage: .granted,
            .adStorage: adConsent,
            .adUserData: adConsent,
            .adPersonalization: adConsent,
        ])

        lastOutcome = outcome
        DispatchQueue.main.async { completion(outcome) }
    }
}

extension Request {
    /// Requête pub respectant le consentement : hors mode personnalisé,
    /// ajoute npa=1 en complément du publisherPrivacyPersonalizationState.
    static func consentAware() -> Request {
        let request = Request()
        if ConsentManager.shared.lastOutcome != .personalizedAds {
            let extras = Extras()
            extras.additionalParameters = ["npa": "1"]
            request.register(extras)
        }
        return request
    }
}
