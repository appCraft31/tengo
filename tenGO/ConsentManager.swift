//
//  ConsentManager.swift
//  tenGO
//
//  Gestion du consentement publicitaire GDPR (UMP) + App Tracking
//  Transparency (ATT). Doit être exécuté avant d'initialiser AdMob.
//
//  Flow :
//    1. UMP requestConsentInfoUpdate
//    2. Si requis : loadAndPresentIfRequired (formulaire GDPR)
//    3. Si canRequestAds → demander l'ATT (iOS 14.5+)
//    4. Callback pour lancer MobileAds.start()
//

import Foundation
import UIKit
import UserMessagingPlatform
import AppTrackingTransparency

final class ConsentManager {
    static let shared = ConsentManager()
    private init() {}

    /// Orchestration du flow de consentement.
    /// - Parameter viewController : présenter le formulaire GDPR si nécessaire
    /// - Parameter completion : appelé lorsque l'app peut initialiser AdMob
    func gatherConsent(from viewController: UIViewController,
                       completion: @escaping () -> Void) {
        let parameters = RequestParameters()

        #if DEBUG
        let debug = DebugSettings()
        debug.geography = .EEA           // simule l'EEA pour tester le formulaire
        parameters.debugSettings = debug
        #endif

        ConsentInformation.shared.requestConsentInfoUpdate(with: parameters) { [weak self] error in
            if let error = error {
                print("[Consent] update error : \(error.localizedDescription)")
                self?.proceedWithATT(completion: completion)
                return
            }

            // Présente le formulaire si requis (sinon ne fait rien)
            ConsentForm.loadAndPresentIfRequired(from: viewController) { error in
                if let error = error {
                    print("[Consent] form error : \(error.localizedDescription)")
                }
                self?.proceedWithATT(completion: completion)
            }
        }
    }

    /// Demande l'autorisation ATT (iOS 14.5+), puis continue.
    private func proceedWithATT(completion: @escaping () -> Void) {
        let done = {
            DispatchQueue.main.async { completion() }
        }
        if #available(iOS 14.5, *) {
            // requestTrackingAuthorization doit impérativement être appelé depuis
            // le main thread — les callbacks UMP (Google) ne le garantissent pas.
            DispatchQueue.main.async {
                ATTrackingManager.requestTrackingAuthorization { _ in done() }
            }
        } else {
            done()
        }
    }

    /// Permet à l'utilisateur de réviser son consentement (bouton "Options de confidentialité")
    func presentPrivacyOptions(from viewController: UIViewController,
                                completion: @escaping (Error?) -> Void) {
        ConsentForm.presentPrivacyOptionsForm(from: viewController,
                                              completionHandler: completion)
    }
}
