//
//  InterstitialAdManager.swift
//  tenGO
//

import GoogleMobileAds
import UIKit

enum AdTrigger {
    case home          // bouton ⌂ — toujours (si conditions OK)
    case replay        // bouton Rejouer du panel — 1 fois sur 2
    case gameOverAuto  // 1.5s après l'apparition du panel — 1 sur 3, dès la 2e partie
}

final class InterstitialAdManager: NSObject {
    static let shared = InterstitialAdManager()
    private override init() {}

    private var interstitial: InterstitialAd?
    private var isReady = false
    private var isLoading = false

    // MARK: - Garde-fous

    private static let cooldownSeconds: TimeInterval = 60
    private static let lastShownKey = "AdMob.interstitialLastShownAt"

    private var gamesCompletedThisSession = 0
    private var replayCount = 0
    private var gameOverCount = 0

    private var lastShownAt: Date {
        get { UserDefaults.standard.object(forKey: Self.lastShownKey) as? Date ?? .distantPast }
        set { UserDefaults.standard.set(newValue, forKey: Self.lastShownKey) }
    }

    private var pendingCompletion: (() -> Void)?

    // MARK: - Chargement

    func loadAd() {
        let adUnitID: String
        #if DEBUG
        adUnitID = "ca-app-pub-3940256099942544/4411468910"
        #else
        adUnitID = "ca-app-pub-4352408747876735/5201754193"
        #endif

        isLoading = true
        InterstitialAd.load(with: adUnitID, request: .consentAware()) { [weak self] ad, error in
            guard let self else { return }
            self.isLoading = false
            if let error {
                print("[AdMob] Interstitiel échec chargement : \(error.localizedDescription)")
                self.isReady = false
                return
            }
            self.interstitial = ad
            self.interstitial?.fullScreenContentDelegate = self
            self.interstitial?.paidEventHandler = { adValue in
                AnalyticsService.adImpression(adValue: adValue, format: "interstitial", unitName: adUnitID)
            }
            self.isReady = true
            print("[AdMob] Interstitiel prêt")
        }
    }

    // MARK: - Suivi de session

    /// À appeler à la fin de chaque partie (win/lose).
    func markGameCompleted() {
        gamesCompletedThisSession += 1
        // Préchargement paresseux : la première interstitielle n'est demandée
        // qu'une fois qu'une partie a été jouée (condition minimale de
        // shouldShow), pour ne pas consommer d'annonce servie jamais affichée.
        if !isReady && !isLoading {
            loadAd()
        }
    }

    // MARK: - Affichage conditionnel

    /// Affiche l'interstitielle si toutes les conditions du trigger sont remplies,
    /// sinon appelle `completion` immédiatement (fallback silencieux).
    func maybeShow(trigger: AdTrigger,
                   from viewController: UIViewController,
                   completion: @escaping () -> Void) {
        guard shouldShow(for: trigger) else {
            completion()
            return
        }
        present(from: viewController, completion: completion)
    }

    private func shouldShow(for trigger: AdTrigger) -> Bool {
        guard isReady, interstitial != nil else { return false }
        guard Date().timeIntervalSince(lastShownAt) >= Self.cooldownSeconds else { return false }

        // Grâce 1re partie ; gameOverAuto épargne aussi le 1er game over
        let minimumGames = (trigger == .gameOverAuto) ? 2 : 1
        guard gamesCompletedThisSession >= minimumGames else { return false }

        switch trigger {
        case .home:
            return true
        case .replay:
            replayCount += 1
            return replayCount % 2 == 0
        case .gameOverAuto:
            gameOverCount += 1
            return gameOverCount % 3 == 0
        }
    }

    private func present(from viewController: UIViewController,
                         completion: @escaping () -> Void) {
        guard let ad = interstitial else {
            completion()
            return
        }
        isReady = false
        lastShownAt = Date()
        pendingCompletion = completion
        ad.present(from: viewController)
    }
}

// MARK: - FullScreenContentDelegate

extension InterstitialAdManager: FullScreenContentDelegate {

    func adDidDismissFullScreenContent(_ ad: FullScreenPresentingAd) {
        pendingCompletion?()
        pendingCompletion = nil
        loadAd()
    }

    func ad(_ ad: FullScreenPresentingAd, didFailToPresentFullScreenContentWithError error: Error) {
        print("[AdMob] Interstitiel échec affichage : \(error.localizedDescription)")
        pendingCompletion?()
        pendingCompletion = nil
        loadAd()
    }
}
