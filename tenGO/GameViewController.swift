//
//  GameViewController.swift
//  tenGO
//

import UIKit
import SpriteKit
import GameplayKit
import GameKit
import GoogleMobileAds
import UserMessagingPlatform

class GameViewController: UIViewController {

    private var bannerView: BannerView?
    private var consentGathered = false

    override func viewDidLoad() {
        super.viewDidLoad()

        guard let skView = self.view as? SKView else { return }

        #if DEBUG
        MobileAds.shared.requestConfiguration.testDeviceIdentifiers = ["052b09f5dbca790a5d0b42140dcfa503"]
        #endif

        let scene = MenuScene(size: CGSize(width: 750, height: 1334))
        scene.anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scene.scaleMode = .aspectFill
        skView.presentScene(scene)
        skView.ignoresSiblingOrder = true
        skView.showsFPS = false
        skView.showsNodeCount = false

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(sceneDidChange(_:)),
            name: .tenGOSceneChanged,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(showGameCenter),
            name: .tenGOShowGameCenter,
            object: nil
        )
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        guard !consentGathered else { return }
        consentGathered = true

        GameCenterManager.shared.authenticate(from: self)

        // Délai court pour laisser le windowScene se stabiliser (critique sur iPad) :
        // requestTrackingAuthorization est ignoré si la fenêtre n'est pas encore active.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
            guard let self else { return }
            ConsentManager.shared.gatherConsent(from: self) { [weak self] in
                guard ConsentInformation.shared.canRequestAds else {
                    print("[AdMob] Consentement refusé — pas de bannière")
                    return
                }
                MobileAds.shared.start { _ in
                    DispatchQueue.main.async { self?.setupBanner() }
                }
            }
        }
    }

    // MARK: - Bannière AdMob

    private func setupBanner() {
        let banner = BannerView(adSize: AdSizeBanner)
        #if DEBUG
        banner.adUnitID = "ca-app-pub-3940256099942544/2934735716"
        #else
        banner.adUnitID = "ca-app-pub-4352408747876735/9831578000"
        #endif
        banner.rootViewController = self
        banner.delegate = self
        banner.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(banner)

        NSLayoutConstraint.activate([
            banner.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            banner.bottomAnchor.constraint(equalTo: view.safeAreaLayoutGuide.bottomAnchor)
        ])

        banner.load(Request())
        bannerView = banner
    }

    @objc private func showGameCenter() {
        GameCenterManager.shared.showLeaderboard(from: self)
    }

    @objc private func sceneDidChange(_ notification: Notification) {
        let isMenu = notification.userInfo?["isMenu"] as? Bool ?? false
        UIView.animate(withDuration: 0.25) {
            self.bannerView?.alpha = isMenu ? 1 : 0
        }
    }

    // MARK: - Orientations

    override var supportedInterfaceOrientations: UIInterfaceOrientationMask {
        return .portrait
    }

    override var prefersStatusBarHidden: Bool {
        return true
    }
}

// MARK: - BannerViewDelegate

extension GameViewController: BannerViewDelegate {
    func bannerViewDidReceiveAd(_ bannerView: BannerView) {
        print("[AdMob] Bannière chargée avec succès")
    }

    func bannerView(_ bannerView: BannerView, didFailToReceiveAdWithError error: Error) {
        print("[AdMob] Échec chargement bannière : \(error.localizedDescription)")
    }
}
