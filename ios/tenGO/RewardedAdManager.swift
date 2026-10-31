//
//  RewardedAdManager.swift
//  tenGO
//
//  Interstitiel AVEC récompense (rewarded interstitial) : l'utilisateur regarde
//  une pub volontairement. Trois emplacements (cf. `RewardedPlacement`) :
//  pièces depuis le menu, indice gratuit quand le joueur sèche, seconde chance
//  quand la grille est bloquée. Chargé après MobileAds.start, rechargé après
//  chaque diffusion.
//
//  Le plafond journalier ne concerne que les pièces du menu (farm de monnaie) :
//  les emplacements en partie sont bornés par partie dans GameScene.
//

import GoogleMobileAds
import UIKit

/// Où la vidéo récompensée a été proposée. La valeur brute part dans
/// Analytics (paramètre `placement`) pour comparer les emplacements.
enum RewardedPlacement: String {
    case menuCoins = "menu_coins"        // +10 pièces depuis le menu
    case hint = "hint"                   // indice gratuit en partie
    case secondChance = "second_chance"  // grille bloquée : mélange et on continue
}

final class RewardedAdManager: NSObject {

    static let shared = RewardedAdManager()
    private override init() {}

    private var rewardedAd: RewardedInterstitialAd?
    private(set) var isReady = false
    private var isLoading = false

    /// Posé par GameViewController une fois MobileAds.start terminé (après le
    /// flux de consentement). Tant que c'est faux, preloadIfNeeded ne fait
    /// rien : le menu apparaît sous l'alerte ATT, il ne doit pas déclencher
    /// de requête avant que le mode publicitaire soit connu.
    var isAdsSessionStarted = false

    private let defaults = UserDefaults.standard

    // MARK: - Plafond journalier (pièces du menu)

    /// Nombre maximum de pubs « pièces » par jour (reset à minuit local).
    static let dailyLimit = 5

    private var todayKey: Int {
        let c = Calendar.current.dateComponents([.year, .month, .day], from: Date())
        return (c.year ?? 0) * 10000 + (c.month ?? 0) * 100 + (c.day ?? 0)
    }

    /// Nombre de pubs « pièces » déjà regardées aujourd'hui (0 si on a changé de jour).
    private var countToday: Int {
        guard defaults.integer(forKey: AppConfig.UserDefaultsKey.rewardedDayKey) == todayKey else { return 0 }
        return defaults.integer(forKey: AppConfig.UserDefaultsKey.rewardedDayCount)
    }

    /// Pubs « pièces » encore disponibles aujourd'hui.
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

    /// Précharge une pub si aucune n'est déjà prête ou en cours de chargement.
    /// Les emplacements en partie (indice, seconde chance) n'ont pas de quota
    /// journalier : une annonce a toujours une chance d'être regardée, le
    /// préchargement ne dépend donc plus du quota des pièces du menu.
    func preloadIfNeeded() {
        guard isAdsSessionStarted, !isReady, !isLoading else { return }
        loadAd()
    }

    /// L'emplacement peut être proposé maintenant : une pub est prête et,
    /// pour les pièces du menu, le quota du jour n'est pas épuisé.
    func canOffer(_ placement: RewardedPlacement) -> Bool {
        #if DEBUG
        if fakesAds { return placement != .menuCoins || canWatchToday }
        #endif
        guard isReady, rewardedAd != nil else { return false }
        return placement != .menuCoins || canWatchToday
    }

    func loadAd() {
        let adUnitID: String
        #if DEBUG
        adUnitID = "ca-app-pub-3940256099942544/6978759866"   // ID de test Google (rewarded interstitial)
        #else
        adUnitID = "ca-app-pub-4352408747876735/2440159179"
        #endif

        isLoading = true
        RewardedInterstitialAd.load(with: adUnitID, request: .consentAware()) { [weak self] ad, error in
            guard let self else { return }
            self.isLoading = false
            if let error {
                print("[AdMob] Récompensée échec chargement : \(error.localizedDescription)")
                self.isReady = false
                return
            }
            self.rewardedAd = ad
            self.rewardedAd?.fullScreenContentDelegate = self
            // Revenu de chaque affichage, pour suivre le revenu pour 1 000
            // affichages par format (cf. ios/docs/ads-revenue-tracking.md).
            self.rewardedAd?.paidEventHandler = { adValue in
                AnalyticsService.adImpression(adValue: adValue, format: "rewarded_interstitial",
                                              unitName: adUnitID)
            }
            self.isReady = true
            print("[AdMob] Pub récompensée prête")
        }
    }

