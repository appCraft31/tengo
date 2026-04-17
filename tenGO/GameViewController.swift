//
//  GameViewController.swift
//  tenGO
//
//  Created by Nicolas on 17/04/2026.
//

import UIKit
import SpriteKit
import GameplayKit
import GoogleMobileAds

class GameViewController: UIViewController {

    private var bannerView: BannerView?

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

        MobileAds.shared.start { [weak self] _ in
            DispatchQueue.main.async {
                self?.setupBanner()
            }
        }

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(sceneDidChange(_:)),
            name: .tenGOSceneChanged,
            object: nil
        )
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

    @objc private func sceneDidChange(_ notification: Notification) {
        let isMenu = notification.userInfo?["isMenu"] as? Bool ?? false
        UIView.animate(withDuration: 0.25) {
            self.bannerView?.alpha = isMenu ? 1 : 0
        }
    }

    // MARK: - Orientations

    override var supportedInterfaceOrientations: UIInterfaceOrientationMask {
        if UIDevice.current.userInterfaceIdiom == .phone {
            return .allButUpsideDown
        } else {
            return .all
        }
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
