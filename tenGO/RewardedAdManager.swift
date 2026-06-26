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

    private let defaults = UserDefaults.standard

    // MARK: - Plafond journalier

    /// Nombre maximum de pubs récompensées par jour (reset à minuit local).
    static let dailyLimit = 5

    private var todayKey: Int {
        let c = Calendar.current.dateComponents([.year, .month, .day], from: Date())
        return (c.year ?? 0) * 10000 + (c.month ?? 0) * 100 + (c.day ?? 0)
    }

    /// Nombre de pubs déjà regardées aujourd'hui (0 si on a changé de jour).
    private var countToday: Int {
        guard defaults.integer(forKey: AppConfig.UserDefaultsKey.rewardedDayKey) == todayKey else { return 0 }
        return defaults.integer(forKey: AppConfig.UserDefaultsKey.rewardedDayCount)
    }

    /// Pubs récompensées encore disponibles aujourd'hui.
    var remainingToday: Int { max(0, RewardedAdManager.dailyLimit - countToday) }

    /// Le quota du jour n'est pas épuisé.
    var canWatchToday: Bool { remainingToday > 0 }

    /// Incrémente le compteur du jour (à l'obtention de la récompense).
    private func recordView() {
        let current = countToday
        defaults.set(todayKey, forKey: AppConfig.UserDefaultsKey.rewardedDayKey)
        defaults.set(current + 1, forKey: AppConfig.UserDefaultsKey.rewardedDayCount)
    }

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

    /// Présente la pub si le quota du jour n'est pas épuisé et qu'une pub est prête.
    /// - onReward : l'utilisateur a gagné la récompense (le compteur du jour est incrémenté).
    /// - onUnavailable : aucune pub prête (relance un chargement).
    /// - onLimitReached : quota journalier atteint.
    func show(from viewController: UIViewController,
              onReward: @escaping () -> Void,
              onUnavailable: @escaping () -> Void,
              onLimitReached: @escaping () -> Void) {
        guard canWatchToday else {
            onLimitReached()
            return
        }
        guard let ad = rewardedAd, isReady else {
            onUnavailable()
            loadAd()
            return
        }
        isReady = false
        ad.present(from: viewController) { [weak self] in
            // L'utilisateur a regardé la pub jusqu'au bout : récompense accordée.
            self?.recordView()
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
