//
//  GameViewController.swift
//  tenGO
//

import UIKit
import SpriteKit
import GameplayKit
import GameKit
import GoogleMobileAds

class GameViewController: UIViewController {

    private var bannerView: BannerView?
    private var consentGathered = false

    /// Vue de jeu (sous-vue d'une racine UIView simple, pas une SKView imbriquée :
    /// évite les soucis de livraison tactile). Se réduit au-dessus de la bannière.
    private var gameView: SKView!
    /// Contrainte basse de `gameView` : bas de l'écran sans bannière, puis haut de
    /// la bannière une fois celle-ci posée — toutes les scènes restent au-dessus.
    private var gameViewBottom: NSLayoutConstraint!
    /// Ancrage de la vue de jeu au-dessus de la bannière (nil sans bannière).
    private var gameViewBottomToBanner: NSLayoutConstraint?

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

        #if DEBUG
        applyQAOverrides(env)
        #endif

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
        } else if env["SHOP_MODE"] == "1" {
            // Ouvre directement la boutique (capture de review App Store des achats in-app).
            let shop = BoutiqueScene(size: CGSize(width: 750, height: 1334))
            #if DEBUG
            if env["FORCE_SHOP_GUIDE"] == "1" { shop.guidedPurchase = true }
            if env["SHOP_RETURN"] == "game" { shop.returnDestination = .game }
            #endif
            scene = shop
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
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(openDuelFromLink),
            name: .tenGOOpenDuel,
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(adFreeDidChange),
            name: .tenGOAdFreeChanged,
            object: nil
        )

        // Source de vérité StoreKit du mod sans pub (le cache local a déjà été
        // lu ; ceci le corrige après réinstallation, changement d'appareil ou
        // remboursement).
        Task { await AdFreeManager.shared.refreshEntitlements() }
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        // Lien entrant reçu avant que l'observateur ne soit prêt (lancement à froid).
        if DeepLink.pendingDaily { openDaily() }
        let env = ProcessInfo.processInfo.environment
        guard env["SCREENSHOT_DAILY"] != "1", env["DEMO_MODE"] != "1", env["BRAND_MODE"] != "1", env["SHOP_MODE"] != "1" else { return }

        // QA (GAME_NORMAL) : démarre les pubs sans le flux de consentement, pour
        // tester le layout de la bannière au simulateur sans boîtes système bloquantes.
        if env["GAME_NORMAL"] == "1" {
            guard !consentGathered else { return }
            consentGathered = true
            // Aucun consentement recueilli dans ce mode → non personnalisé.
            MobileAds.shared.requestConfiguration.publisherPrivacyPersonalizationState = .disabled
            MobileAds.shared.start { _ in
                DispatchQueue.main.async {
                    self.setupBanner()
                    RewardedAdManager.shared.isAdsSessionStarted = true
                    RewardedAdManager.shared.preloadIfNeeded()
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
            ConsentManager.shared.gatherConsent { [weak self] outcome in
                print("[AdMob] Démarrage pubs — mode \(outcome == .personalizedAds ? "personnalisé" : "non personnalisé")")
                MobileAds.shared.start { _ in
                    DispatchQueue.main.async {
                        self?.setupBanner()
                        // Le menu est déjà affiché à ce stade : c'est ici que
                        // le premier préchargement de la récompensée se joue.
                        RewardedAdManager.shared.isAdsSessionStarted = true
                        RewardedAdManager.shared.preloadIfNeeded()
                    }
                }
            }
        }
    }

    // MARK: - Bannière AdMob

    private func setupBanner() {
        // Mod « sans pub » : aucune bannière, et surtout on laisse gameViewBottom
        // actif pour que la vue de jeu occupe toute la hauteur.
        guard !AdFreeManager.shared.isPurchased else { return }

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
        // La contrainte est retenue : il faut pouvoir la désactiver si l'utilisateur
        // achète le mod sans pub en cours de session (sinon conflit avec gameViewBottom).
        gameViewBottom.isActive = false
        let toBanner = gameView.bottomAnchor.constraint(equalTo: banner.topAnchor)
        toBanner.isActive = true
        gameViewBottomToBanner = toBanner
        view.layoutIfNeeded()
        (gameView.scene as? GameScene)?.relayoutForViewChange()

        banner.load(.consentAware())
        bannerView = banner
    }

    @objc private func showGameCenter(_ notification: Notification) {
        let id = notification.userInfo?["leaderboardID"] as? String ?? AppConfig.gameCenterLeaderboardID
        GameCenterManager.shared.showLeaderboard(id, from: self)
    }

    @objc private func openDailyFromLink() {
        openDaily()
    }

    /// Ouvre l'écran Duel avec le code reçu par lien, prêt à être accepté.
    @objc private func openDuelFromLink() {
        guard let skView = gameView else { return }
        let scene = DuelScene(size: CGSize(width: 750, height: 1334))
        scene.incomingCode = DeepLink.consumePendingDuelCode()
        scene.anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scene.scaleMode = .aspectFill
        skView.presentScene(scene, transition: SKTransition.fade(withDuration: 0.3))
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

    /// Mod « sans pub » acheté ou restauré : retire la bannière à chaud et rend
    /// sa hauteur à la vue de jeu, sans redémarrer l'app.
    @objc private func adFreeDidChange() {
        guard AdFreeManager.shared.isPurchased, let banner = bannerView else { return }
        bannerView = nil
        // Libérer l'ancrage à la bannière AVANT de réactiver la contrainte pleine
        // hauteur, sinon les deux coexistent et le layout part en conflit.
        gameViewBottomToBanner?.isActive = false
        gameViewBottomToBanner = nil
        banner.removeFromSuperview()
        gameViewBottom.isActive = true
        view.layoutIfNeeded()
        (gameView.scene as? GameScene)?.relayoutForViewChange()
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

    #if DEBUG
    /// Raccourcis de QA pilotables par variables d'environnement
    /// (`SIMCTL_CHILD_*` au lancement). Le simulateur ne pouvant pas être
    /// cliqué automatiquement, ils permettent d'atteindre directement chaque
    /// état de l'onboarding des boosters.
    ///
    ///   BOOSTER_GRANT=hint:2,hammer:1   inventaire de départ
    ///   BOOSTER_RESET=1                 vide l'inventaire
    ///   COINS=30                        solde de pièces
    ///   RESET_ONBOARDING=1              rejoue guide d'achat + coach-marks
    ///   FORCE_SHOP_GUIDE=1              force le tutoriel d'achat (avec SHOP_MODE)
    ///   SHOP_RETURN=game                retour boutique → partie
    private func applyQAOverrides(_ env: [String: String]) {
        let defaults = UserDefaults.standard

        if env["BOOSTER_RESET"] == "1" {
            for booster in Booster.allCases {
                defaults.set(0, forKey: AppConfig.UserDefaultsKey.boosterInventoryPrefix + booster.rawValue)
            }
        }
        if let grant = env["BOOSTER_GRANT"], !grant.isEmpty {
            for pair in grant.split(separator: ",") {
                let parts = pair.split(separator: ":")
                guard let booster = Booster(rawValue: String(parts[0])) else { continue }
                let qty = parts.count > 1 ? (Int(parts[1]) ?? 1) : 1
                defaults.set(qty, forKey: AppConfig.UserDefaultsKey.boosterInventoryPrefix + booster.rawValue)
            }
        }
        if let coins = env["COINS"], let value = Int(coins) {
            defaults.set(value, forKey: AppConfig.UserDefaultsKey.coinsBalance)
        }
        if env["RESET_ONBOARDING"] == "1" {
            defaults.set(false, forKey: AppConfig.UserDefaultsKey.hasSeenShopPurchaseGuide)
            defaults.set(0, forKey: AppConfig.UserDefaultsKey.shopGuideSnooze)
            defaults.set(true, forKey: AppConfig.UserDefaultsKey.hasSeenTutorial)
            for booster in Booster.allCases {
                defaults.removeObject(forKey: AppConfig.UserDefaultsKey.boosterCoachSeenPrefix + booster.rawValue)
            }
        }
        print("[BOOSTER] QA — coins=\(CoinManager.shared.balance) "
              + Booster.allCases.map { "\($0.rawValue)=\(BoosterManager.shared.count($0))" }.joined(separator: " "))
    }
    #endif
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
