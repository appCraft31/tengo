//
//  GameCenterManager.swift
//  tenGO
//

import GameKit
import UIKit

final class GameCenterManager: NSObject {

    static let shared = GameCenterManager()
    private override init() {}

    private(set) var isAuthenticated = false

    func authenticate(from viewController: UIViewController) {
        GKLocalPlayer.local.authenticateHandler = { [weak self] gcViewController, error in
            guard let self else { return }
            if let gcViewController {
                viewController.present(gcViewController, animated: true)
            } else if GKLocalPlayer.local.isAuthenticated {
                self.isAuthenticated = true
                print("[GameCenter] Authentifié : \(GKLocalPlayer.local.displayName)")
            } else {
                self.isAuthenticated = false
                if let error {
                    print("[GameCenter] Erreur d'authentification : \(error.localizedDescription)")
                }
            }
        }
    }

    func submitScore(_ score: Int) {
        guard GKLocalPlayer.local.isAuthenticated else { return }
        GKLeaderboard.submitScore(
            score,
            context: 0,
            player: GKLocalPlayer.local,
            leaderboardIDs: [AppConfig.gameCenterLeaderboardID]
        ) { error in
            if let error {
                print("[GameCenter] Erreur soumission score : \(error.localizedDescription)")
            } else {
                print("[GameCenter] Score \(score) soumis avec succès")
            }
        }
    }

    func submitDailyScore(_ score: Int) {
        guard GKLocalPlayer.local.isAuthenticated else { return }
        GKLeaderboard.submitScore(
            score,
            context: 0,
            player: GKLocalPlayer.local,
            leaderboardIDs: [AppConfig.gameCenterDailyLeaderboardID]
        ) { error in
            if let error {
                print("[GameCenter] Erreur soumission score défi : \(error.localizedDescription)")
            } else {
                print("[GameCenter] Score défi \(score) soumis avec succès")
            }
        }
    }

    func submitRushScore(_ score: Int) {
        guard GKLocalPlayer.local.isAuthenticated else { return }
        GKLeaderboard.submitScore(
            score,
            context: 0,
            player: GKLocalPlayer.local,
            leaderboardIDs: [AppConfig.gameCenterRushLeaderboardID]
        ) { error in
            if let error {
                print("[GameCenter] Erreur soumission score Rush : \(error.localizedDescription)")
            } else {
                print("[GameCenter] Score Rush \(score) soumis avec succès")
            }
        }
    }

    func showLeaderboard(_ leaderboardID: String = AppConfig.gameCenterLeaderboardID, from viewController: UIViewController) {
        guard GKLocalPlayer.local.isAuthenticated else { return }
        let gcVC = GKGameCenterViewController(
            leaderboardID: leaderboardID,
            playerScope: .global,
            timeScope: .allTime
        )
        gcVC.gameCenterDelegate = self
        viewController.present(gcVC, animated: true)
    }
}

extension GameCenterManager: GKGameCenterControllerDelegate {
    func gameCenterViewControllerDidFinish(_ gameCenterViewController: GKGameCenterViewController) {
        gameCenterViewController.dismiss(animated: true)
    }
}
