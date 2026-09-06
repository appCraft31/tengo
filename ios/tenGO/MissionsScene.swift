//
//  MissionsScene.swift
//  tenGO
//
//  Missions du jour : liste des 3 missions + la super mission, progression
//  en direct, réclamation de la récompense une fois complétée.
//

import SpriteKit

class MissionsScene: SKScene {

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
        setupUI()
    }

    // MARK: - UI

    /// Largeur réellement visible en coordonnées scène : la scène fait
    /// 750×1334 en aspectFill, les bords latéraux sont rognés. Une largeur
    /// de carte en dur y occuperait à peine la moitié de l'écran.
    private var cardWidth: CGFloat = 340

    private func setupUI() {
        guard let view = view else { return }
        let topY = size.height / 2
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        let safeBottomInset = view.safeAreaInsets.bottom / scale
        let bottomY = -visibleHalfH + safeBottomInset
        cardWidth = min(usableWidth - 48, 600)

        let title = SKLabelNode(text: String(localized: "missions.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 40
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        // Les cartes remplissent la bande disponible au lieu de se tasser en
        // haut de l'écran : 4 missions seulement, autant qu'elles respirent.
        let missions = MissionManager.shared.todaysMissions
        let listTop = topY - 180
        let listBottom = bottomY + 150
        let available = max(0, listTop - listBottom)
        let gap: CGFloat = 18
        let count = CGFloat(max(1, missions.count))
        let rowH = max(96, min(150, available / count - gap))
        // Bloc centré verticalement dans la bande.
        let blockH = rowH * count + gap * (count - 1)
        var cursorY = listTop - max(0, (available - blockH) / 2) - rowH / 2

        for mission in missions {
            addMissionRow(mission, atY: cursorY, height: rowH)
            cursorY -= (rowH + gap)
        }

        addBackButton(atY: bottomY + 90)
    }

    private func addMissionRow(_ mission: MissionManager.DisplayMission, atY y: CGFloat, height: CGFloat) {
        let def = mission.definition
        let width = cardWidth

        let row = SKNode()
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: 22)
        bg.fillColor = def.isSuper
            ? UIColor(red: 0.94, green: 0.88, blue: 0.98, alpha: 1)
            : UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.35)
        bg.lineWidth = 1
        row.addChild(bg)

        // Deux colonnes : texte à gauche, récompense/bouton à droite. La barre
        // de progression court en bas sur toute la largeur, sans passer sous
        // la colonne de droite.
        let rightColumnW: CGFloat = 150
        let textLeft = -width / 2 + 22
        let titleY = height * 0.14

        let title = SKLabelNode(text: missionTitle(for: def))
        title.fontName = "AvenirNext-DemiBold"
        title.fontSize = 19
        title.fontColor = UIColor(white: 0.26, alpha: 1)
        title.horizontalAlignmentMode = .left
        title.verticalAlignmentMode = .center
        title.numberOfLines = 2
        title.preferredMaxLayoutWidth = width - rightColumnW - 44
        title.position = CGPoint(x: textLeft, y: titleY)
        row.addChild(title)

        let progressLabel = SKLabelNode(text: "\(mission.progress) / \(def.target)")
        progressLabel.fontName = "AvenirNext-Medium"
        progressLabel.fontSize = 14
        progressLabel.fontColor = UIColor(white: 0.45, alpha: 1)
        progressLabel.horizontalAlignmentMode = .left
        progressLabel.verticalAlignmentMode = .center
        progressLabel.position = CGPoint(x: textLeft, y: -height * 0.16)
        row.addChild(progressLabel)

        // Barre de progression, en bas de la carte.
        let barW = width - 44
        let barH: CGFloat = 10
        let barY = -height * 0.34
        let track = SKShapeNode(rectOf: CGSize(width: barW, height: barH), cornerRadius: barH / 2)
        track.fillColor = UIColor(white: 0.85, alpha: 1)
        track.strokeColor = .clear
        track.position = CGPoint(x: 0, y: barY)
        row.addChild(track)

        let ratio = def.target > 0 ? min(1, max(0, CGFloat(mission.progress) / CGFloat(def.target))) : 0
        let fillW = max(barH, barW * ratio)
        let fill = SKShapeNode(rectOf: CGSize(width: fillW, height: barH), cornerRadius: barH / 2)
        fill.fillColor = mission.isCompleted ? UIColor(red: 0.42, green: 0.72, blue: 0.48, alpha: 1) : ThemeManager.shared.active.accent
        fill.strokeColor = .clear
        fill.position = CGPoint(x: -barW / 2 + fillW / 2, y: barY)
        row.addChild(fill)

        // Colonne de droite : récompense, bouton de réclamation ou coche.
        if mission.claimed {
            let check = SKLabelNode(text: "✓")
            check.fontName = "AvenirNext-Bold"
            check.fontSize = 32
            check.fontColor = UIColor(red: 0.45, green: 0.62, blue: 0.48, alpha: 1)
            check.verticalAlignmentMode = .center
            check.horizontalAlignmentMode = .center
            check.position = CGPoint(x: width / 2 - 52, y: titleY)
            row.addChild(check)
        } else if mission.isCompleted {
            let claimBtn = SKNode()
            claimBtn.name = "claim_\(def.id)"
            claimBtn.position = CGPoint(x: width / 2 - 76, y: titleY)
            row.addChild(claimBtn)

            let claimBg = SKShapeNode(rectOf: CGSize(width: 124, height: 52), cornerRadius: 26)
            claimBg.fillColor = UIColor(red: 0.98, green: 0.80, blue: 0.35, alpha: 1)
            claimBg.strokeColor = UIColor(white: 0.60, alpha: 0.35)
            claimBg.lineWidth = 1
            claimBtn.addChild(claimBg)

            let claimLabel = SKLabelNode(text: String(localized: "missions.claim"))
            claimLabel.fontName = "AvenirNext-Bold"
            claimLabel.fontSize = 17
            claimLabel.fontColor = UIColor(white: 0.2, alpha: 1)
            claimLabel.verticalAlignmentMode = .center
            claimLabel.horizontalAlignmentMode = .center
            claimBtn.addChild(claimLabel)
        } else {
            let coin = CoinIcon.make(radius: 12)
            coin.position = CGPoint(x: width / 2 - 92, y: titleY)
            row.addChild(coin)

            let rewardLabel = SKLabelNode(text: "+\(def.coinReward)")
            rewardLabel.fontName = "AvenirNext-DemiBold"
            rewardLabel.fontSize = 19
            rewardLabel.fontColor = UIColor(white: 0.35, alpha: 1)
            rewardLabel.horizontalAlignmentMode = .left
            rewardLabel.verticalAlignmentMode = .center
            rewardLabel.position = CGPoint(x: width / 2 - 74, y: titleY)
            row.addChild(rewardLabel)
        }

        if def.isSuper {
            let badge = SKLabelNode(text: String(localized: "missions.super_badge"))
            badge.fontName = "AvenirNext-Bold"
            badge.fontSize = 11
            badge.fontColor = UIColor(red: 0.55, green: 0.35, blue: 0.75, alpha: 1)
            badge.horizontalAlignmentMode = .left
            badge.verticalAlignmentMode = .center
            badge.position = CGPoint(x: -width / 2 + 20, y: height / 2 - 11)
            row.addChild(badge)
        }
    }

    private func missionTitle(for def: MissionDefinition) -> String {
        switch def.kind {
        case .chainAtLeast:
            return String(format: String(localized: "mission.chain_at_least"), def.target, def.param)
        case .cumulativeScore:
            return String(format: String(localized: "mission.cumulative_score"), def.target)
        case .gamesPlayed:
            return String(format: String(localized: "mission.games_played"), def.target)
        case .perfectBoards:
            return String(format: String(localized: "mission.perfect_boards"), def.target)
        case .movesPlayed:
            return String(format: String(localized: "mission.moves_played"), def.target)
        }
    }

    private func addBackButton(atY y: CGFloat) {
        let back = SKNode()
        back.name = "back"
        back.position = CGPoint(x: 0, y: y)
        addChild(back)

        let circle = SKShapeNode(circleOfRadius: 36)
        circle.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        circle.strokeColor = UIColor(white: 0.68, alpha: 0.45)
        circle.lineWidth = 1.5
        back.addChild(circle)

        let icon = SKLabelNode(text: "‹")
        icon.fontName = "AvenirNext-Medium"
        icon.fontSize = 32
        icon.fontColor = UIColor(white: 0.45, alpha: 1)
        icon.verticalAlignmentMode = .center
        icon.horizontalAlignmentMode = .center
        back.addChild(icon)
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)
        for node in nodes(at: point) {
            guard let name = node.parent?.name ?? node.name else { continue }
            if name == "back" {
                let menu = MenuScene(size: size)
                menu.scaleMode = .aspectFill
                view?.presentScene(menu, transition: SceneTransition.crossFade(0.28))
                return
            }
            if name.hasPrefix("claim_") {
                let id = String(name.dropFirst("claim_".count))
                if MissionManager.shared.claim(id) > 0 {
                    let refreshed = MissionsScene(size: size)
                    refreshed.scaleMode = .aspectFill
                    view?.presentScene(refreshed, transition: SceneTransition.crossFade(0.2))
                }
                return
            }
        }
    }
}
