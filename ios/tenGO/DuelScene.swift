//
//  DuelScene.swift
//  tenGO
//
//  Point d'entrée du Duel : lancer un défi, répondre à un code reçu, et
//  retrouver ses duels passés avec leur issue.
//

import SpriteKit
import UIKit

class DuelScene: SKScene {

    /// Code pré-rempli quand on arrive par un lien tengo://duel/XXXXXX.
    var incomingCode: String?

    private var cardWidth: CGFloat = 340
    private weak var presenter: UIViewController?
    private var duels: [Duel] = []
    private var statusLabel: SKLabelNode?

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
        presenter = view.window?.rootViewController
        setupUI()

        if let code = incomingCode {
            incomingCode = nil
            acceptDuel(code: code)
        } else {
            reloadDuels()
        }
    }

    // MARK: - UI

    private func setupUI() {
        guard let view = view else { return }
        let topY = size.height / 2
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        let bottomY = -visibleHalfH + view.safeAreaInsets.bottom / scale
        cardWidth = min(usableWidth - 48, 600)

        let title = SKLabelNode(text: String(localized: "duel.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        let subtitle = SKLabelNode(text: String(localized: "duel.subtitle"))
        subtitle.fontName = "AvenirNext-Medium"
        subtitle.fontSize = 15
        subtitle.fontColor = UIColor(white: 0.45, alpha: 1)
        subtitle.verticalAlignmentMode = .center
        subtitle.numberOfLines = 2
        subtitle.preferredMaxLayoutWidth = cardWidth
        subtitle.position = CGPoint(x: 0, y: topY - 146)
        addChild(subtitle)

        addButton(text: String(localized: "duel.start"), name: "startDuel",
                  color: ThemeManager.shared.active.color(forValue: 4), atY: topY - 216)
        addButton(text: String(localized: "duel.answer"), name: "answerDuel",
                  color: ThemeManager.shared.active.color(forValue: 5), atY: topY - 292)

        let status = SKLabelNode(text: "")
        status.fontName = "AvenirNext-Medium"
        status.fontSize = 15
        status.fontColor = UIColor(white: 0.40, alpha: 1)
        status.verticalAlignmentMode = .center
        status.numberOfLines = 2
        status.preferredMaxLayoutWidth = cardWidth
        status.position = CGPoint(x: 0, y: topY - 344)
        addChild(status)
        statusLabel = status

        // Cet écran EST l'onglet Social : pas de retour, des onglets.
        let tabBar = TabBar.make(width: usableWidth, selected: .social)
        tabBar.position = CGPoint(x: 0, y: bottomY + TabBar.height / 2)
        addChild(tabBar)
    }

    private func addButton(text: String, name: String, color: UIColor, atY y: CGFloat) {
        let node = SKNode()
        node.name = name
        node.position = CGPoint(x: 0, y: y)
        addChild(node)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: 62), cornerRadius: 31)
        bg.fillColor = color
        bg.strokeColor = UIColor(white: 0.68, alpha: 0.3)
        bg.lineWidth = 1
        node.addChild(bg)

        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 19
        label.fontColor = UIColor(white: 0.2, alpha: 1)
        label.verticalAlignmentMode = .center
        node.addChild(label)
    }

    /// Liste des duels passés, rechargée depuis le serveur (l'historique des
    /// codes, lui, est local — cf. DuelHistory).
    private func reloadDuels() {
        guard !DuelHistory.codes.isEmpty else { return }
        statusLabel?.text = String(localized: "duel.loading")
        Task { @MainActor in
            duels = await DuelService.shared.myDuels()
            statusLabel?.text = ""
            renderDuelList()
        }
    }

    private func renderDuelList() {
        children.filter { $0.name == "duelRow" }.forEach { $0.removeFromParent() }
        guard let view = view, let uid = FirebaseService.shared.uid else { return }
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let bottomY = -view.bounds.height / scale / 2 + view.safeAreaInsets.bottom / scale
        let topY = size.height / 2

        var cursorY = topY - 400
        let rowH: CGFloat = 56
        for duel in duels.prefix(5) where cursorY - rowH > bottomY + 130 {
            addDuelRow(duel, uid: uid, atY: cursorY, height: rowH)
            cursorY -= (rowH + 10)
        }
    }

    private func addDuelRow(_ duel: Duel, uid: String, atY y: CGFloat, height: CGFloat) {
        let row = SKNode()
        row.name = "duelRow"
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 18)
        bg.fillColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 0.9)
        bg.strokeColor = UIColor(white: 0.72, alpha: 0.25)
        bg.lineWidth = 1
        row.addChild(bg)

        let left = SKLabelNode(text: duel.code)
        left.fontName = "AvenirNext-Bold"
        left.fontSize = 17
        left.fontColor = UIColor(white: 0.30, alpha: 1)
        left.horizontalAlignmentMode = .left
        left.verticalAlignmentMode = .center
        left.position = CGPoint(x: -cardWidth / 2 + 20, y: 0)
        row.addChild(left)

        let text: String
        var color = UIColor(white: 0.42, alpha: 1)
        if duel.isComplete, let outcome = duel.outcome(for: uid) {
            switch outcome {
            case .win:
                text = String(localized: "duel.row_win")
                color = UIColor(red: 0.30, green: 0.60, blue: 0.36, alpha: 1)
            case .loss:
                text = String(localized: "duel.row_loss")
                color = UIColor(red: 0.72, green: 0.38, blue: 0.34, alpha: 1)
            case .draw:
                text = String(localized: "duel.row_draw")
            }
        } else if duel.isExpired {
            text = String(localized: "duel.row_expired")
        } else {
            text = String(localized: "duel.row_pending")
        }

        let right = SKLabelNode(text: text)
        right.fontName = "AvenirNext-DemiBold"
        right.fontSize = 15
        right.fontColor = color
        right.horizontalAlignmentMode = .right
        right.verticalAlignmentMode = .center
        right.position = CGPoint(x: cardWidth / 2 - 20, y: 0)
        row.addChild(right)
    }

    // MARK: - Actions

    /// Le challenger joue d'abord : le duel n'est créé qu'avec un vrai score,
    /// ce qui évite de laisser des duels vides derrière soi.
    private func startDuel() {
        let seed = UInt64.random(in: 1...UInt64.max)
        let scene = GameScene(size: size, duelSeed: seed, duelCode: nil)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.3))
    }

    private func promptForCode() {
        guard let presenter else { return }
        let alert = UIAlertController(title: String(localized: "duel.answer"),
                                      message: String(localized: "duel.enter_code"),
                                      preferredStyle: .alert)
        alert.addTextField { field in
            field.placeholder = "ABC123"
            field.autocapitalizationType = .allCharacters
            field.autocorrectionType = .no
        }
        alert.addAction(UIAlertAction(title: String(localized: "common.cancel"), style: .cancel))
        alert.addAction(UIAlertAction(title: String(localized: "duel.go"), style: .default) { [weak self, weak alert] _ in
            guard let code = alert?.textFields?.first?.text, !code.isEmpty else { return }
            self?.acceptDuel(code: code)
        })
        presenter.present(alert, animated: true)
    }

    /// Récupère le duel et lance la partie sur la MÊME grille. Les refus
    /// (introuvable, expiré, déjà joué, son propre duel) sont dits clairement
    /// plutôt que de mener à une partie qui ne comptera pas.
    private func acceptDuel(code: String) {
        statusLabel?.text = String(localized: "duel.loading")
        Task { @MainActor in
            do {
                let duel = try await DuelService.shared.fetch(code: code)
                guard !duel.isExpired else { throw DuelService.DuelError.expired }
                guard duel.opponentScore == nil else { throw DuelService.DuelError.alreadyPlayed }
                guard duel.challengerUid != FirebaseService.shared.uid else { throw DuelService.DuelError.ownDuel }

                statusLabel?.text = String(format: String(localized: "duel.score_to_beat"),
                                           duel.challengerName, duel.challengerScore)
                let scene = GameScene(size: size, duelSeed: duel.seed, duelCode: duel.code)
                scene.scaleMode = .aspectFill
                view?.presentScene(scene, transition: SKTransition.fade(withDuration: 0.35))
            } catch {
                statusLabel?.text = error.localizedDescription
            }
        }
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)
        for node in nodes(at: point) {
            guard let name = node.parent?.name ?? node.name else { continue }
            switch name {
            case "startDuel":
                startDuel()
                return
            case "answerDuel":
                promptForCode()
                return
            default: break
            }
        }

        if let tab = TabBar.tab(at: point, in: self), tab != .social {
            TabBar.present(tab, from: self)
        }
    }
}
