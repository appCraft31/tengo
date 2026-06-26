//
//  RewardedAdManager.swift
//  tenGO
//
//  Interstitiel AVEC récompense (rewarded interstitial) : l'utilisateur regarde
//  une pub volontairement pour gagner des pièces. Chargé après MobileAds.start,
//  rechargé après chaque diffusion.
//

import GoogleMobileAds
import UIKit

final class RewardedAdManager: NSObject {

    static let shared = RewardedAdManager()
    private override init() {}

    private var rewardedAd: RewardedInterstitialAd?
    private(set) var isReady = false

    // MARK: - Chargement

    func loadAd() {
        let adUnitID: String
        #if DEBUG
        adUnitID = "ca-app-pub-3940256099942544/6978759866"   // ID de test Google (rewarded interstitial)
        #else
        adUnitID = "ca-app-pub-4352408747876735/2440159179"
        #endif

        RewardedInterstitialAd.load(with: adUnitID, request: Request()) { [weak self] ad, error in
            guard let self else { return }
            if let error {
                print("[AdMob] Récompensée échec chargement : \(error.localizedDescription)")
                self.isReady = false
                return
            }
            self.rewardedAd = ad
            self.rewardedAd?.fullScreenContentDelegate = self
            self.isReady = true
            print("[AdMob] Pub récompensée prête")
        }
    }

    // MARK: - Affichage

    /// Présente la pub. `onReward` est appelé si l'utilisateur gagne la récompense.
    /// `onUnavailable` est appelé si aucune pub n'est prête (et relance un chargement).
    func show(from viewController: UIViewController,
              onReward: @escaping () -> Void,
              onUnavailable: @escaping () -> Void) {
        guard let ad = rewardedAd, isReady else {
            onUnavailable()
            loadAd()
            return
        }
        isReady = false
        ad.present(from: viewController) {
            // L'utilisateur a regardé la pub jusqu'au bout : récompense accordée.
            onReward()
        }
    }
}

// MARK: - FullScreenContentDelegate

extension RewardedAdManager: FullScreenContentDelegate {

    func adDidDismissFullScreenContent(_ ad: FullScreenPresentingAd) {
        rewardedAd = nil
        loadAd()   // précharge la suivante
    }

    func ad(_ ad: FullScreenPresentingAd, didFailToPresentFullScreenContentWithError error: Error) {
        print("[AdMob] Récompensée échec affichage : \(error.localizedDescription)")
        rewardedAd = nil
        loadAd()
    }
}
