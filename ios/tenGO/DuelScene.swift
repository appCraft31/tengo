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

    private var theme: Theme { ThemeManager.shared.active }
    /// Haut de la liste des duels, sous les deux cartes d'action.
    private var listTopY: CGFloat = 0

    private func setupUI() {
        guard let view = view else { return }
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        // safeAreaInsets peut être nul au lancement : plancher à ~47 pt (encoche).
        let safeTop = max(view.safeAreaInsets.top, 47) / scale
        let bottomY = -visibleHalfH + view.safeAreaInsets.bottom / scale
        cardWidth = min(usableWidth - 48, 600)

        let titleY = visibleHalfH - safeTop - 36
        let title = SKLabelNode(text: String(localized: "duel.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: titleY)
        addChild(title)

        let pitchH: CGFloat = 268
        let pitchY = titleY - 48 - pitchH / 2
        addPitchCard(atY: pitchY, height: pitchH)

        let codeH: CGFloat = 92
        let codeY = pitchY - pitchH / 2 - 22 - codeH / 2
        addCodeCard(atY: codeY, height: codeH)

        let status = SKLabelNode(text: "")
        status.fontName = "AvenirNext-Medium"
        status.fontSize = 15
        status.fontColor = theme.logo.withAlphaComponent(0.75)
        status.verticalAlignmentMode = .center
        status.numberOfLines = 2
        status.preferredMaxLayoutWidth = cardWidth
        status.position = CGPoint(x: 0, y: codeY - codeH / 2 - 34)
        addChild(status)
        statusLabel = status
        listTopY = codeY - codeH / 2 - 26

        // Cet écran EST l'onglet Social : pas de retour, des onglets.
        let tabBar = TabBar.make(width: usableWidth, selected: .social)
        tabBar.position = CGPoint(x: 0, y: bottomY + TabBar.height / 2)
        addChild(tabBar)
    }

    /// Carte d'appel : « moi contre ? », la règle en une phrase, et l'action
    /// principale de l'écran.
    private func addPitchCard(atY y: CGFloat, height: CGFloat) {
        let tint = theme.color(forValue: 1)
        let ink = tint.readableInk()
        let half = height / 2

        let card = SKNode()
        card.position = CGPoint(x: 0, y: y)
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 30)
        bg.fillColor = tint
        bg.strokeColor = .clear
        card.addChild(bg)
        Relief.raise(bg, depth: 6)

        let avatarY = half - 68
        // Sans pseudo Game Center, `displayName` rend le nom anonyme : pas
        // d'initiale à en tirer.
        let me = DuelService.displayName
        let isAnonymous = me == String(localized: "duel.anonymous_player")
        addAvatar(to: card, initial: isAnonymous ? nil : me.first.map { String($0).uppercased() }, radius: 42,
                  tint: theme.color(forValue: 5), at: CGPoint(x: -96, y: avatarY))
        addAvatar(to: card, initial: "?", radius: 42,
                  tint: UIColor(white: 1, alpha: 1), at: CGPoint(x: 96, y: avatarY), raised: false)

        let versus = SKShapeNode(rectOf: CGSize(width: 62, height: 36), cornerRadius: 18)
        versus.fillColor = .white
        versus.strokeColor = .clear
        versus.position = CGPoint(x: 0, y: avatarY)
        card.addChild(versus)
        let versusLabel = SKLabelNode(text: "VS")
        versusLabel.fontName = "AvenirNext-Heavy"
        versusLabel.fontSize = 17
        versusLabel.fontColor = UIColor(white: 0.24, alpha: 1)
        versusLabel.verticalAlignmentMode = .center
        versusLabel.position = versus.position
        card.addChild(versusLabel)

        let pitch = SKLabelNode(text: String(localized: "duel.subtitle"))
        pitch.fontName = "AvenirNext-Medium"
        pitch.fontSize = 16
        pitch.fontColor = ink.withAlphaComponent(0.9)
        pitch.verticalAlignmentMode = .center
        pitch.numberOfLines = 2
        pitch.preferredMaxLayoutWidth = cardWidth - 56
        pitch.position = CGPoint(x: 0, y: avatarY - 78)
        card.addChild(pitch)

        let button = SKNode()
        button.name = "startDuel"
        button.position = CGPoint(x: 0, y: -half + 48)
        card.addChild(button)
        let buttonBg = SKShapeNode(rectOf: CGSize(width: cardWidth - 48, height: 56), cornerRadius: 28)
        buttonBg.fillColor = .white
        buttonBg.strokeColor = .clear
        buttonBg.name = "startDuel"
        button.addChild(buttonBg)
        Relief.raise(buttonBg, depth: 4, surface: true)
        let buttonLabel = SKLabelNode(text: String(localized: "duel.start"))
        buttonLabel.fontName = "AvenirNext-Bold"
        buttonLabel.fontSize = 19
        buttonLabel.fontColor = theme.logo
        buttonLabel.verticalAlignmentMode = .center
        button.addChild(buttonLabel)
    }

    /// Pastille ronde portant une initiale (ou « ? » tant que l'adversaire
    /// est inconnu).
    private func addAvatar(to parent: SKNode, initial: String?, radius: CGFloat, tint: UIColor,
                           at position: CGPoint, raised: Bool = true) {
        let circle = SKShapeNode(circleOfRadius: radius)
        circle.fillColor = raised ? tint : tint.withAlphaComponent(0.55)
        circle.strokeColor = .clear
        circle.position = position
        parent.addChild(circle)
        if raised { Relief.raise(circle, depth: 4) }

        let ink = UIColor(white: 0.24, alpha: 1)
        if let initial {
            let label = SKLabelNode(text: initial)
            label.fontName = "AvenirNext-Heavy"
            label.fontSize = radius * 0.86
            label.fontColor = ink
            label.verticalAlignmentMode = .center
            label.position = position
            parent.addChild(label)
        } else {
            // Pas de pseudo Game Center : une silhouette plutôt qu'une lettre.
            let icon = VectorIcon.social.node(size: radius * 1.1, color: ink)
            icon.position = position
            parent.addChild(icon)
        }
    }

    /// Carte « j'ai reçu un code » : un faux champ et son bouton. Le tap ouvre
    /// la saisie système (SpriteKit n'a pas de champ de texte).
    private func addCodeCard(atY y: CGFloat, height: CGFloat) {
        let card = SKNode()
        card.name = "answerDuel"
        card.position = CGPoint(x: 0, y: y)
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 26)
        bg.fillColor = Relief.surface
        bg.strokeColor = .clear
        bg.name = "answerDuel"
        card.addChild(bg)
        Relief.raise(bg, depth: 4)

        let go = SKLabelNode(text: String(localized: "duel.go"))
        go.fontName = "AvenirNext-Bold"
        go.fontSize = 17
        go.verticalAlignmentMode = .center
        let goW = max(104, go.frame.width + 44)
        let goTint = theme.color(forValue: 4)
        let goBg = SKShapeNode(rectOf: CGSize(width: goW, height: 52), cornerRadius: 26)
        goBg.fillColor = goTint
        goBg.strokeColor = .clear
        goBg.position = CGPoint(x: cardWidth / 2 - 20 - goW / 2, y: 2)
        goBg.name = "answerDuel"
        card.addChild(goBg)
        Relief.raise(goBg, depth: 4)
        go.fontColor = goTint.readableInk()
        go.position = goBg.position
        card.addChild(go)

        let fieldLeft = -cardWidth / 2 + 20
        let fieldW = cardWidth - 40 - goW - 14
        let caption = SKLabelNode(text: String(localized: "duel.answer"))
        caption.fontName = "AvenirNext-DemiBold"
        caption.fontSize = 14
        caption.fontColor = theme.logo.withAlphaComponent(0.8)
        caption.horizontalAlignmentMode = .left
        caption.verticalAlignmentMode = .center
        caption.position = CGPoint(x: fieldLeft + 4, y: 26)
        if caption.frame.width > fieldW { caption.setScale(fieldW / caption.frame.width) }
        card.addChild(caption)

        let field = SKShapeNode(rectOf: CGSize(width: fieldW, height: 40), cornerRadius: 14)
        field.fillColor = theme.logo.withAlphaComponent(0.08)
        field.strokeColor = .clear
        field.position = CGPoint(x: fieldLeft + fieldW / 2, y: -12)
        field.name = "answerDuel"
        card.addChild(field)
        let placeholder = SKLabelNode(text: "A B C 1 2 3")
        placeholder.fontName = "AvenirNext-Bold"
        placeholder.fontSize = 17
        placeholder.fontColor = theme.logo.withAlphaComponent(0.38)
        placeholder.horizontalAlignmentMode = .left
        placeholder.verticalAlignmentMode = .center
        placeholder.position = CGPoint(x: fieldLeft + 16, y: -12)
        card.addChild(placeholder)
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
        children.filter { ($0.name ?? "").hasPrefix("duelRow") || ($0.name ?? "").hasPrefix("duelShare:") }
            .forEach { $0.removeFromParent() }
        guard let view = view, let uid = FirebaseService.shared.uid, !duels.isEmpty else { return }
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let bottomY = -view.bounds.height / scale / 2 + view.safeAreaInsets.bottom / scale

        let header = SKLabelNode(text: String(localized: "duel.section_list", defaultValue: "Tes duels"))
        header.name = "duelRowHeader"
        header.fontName = "AvenirNext-Bold"
        header.fontSize = 22
        header.fontColor = theme.logo
        header.horizontalAlignmentMode = .left
        header.verticalAlignmentMode = .center
        header.position = CGPoint(x: -cardWidth / 2 + 4, y: listTopY - 16)
        addChild(header)

        var cursorY = listTopY - 50
        let rowH: CGFloat = 72
        for duel in duels.prefix(5) where cursorY - rowH > bottomY + TabBar.height + 10 {
            addDuelRow(duel, uid: uid, atY: cursorY - rowH / 2, height: rowH)
            cursorY -= (rowH + 12)
        }
    }

    private func addDuelRow(_ duel: Duel, uid: String, atY y: CGFloat, height: CGFloat) {
        let row = SKNode()
        row.name = "duelRow"
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 24)
        bg.fillColor = Relief.surface
        bg.strokeColor = .clear
        row.addChild(bg)

        // Duel lancé par ce joueur et toujours sans adversaire : c'est ici
        // qu'on revient quand on a oublié d'envoyer le code sur le moment.
        // Sans ce rattrapage, un duel créé mais non partagé est perdu.
        let shareable = !duel.isComplete && !duel.isExpired && duel.challengerUid == uid
        if shareable {
            row.name = "duelShare:\(duel.code)"
            bg.name = row.name
        }
        Relief.raise(bg, depth: 4)

        // Adversaire : son initiale, ou « ? » tant que personne n'a répondu.
        let rival = duel.rivalName(for: uid).trimmingCharacters(in: .whitespaces)
        let avatarX = -cardWidth / 2 + 20 + 24
        let tints = [6, 2, 8, 5, 7, 3].map { theme.color(forValue: $0) }
        let tint = tints[abs(duel.code.unicodeScalars.reduce(0) { $0 + Int($1.value) }) % tints.count]
        addAvatar(to: row, initial: rival.first.map { String($0).uppercased() } ?? "?", radius: 24,
                  tint: tint, at: CGPoint(x: avatarX, y: 0), raised: false)

        // Statut : pastille colorée à droite.
        let status: String
        var statusFill = theme.logo.withAlphaComponent(0.08)
        var statusInk = theme.logo.withAlphaComponent(0.8)
        if duel.isComplete, let outcome = duel.outcome(for: uid) {
            switch outcome {
            case .win:
                status = String(localized: "duel.row_win")
                statusFill = UIColor(red: 0.99, green: 0.95, blue: 0.80, alpha: 1)
                statusInk = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
            case .loss:
                status = String(localized: "duel.row_loss")
                statusFill = UIColor(red: 0.99, green: 0.89, blue: 0.88, alpha: 1)
                statusInk = UIColor(red: 0.60, green: 0.24, blue: 0.22, alpha: 1)
            case .draw:
                status = String(localized: "duel.row_draw")
            }
        } else if duel.isExpired {
            status = String(localized: "duel.row_expired")
        } else {
            status = String(localized: "duel.row_pending")
        }
        let statusLabel = SKLabelNode(text: status)
        statusLabel.fontName = "AvenirNext-Bold"
        statusLabel.fontSize = 14
        statusLabel.fontColor = statusInk
        statusLabel.verticalAlignmentMode = .center
        let tagW = statusLabel.frame.width + 28
        let tagX = cardWidth / 2 - 18 - tagW / 2 - (shareable ? 40 : 0)
        let tag = SKShapeNode(rectOf: CGSize(width: tagW, height: 34), cornerRadius: 17)
        tag.fillColor = statusFill
        tag.strokeColor = .clear
        tag.position = CGPoint(x: tagX, y: 0)
        tag.name = row.name
        row.addChild(tag)
        statusLabel.position = tag.position
        row.addChild(statusLabel)

        if shareable {
            let icon = VectorIcon.share.node(size: 22, color: theme.logo.withAlphaComponent(0.75))
            icon.position = CGPoint(x: cardWidth / 2 - 30, y: 0)
            row.addChild(icon)
        }

        // Nom de l'adversaire (ou code du duel), puis les scores.
        let textX = avatarX + 24 + 14
        let textW = tagX - tagW / 2 - 12 - textX
        let name = SKLabelNode(text: rival.isEmpty ? duel.code : rival)
        name.fontName = "AvenirNext-Bold"
        name.fontSize = 17
        name.fontColor = theme.logo
        name.horizontalAlignmentMode = .left
        name.verticalAlignmentMode = .center
        name.position = CGPoint(x: textX, y: 11)
        if name.frame.width > textW { name.setScale(textW / name.frame.width) }
        row.addChild(name)

        let mine = uid == duel.challengerUid ? duel.challengerScore : (duel.opponentScore ?? 0)
        let theirs = uid == duel.challengerUid ? duel.opponentScore : duel.challengerScore
        let points = String(localized: "game.points_label")
        var detail = theirs.map { "\(mine) – \($0) \(points)" } ?? "\(mine) \(points)"
        if !rival.isEmpty { detail = "\(duel.code) · " + detail }
        let sub = SKLabelNode(text: detail)
        sub.fontName = "AvenirNext-Medium"
        sub.fontSize = 13
        sub.fontColor = theme.logo.withAlphaComponent(0.7)
        sub.horizontalAlignmentMode = .left
        sub.verticalAlignmentMode = .center
        sub.position = CGPoint(x: textX, y: -12)
        if sub.frame.width > textW { sub.setScale(textW / sub.frame.width) }
        row.addChild(sub)
    }

    // MARK: - Actions

    /// Le challenger joue d'abord : le duel n'est créé qu'avec un vrai score,
    /// ce qui évite de laisser des duels vides derrière soi.
    private func startDuel() {
        let seed = UInt64.random(in: 1...UInt64.max)
        let scene = GameScene(size: size, duelSeed: seed, duelCode: nil)
        scene.scaleMode = .aspectFill
        view?.presentScene(scene, transition: SceneTransition.fade(0.3))
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
                view?.presentScene(scene, transition: SceneTransition.fade(0.35))
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
            if name.hasPrefix("duelShare:") {
                let code = String(name.dropFirst("duelShare:".count))
                DuelShare.present(code: code, from: presenter)
                return
            }
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
