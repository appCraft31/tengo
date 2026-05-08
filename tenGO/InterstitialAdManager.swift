//
//  InterstitialAdManager.swift
//  tenGO
//

import GoogleMobileAds
import UIKit

final class InterstitialAdManager: NSObject {
    static let shared = InterstitialAdManager()
    private override init() {}

    private var interstitial: InterstitialAd?
    private var isReady = false

    // MARK: - Chargement

    func loadAd() {
        let adUnitID: String
        #if DEBUG
        adUnitID = "ca-app-pub-3940256099942544/4411468910"
        #else
        adUnitID = "ca-app-pub-4352408747876735/5201754193"
        #endif

        InterstitialAd.load(with: adUnitID, request: Request()) { [weak self] ad, error in
            guard let self else { return }
            if let error {
                print("[AdMob] Interstitiel échec chargement : \(error.localizedDescription)")
                self.isReady = false
                return
            }
            self.interstitial = ad
            self.interstitial?.fullScreenContentDelegate = self
            self.isReady = true
            print("[AdMob] Interstitiel prêt")
        }
    }

    // MARK: - Affichage

    /// Affiche l'interstitiel si disponible, puis appelle `completion`.
    /// Si non disponible, `completion` est appelé immédiatement.
    func showIfReady(from viewController: UIViewController, completion: @escaping () -> Void) {
        guard isReady, let ad = interstitial else {
            completion()
            return
        }
        isReady = false
        pendingCompletion = completion
        ad.present(from: viewController)
    }

    private var pendingCompletion: (() -> Void)?
}

// MARK: - FullScreenContentDelegate

extension InterstitialAdManager: FullScreenContentDelegate {

    func adDidDismissFullScreenContent(_ ad: FullScreenPresentingAd) {
        pendingCompletion?()
        pendingCompletion = nil
        loadAd() // recharge immédiatement pour la prochaine fois
    }

    func ad(_ ad: FullScreenPresentingAd, didFailToPresentFullScreenContentWithError error: Error) {
        print("[AdMob] Interstitiel échec affichage : \(error.localizedDescription)")
        pendingCompletion?()
        pendingCompletion = nil
        loadAd()
    }
}
