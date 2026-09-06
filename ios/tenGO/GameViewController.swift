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

    private var consentGathered = false

    /// Vue de jeu (sous-vue d'une racine UIView simple, pas une SKView imbriquée :
    /// évite les soucis de livraison tactile). Occupe tout l'écran.
    private var gameView: SKView!

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
        NSLayoutConstraint.activate([
            skView.topAnchor.constraint(equalTo: root.topAnchor),
            skView.leadingAnchor.constraint(equalTo: root.leadingAnchor),
            skView.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            skView.bottomAnchor.constraint(equalTo: root.bottomAnchor)
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

        // Doit précéder la construction de la scène : le Profil et le Duel
        // lisent ces valeurs à l'affichage.
        if env["SCREENSHOT_SEED_DATA"] == "1" { Self.seedShowcaseData() }

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
        } else if let name = env["SCREENSHOT_SCENE"] {
            // Captures des écrans qui ne sont pas des parties (fiche stores).
            // Même esprit que SHOP_MODE ci-dessus : hors #if DEBUG, pour rester
            // utilisable sur un build Release.
            let size = CGSize(width: 750, height: 1334)
            switch name {
            case "profile": scene = ProfileScene(size: size)
            case "duel":    scene = DuelScene(size: size)
            case "puzzles": scene = PuzzleLevelsScene(size: size, world: 1)
            default:        scene = MenuScene(size: size)
            }
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

        // Source de vérité StoreKit du mod sans pub (le cache local a déjà été
        // lu ; ceci le corrige après réinstallation, changement d'appareil ou
        // remboursement).
        Task { await AdFreeManager.shared.refreshEntitlements() }
    }

    /// Données de vitrine pour les captures de la fiche stores.
    ///
    /// Sur un simulateur vierge, le Profil affiche « Meilleur score 0 » et le
    /// Duel une liste vide : invendable sur une fiche. Ces valeurs ne sont
    /// écrites que sous `SCREENSHOT_SEED_DATA`, jamais par défaut, et restent
    /// plausibles — une fiche doit montrer le jeu tel qu'on peut le vivre.
    private static func seedShowcaseData() {
        let d = UserDefaults.standard
        let k = AppConfig.UserDefaultsKey.self
        d.set(4820, forKey: k.totalXP)                 // niveau ~20
        d.set(340, forKey: k.coinsBalance)
        d.set(12, forKey: k.streakCurrent)
        d.set(18, forKey: k.streakBest)
        d.set(1, forKey: k.streakShieldCount)
        d.set(9, forKey: k.statsBestChain)
        d.set(7, forKey: k.statsPerfectTotal)
        d.set(1463, forKey: k.statsTotalChains)
        d.set(24, forKey: k.statsTotalRushGames)
        d.set(31, forKey: k.statsTotalDaily)
        d.set([4820, 4310, 3980, 3720, 3450], forKey: "tengo_high_scores")
        d.set([2140, 1980, 1760], forKey: "tengo_rush_high_scores")
        d.set(true, forKey: k.hasSeenTutorial)
        d.set(true, forKey: k.noAdsPurchased)          // boutique sans prix
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        // Lien entrant reçu avant que l'observateur ne soit prêt (lancement à froid).
        if DeepLink.pendingDaily { openDaily() }
        let env = ProcessInfo.processInfo.environment
        // Modes de capture : ni pubs, ni feuille Game Center — elle se
        // superpose à l'écran et ruine la capture.
        guard env["SCREENSHOT_DAILY"] != "1", env["DEMO_MODE"] != "1", env["BRAND_MODE"] != "1",
              env["SHOP_MODE"] != "1", env["SCREENSHOT_SCENE"] == nil else { return }

        // QA (GAME_NORMAL) : démarre les pubs sans le flux de consentement, pour
        // tester au simulateur sans boîtes système bloquantes.
        if env["GAME_NORMAL"] == "1" {
            guard !consentGathered else { return }
            consentGathered = true
            // Aucun consentement recueilli dans ce mode → non personnalisé.
            MobileAds.shared.requestConfiguration.publisherPrivacyPersonalizationState = .disabled
            MobileAds.shared.start { _ in
                DispatchQueue.main.async {
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
            guard self != nil else { return }
            ConsentManager.shared.gatherConsent { outcome in
                print("[AdMob] Démarrage pubs — mode \(outcome == .personalizedAds ? "personnalisé" : "non personnalisé")")
                MobileAds.shared.start { _ in
                    DispatchQueue.main.async {
                        // Le menu est déjà affiché à ce stade : c'est ici que
                        // le premier préchargement de la récompensée se joue.
                        RewardedAdManager.shared.isAdsSessionStarted = true
                        RewardedAdManager.shared.preloadIfNeeded()
                    }
                }
            }
        }
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
        skView.presentScene(scene, transition: SceneTransition.fade(0.3))
    }

    /// Ouvre directement le Défi du jour (depuis un lien externe / événement intégré).
    private func openDaily() {
        _ = DeepLink.consumePendingDaily()
        let skView = gameView!
        let scene = GameScene(size: CGSize(width: 750, height: 1334), daily: DailyChallenge.make())
        scene.anchorPoint = CGPoint(x: 0.5, y: 0.5)
        scene.scaleMode = .aspectFill
        skView.presentScene(scene, transition: SceneTransition.fade(0.3))
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
