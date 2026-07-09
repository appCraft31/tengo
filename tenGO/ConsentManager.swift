//
//  ConsentManager.swift
//  tenGO
//
//  Gestion du consentement : App Tracking Transparency (ATT) d'abord,
//  puis consentement publicitaire GDPR (UMP). Doit être exécuté avant
//  d'initialiser AdMob.
//
//  Flow (guideline Apple 5.1.1(iv) : aucun tracking ni prompt cookies
//  après un refus ATT) :
//    1. ATT requestTrackingAuthorization (iOS)
//    2. UMP requestConsentInfoUpdate (rafraîchit canRequestAds et
//       privacyOptionsRequirementStatus, sans afficher de formulaire)
//    3. Formulaire GDPR (loadAndPresentIfRequired) UNIQUEMENT si ATT autorisé
//    4. Selon le résultat : pubs personnalisées, non personnalisées
//       (publisherPrivacyPersonalizationState = .disabled + npa=1),
//       ou pas de pub ; consentement Firebase (Analytics.setConsent) aligné
//    5. Callback pour lancer MobileAds.start()
//

import Foundation
import UIKit
import UserMessagingPlatform
import AppTrackingTransparency
import GoogleMobileAds
import FirebaseAnalytics

/// Résultat du flux de consentement, consommé par GameViewController
/// (démarrage des pubs) et Request.consentAware() (npa).
enum AdConsentOutcome {
    /// ATT autorisé + consentement UMP exploitable → pubs personnalisées.
    case personalizedAds
    /// ATT refusé mais requêtes pub permises (hors EEA, ou consentement
    /// UMP déjà stocké) → pubs non personnalisées uniquement.
    case nonPersonalizedAds
    /// Pas de consentement UMP exploitable (EEA sans formulaire présenté)
    /// → aucune requête pub.
    case noAds
}

final class ConsentManager {
    static let shared = ConsentManager()
    private init() {}

    /// Dernier résultat du flux, lu par Request.consentAware().
    private(set) var lastOutcome: AdConsentOutcome = .noAds

    /// Le formulaire « options de confidentialité » UMP a-t-il un sens ici ?
    /// (.unknown tant que requestConsentInfoUpdate n'a pas tourné)
    var isPrivacyOptionsRequired: Bool {
        ConsentInformation.shared.privacyOptionsRequirementStatus == .required
    }

    /// Orchestration du flux de consentement.
    /// - Parameter viewController : présenter le formulaire GDPR si nécessaire
    /// - Parameter completion : appelé (main thread) avec le mode publicitaire autorisé
    func gatherConsent(from viewController: UIViewController,
                       completion: @escaping (AdConsentOutcome) -> Void) {
        requestATT { [weak self] attAuthorized in
            let parameters = RequestParameters()

            #if DEBUG
            let debug = DebugSettings()
            debug.geography = .EEA           // simule l'EEA pour tester le formulaire
            parameters.debugSettings = debug
            #endif

            // Toujours rafraîchir l'info UMP (canRequestAds,
            // privacyOptionsRequirementStatus), sans afficher de formulaire.
            ConsentInformation.shared.requestConsentInfoUpdate(with: parameters) { error in
                if let error = error {
                    print("[Consent] update error : \(error.localizedDescription)")
                }

                guard attAuthorized else {
                    // Refus ATT → aucun prompt cookies (motif du rejet 5.1.1(iv)).
                    self?.finish(attAuthorized: false, completion: completion)
                    return
                }

                // Présente le formulaire si requis (sinon ne fait rien)
                ConsentForm.loadAndPresentIfRequired(from: viewController) { error in
                    if let error = error {
                        print("[Consent] form error : \(error.localizedDescription)")
                    }
                    self?.finish(attAuthorized: true, completion: completion)
                }
            }
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
        let canRequestAds = ConsentInformation.shared.canRequestAds
        let outcome: AdConsentOutcome
        if attAuthorized && canRequestAds {
            outcome = .personalizedAds
        } else if canRequestAds {
            outcome = .nonPersonalizedAds
            // Force le non-personnalisé pour toute la session (à poser avant
            // MobileAds.start / premier load).
            MobileAds.shared.requestConfiguration.publisherPrivacyPersonalizationState = .disabled
        } else {
            outcome = .noAds
        }

        // Consent mode Firebase : les signaux publicitaires ne sont accordés
        // qu'en mode personnalisé (défauts refusés via Info.plist
        // GOOGLE_ANALYTICS_DEFAULT_ALLOW_*).
        // FirebaseAnalytics.ConsentStatus — qualifié car UMP expose aussi un ConsentStatus.
        let adConsent: FirebaseAnalytics.ConsentStatus = (outcome == .personalizedAds) ? .granted : .denied
        Analytics.setConsent([
            .analyticsStorage: .granted,
            .adStorage: adConsent,
            .adUserData: adConsent,
            .adPersonalization: adConsent,
        ])

        lastOutcome = outcome
        DispatchQueue.main.async { completion(outcome) }
    }

    /// Permet à l'utilisateur de réviser son consentement (bouton "Options de confidentialité")
    func presentPrivacyOptions(from viewController: UIViewController,
                                completion: @escaping (Error?) -> Void) {
        ConsentForm.presentPrivacyOptionsForm(from: viewController,
                                              completionHandler: completion)
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
