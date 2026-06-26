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

    /// Vue de jeu (sous-vue d'une racine UIView simple, pas une SKView imbriquée :
    /// évite les soucis de livraison tactile). Se réduit au-dessus de la bannière.
    private var gameView: SKView!
    /// Contrainte basse de `gameView` : bas de l'écran sans bannière, puis haut de
    /// la bannière une fois celle-ci posée — toutes les scènes restent au-dessus.
    private var gameViewBottom: NSLayoutConstraint!

    /// Hiérarchie programmatique : racine UIView simple + SKView plein écran.
    override func loadView() {
        let root = UIView(frame: UIScreen.main.bounds)
        root.backgroundColor = .black
        // Frame initiale plein écran : garantit des bounds valides dès le premier
        // didMove de la scène (avant que les contraintes ne soient résolues), sinon
        // les éléments positionnés via la géométrie view se retrouvent hors écran.
        let skView = SKView(frame: UIScreen.main.bounds)
        skView.translatesAutoresizingMaskIntoConstraints = false
        root.addSubview(skView)
        gameViewBottom = skView.bottomAnchor.constraint(equalTo: root.bottomAnchor)
        NSLayoutConstraint.activate([
            skView.topAnchor.constraint(equalTo: root.topAnchor),
            skView.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            skView.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            gameViewBottom
        ])
        self.view = root
        self.gameView = skView
    }

    override func viewDidLoad() {
        super.viewDidLoad()

        let skView = gameView!

        #if DEBUG
        MobileAds.shared.requestConfiguration.testDeviceIdentifiers = ["052b09f5dbca790a5d0b42140dcfa503"]
        #endif

        let scene: SKScene
        let env = ProcessInfo.processInfo.environment
        if env["BRAND_MODE"] == "1" {
            // Capture vidéo marketing : écran de marque animé (transition).
            scene = BrandTransitionScene(size: CGSize(width: 750, height: 1334))
        } else if env["DEMO_MODE"] == "1" {
            // Capture vidéo marketing : grille déterministe jouée par l'auto-player.
            let seed = UInt64(env["DEMO_SEED"] ?? "") ?? 7
            let speed = Double(env["DEMO_SPEED"] ?? "") ?? 1.0
            scene = GameScene(size: CGSize(width: 750, height: 1334), demoSeed: seed, demoSpeed: speed)
        } else if env["SCREENSHOT_DAILY"] == "1" {
            scene = GameScene(size: CGSize(width: 750, height: 1334), daily: DailyChallenge.make())
        } else if env["GAME_NORMAL"] == "1" {
            // Démarre directement une partie normale (test/QA + captures gameplay).
            scene = GameScene(size: CGSize(width: 750, height: 1334), savedState: nil)
        } else {
            scene = MenuScene(size: CGSize(width: 750, height: 1334))
        }
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
            selector: #selector(showGameCenter(_:)),
            name: .tenGOShowGameCenter,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(openDailyFromLink),
            name: .tenGOOpenDaily,
            object: nil
        )
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        // Lien entrant reçu avant que l'observateur ne soit prêt (lancement à froid).
        if DeepLink.pendingDaily { openDaily() }
        let env = ProcessInfo.processInfo.environment
        guard env["SCREENSHOT_DAILY"] != "1", env["DEMO_MODE"] != "1", env["BRAND_MODE"] != "1" else { return }

        // QA (GAME_NORMAL) : démarre les pubs sans le flux de consentement, pour
        // tester le layout de la bannière au simulateur sans boîtes système bloquantes.
        if env["GAME_NORMAL"] == "1" {
            guard !consentGathered else { return }
            consentGathered = true
            MobileAds.shared.start { _ in
                DispatchQueue.main.async {
                    self.setupBanner()
                    InterstitialAdManager.shared.loadAd()
                    RewardedAdManager.shared.loadAd()
                }
            }
            return
        }

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
                    DispatchQueue.main.async {
                        self?.setupBanner()
                        InterstitialAdManager.shared.loadAd()
                        RewardedAdManager.shared.loadAd()
                    }
                }
            }
        }
    }

    // MARK: - Bannière AdMob

    private func setupBanner() {
        // Bannière adaptative ancrée, calée sur toute la largeur de l'écran.
        let width = view.frame.inset(by: view.safeAreaInsets).width
        let adSize = currentOrientationAnchoredAdaptiveBanner(width: width)
        let banner = BannerView(adSize: adSize)
        #if DEBUG
        banner.adUnitID = "ca-app-pub-3940256099942544/2934735716"
        #else
        banner.adUnitID = "ca-app-pub-4352408747876735/9831578000"
        #endif
        banner.rootViewController = self
        banner.delegate = self
        let bannerUnitID = banner.adUnitID ?? "banner"
        banner.paidEventHandler = { adValue in
            AnalyticsService.adImpression(adValue: adValue, format: "banner", unitName: bannerUnitID)
        }
        banner.translatesAutoresizingMaskIntoConstraints = false
        banner.backgroundColor = .systemBackground
        view.addSubview(banner)

        NSLayoutConstraint.activate([
            banner.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            banner.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            banner.bottomAnchor.constraint(equalTo: view.safeAreaLayoutGuide.bottomAnchor)
        ])

        // La vue de jeu se borne au-dessus de la bannière : toutes les scènes
        // (menu, tutoriel, jeu, boutique) restent intégralement au-dessus de la pub.
        gameViewBottom.isActive = false
        gameView.bottomAnchor.constraint(equalTo: banner.topAnchor).isActive = true
        view.layoutIfNeeded()
        (gameView.scene as? GameScene)?.relayoutForViewChange()

        banner.load(Request())
        bannerView = banner
    }

    @objc private func showGameCenter(_ notification: Notification) {
        let id = notification.userInfo?["leaderboardID"] as? String ?? AppConfig.gameCenterLeaderboardID
        GameCenterManager.shared.showLeaderboard(id, from: self)
    }

    @objc private func openDailyFromLink() {
        openDaily()
    }

    /// Ouvre directement le Défi du jour (depuis un lien externe / événement intégré).
    private func openDaily() {
        _ = DeepLink.consumePendingDaily()
        let skView = gameView!
        let scene = GameScene(size: CGSize(width: 750, height: 1334), daily: DailyChallenge.make())
        scene.anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scene.scaleMode = .aspectFill
        skView.presentScene(scene, transition: SKTransition.fade(withDuration: 0.3))
    }

    @objc private func sceneDidChange(_ notification: Notification) {
        // La bannière reste visible en permanence : la vue de jeu est bornée
        // au-dessus d'elle, donc aucun élément de jeu n'est masqué.
        bannerView?.alpha = 1
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