    // MARK: - Affichage

    #if DEBUG
    /// QA_FAKE_REWARDED=1 : pub récompensée simulée (toujours prête, récompense
    /// accordée après 0,6 s), pour tester les emplacements sans réseau.
    private let fakesAds = ProcessInfo.processInfo.environment["QA_FAKE_REWARDED"] == "1"
    #endif

    /// Récompense gagnée pendant la diffusion en cours, versée à la fermeture.
    private var pendingReward: (() -> Void)?
    /// Rappel de fermeture de la diffusion en cours (récompense gagnée ou non).
    private var pendingDismiss: ((_ rewarded: Bool) -> Void)?
    private var rewardEarned = false
    private var currentPlacement: RewardedPlacement = .menuCoins

    /// Présente la pub si une pub est prête (et, pour les pièces du menu, si
    /// le quota du jour n'est pas épuisé).
    /// - onReward : l'utilisateur a gagné la récompense. Appelé à la FERMETURE
    ///   de la pub, pour que l'effet (indice, mélange) se joue sous les yeux
    ///   du joueur et non derrière la pub.
    /// - onDismiss : appelé après la fermeture, récompense gagnée ou non
    ///   (le joueur a pu couper la pub avant la fin), ou si elle n'a pas pu
    ///   s'afficher.
    /// - onUnavailable : aucune pub prête (relance un chargement).
    /// - onLimitReached : quota journalier atteint (pièces du menu seulement).
    func show(placement: RewardedPlacement = .menuCoins,
              from viewController: UIViewController,
              onReward: @escaping () -> Void,
              onDismiss: ((_ rewarded: Bool) -> Void)? = nil,
              onUnavailable: @escaping () -> Void,
              onLimitReached: @escaping () -> Void = {}) {
        if placement == .menuCoins, !canWatchToday {
            onLimitReached()
            return
        }
        currentPlacement = placement
        pendingReward = onReward
        pendingDismiss = onDismiss
        rewardEarned = false

        #if DEBUG
        if fakesAds {
            AnalyticsService.rewardedStarted(placement: placement.rawValue)
            print("[AdMob] Récompensée simulée (QA) : \(placement.rawValue)")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { [weak self] in
                guard let self else { return }
                self.rewardEarned = true
                if placement == .menuCoins { self.recordView() }
                self.finishPresentation()
            }
            return
        }
        #endif

        guard let ad = rewardedAd, isReady else {
            pendingReward = nil
            pendingDismiss = nil
            onUnavailable()
            loadAd()
            return
        }
        isReady = false
        AnalyticsService.rewardedStarted(placement: placement.rawValue)
        ad.present(from: viewController) { [weak self] in
            // L'utilisateur a regardé la pub jusqu'au bout : récompense acquise,
            // versée à la fermeture (adDidDismissFullScreenContent).
            guard let self else { return }
            self.rewardEarned = true
            if placement == .menuCoins { self.recordView() }
        }
    }

    /// Verse (ou non) la récompense et prévient l'appelant. Appelé une seule
    /// fois par diffusion, à la fermeture ou sur échec d'affichage.
    private func finishPresentation() {
        let reward = pendingReward
        let dismiss = pendingDismiss
        let earned = rewardEarned
        pendingReward = nil
        pendingDismiss = nil
        rewardEarned = false
        if earned {
            AnalyticsService.rewardedCompleted(placement: currentPlacement.rawValue)
            // Une vidéo vient d'être regardée : pas de plein écran juste derrière.
            InterstitialAdManager.shared.noteFullScreenAdShown()
            reward?()
        }
        dismiss?(earned)
    }
}

// MARK: - FullScreenContentDelegate

extension RewardedAdManager: FullScreenContentDelegate {

    func adDidDismissFullScreenContent(_ ad: FullScreenPresentingAd) {
        rewardedAd = nil
        finishPresentation()
        preloadIfNeeded()   // précharge la suivante
    }

    func ad(_ ad: FullScreenPresentingAd, didFailToPresentFullScreenContentWithError error: Error) {
        print("[AdMob] Récompensée échec affichage : \(error.localizedDescription)")
        rewardedAd = nil
        finishPresentation()
        preloadIfNeeded()
    }
}
