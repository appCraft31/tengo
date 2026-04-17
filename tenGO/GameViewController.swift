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

        // Initialiser le SDK AdMob
        MobileAds.shared.start(completionHandler: nil)

        let scene = MenuScene(size: CGSize(width: 750, height: 1334))
        scene.anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scene.scaleMode = .aspectFill
        skView.presentScene(scene)
        skView.ignoresSiblingOrder = true
        skView.showsFPS = false
        skView.showsNodeCount = false

        setupBanner()

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
        banner.adUnitID = "ca-app-pub-4352408747876735/9831578000"
        banner.rootViewController = self
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

