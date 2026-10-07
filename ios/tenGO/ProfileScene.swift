//
//  ProfileScene.swift
//  tenGO
//
//  Onglet Progression : carte de niveau (XP), tuiles de statistiques, missions
//  du jour, succès et accès au classement. Les missions se réclament ici ; les
//  écrans détaillés (Missions, Succès, Classement) restent à un tap.
//

import SpriteKit

class ProfileScene: SKScene {

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        rebuild()
    }

    // MARK: - UI

    /// Largeur réellement visible en coordonnées scène (scène 750×1334 en
    /// aspectFill : les bords latéraux sont rognés).
    private var cardWidth: CGFloat = 340
    /// Facteur de tassement vertical : < 1 quand l'écran est trop court (iPad)
    /// pour la hauteur nominale du contenu.
    private var squeeze: CGFloat = 1

    private var theme: Theme { ThemeManager.shared.active }

    /// Reconstruit tout l'écran (appelé à l'ouverture et après une réclamation).
    private func rebuild() {
        removeAllChildren()
        addChild(ThemeBackground.make(for: theme, size: size))
        setupUI()
    }

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
        let title = SKLabelNode(text: String(localized: "profile.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: titleY)
        addChild(title)

        let tabBar = TabBar.make(width: usableWidth, selected: .progress)
        tabBar.position = CGPoint(x: 0, y: bottomY + TabBar.height / 2)
        addChild(tabBar)

        // Hauteurs nominales des blocs ; `squeeze` les tasse sur un écran court.
        let headerH: CGFloat = 150, tileH: CGFloat = 92, tileGap: CGFloat = 12
        let sectionH: CGFloat = 34, missionH: CGFloat = 88, missionGap: CGFloat = 12
        let badgesH: CGFloat = 84, navH: CGFloat = 58
        let missions = featuredMissions()
        let missionsBlock = missions.isEmpty
            ? 0
            : sectionH + CGFloat(missions.count) * missionH + CGFloat(missions.count - 1) * missionGap + 22
        let nominal = headerH + 18 + tileH * 3 + tileGap * 2 + 22
            + missionsBlock + sectionH + badgesH + 16 + navH

        let top = titleY - 48
        let available = top - (bottomY + TabBar.height + 14)
        squeeze = min(1, available / nominal)
        var cursor = top
        func place(_ height: CGFloat) -> CGFloat {
            let center = cursor - height * squeeze / 2
            cursor -= height * squeeze
            return center
        }
        func gap(_ height: CGFloat) { cursor -= height * squeeze }

        addLevelCard(atY: place(headerH), height: headerH * squeeze)
        gap(18)

        let stats: [(value: String, label: String, tint: UIColor)] = [
            ("\(GameState.highScores().first ?? 0)", String(localized: "profile.best_score"), theme.color(forValue: 3)),
            ("\(StreakManager.shared.current)", String(localized: "profile.daily_streak"), theme.color(forValue: 2)),
            ("\(PlayerStatsManager.shared.bestChainEver)", String(localized: "profile.best_chain"), theme.color(forValue: 5)),
            ("\(PlayerStatsManager.shared.perfectBoardsTotal)", String(localized: "profile.perfect_count"), theme.color(forValue: 7)),
            ("\(GameState.rushBest())", String(localized: "profile.rush_best"), theme.color(forValue: 4)),
            ("\(PlayerStatsManager.shared.totalChainsMade)", String(localized: "profile.total_chains"), theme.color(forValue: 9)),
        ]
        let tileW = (cardWidth - tileGap) / 2
        for row in 0..<3 {
            let y = place(tileH)
            for column in 0..<2 {
                let stat = stats[row * 2 + column]
                addStatTile(value: stat.value, label: stat.label, tint: stat.tint,
                            at: CGPoint(x: (column == 0 ? -1 : 1) * (tileW + tileGap) / 2, y: y),
                            size: CGSize(width: tileW, height: tileH * squeeze))
            }
            if row < 2 { gap(tileGap) }
        }
        gap(22)

        if !missions.isEmpty {
            addSectionHeader(String(localized: "menu.missions"), trailing: "›", name: "missions",
                             atY: place(sectionH), badged: MissionManager.shared.hasUnclaimedReward)
            for (index, mission) in missions.enumerated() {
                addMissionRow(mission, atY: place(missionH), height: missionH * squeeze)
                if index < missions.count - 1 { gap(missionGap) }
            }
            gap(22)
        }

        let all = AchievementManager.achievements()
        addSectionHeader(String(localized: "menu.achievements"),
                         trailing: "\(all.filter(\.isUnlocked).count) / \(all.count)  ›",
                         name: "achievements", atY: place(sectionH), badged: false)
        addAchievementBadges(all, atY: place(badgesH), height: badgesH * squeeze)
        gap(16)

        addNavRow(label: String(localized: "menu.leaderboard"), name: "classement",
                  atY: place(navH), height: (navH - 6) * squeeze)
    }

    /// Les deux missions à montrer ici : d'abord celles à réclamer, puis
    /// celles en cours, enfin celles déjà réclamées.
    private func featuredMissions() -> [MissionManager.DisplayMission] {
        func rank(_ mission: MissionManager.DisplayMission) -> Int {
            if mission.claimed { return 2 }
            return mission.isCompleted ? 0 : 1
        }
        let sorted = MissionManager.shared.todaysMissions.enumerated()
            .sorted { (rank($0.element), $0.offset) < (rank($1.element), $1.offset) }
            .map(\.element)
        return Array(sorted.prefix(2))
    }

    // MARK: - Blocs

    /// Carte de niveau : bulle du niveau, titre, XP et barre de progression.
    private func addLevelCard(atY y: CGFloat, height: CGFloat) {
        let tint = theme.color(forValue: 6)
        let ink = tint.readableInk()

        let card = SKNode()
        card.name = "levelCard"
        card.position = CGPoint(x: 0, y: y)
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 30)
        bg.fillColor = tint
        bg.strokeColor = .clear
        card.addChild(bg)
        Relief.raise(bg, depth: 6)

        let bubbleR = min(46, height / 2 - 14)
        let bubbleX = -cardWidth / 2 + 26 + bubbleR
        let bubble = SKShapeNode(circleOfRadius: bubbleR)
        bubble.fillColor = .white
        bubble.strokeColor = .clear
        bubble.position = CGPoint(x: bubbleX, y: 0)
        card.addChild(bubble)
        Relief.raise(bubble, depth: 4)

        let number = SKLabelNode(text: "\(LevelManager.shared.level)")
        number.fontName = "AvenirNext-Heavy"
        number.fontSize = 38
        number.fontColor = UIColor(white: 0.24, alpha: 1)
        number.verticalAlignmentMode = .center
        number.position = bubble.position
        if number.frame.width > bubbleR * 1.5 { number.setScale(bubbleR * 1.5 / number.frame.width) }
        card.addChild(number)

        let textX = bubbleX + bubbleR + 22
        let textW = cardWidth / 2 - 26 - textX

        let levelTitle = SKLabelNode(text: LevelManager.shared.currentTitle)
        levelTitle.fontName = "AvenirNext-Bold"
        levelTitle.fontSize = 26
        levelTitle.fontColor = ink
        levelTitle.horizontalAlignmentMode = .left
        levelTitle.verticalAlignmentMode = .center
        levelTitle.position = CGPoint(x: textX, y: 30)
        if levelTitle.frame.width > textW { levelTitle.setScale(textW / levelTitle.frame.width) }
        card.addChild(levelTitle)

        let into = LevelManager.shared.xpIntoCurrentLevel
        let forNext = LevelManager.shared.xpForNextLevel
        let progressText = forNext > 0
            ? String(format: String(localized: "level.overlay.xp_progress"), into, forNext)
            : String(localized: "level.title.ten_master")
        let progress = SKLabelNode(text: progressText)
        progress.fontName = "AvenirNext-DemiBold"
        progress.fontSize = 16
        progress.fontColor = ink.withAlphaComponent(0.85)
        progress.horizontalAlignmentMode = .left
        progress.verticalAlignmentMode = .center
        progress.position = CGPoint(x: textX, y: -2)
        card.addChild(progress)

        let ratio: CGFloat = forNext > 0 ? min(1, max(0, CGFloat(into) / CGFloat(forNext))) : 1
        addBar(to: card, width: textW, ratio: ratio,
               track: UIColor(white: 1, alpha: 0.55), fill: tint.ledgeShade.ledgeShade,
               leftX: textX, y: -32)
    }

    /// Barre de progression arrondie, ancrée par son bord gauche.
    private func addBar(to parent: SKNode, width: CGFloat, ratio: CGFloat,
                        track trackColor: UIColor, fill fillColor: UIColor,
                        leftX: CGFloat, y: CGFloat) {
        let barH: CGFloat = 12
        let track = SKShapeNode(rectOf: CGSize(width: width, height: barH), cornerRadius: barH / 2)
        track.fillColor = trackColor
        track.strokeColor = .clear
        track.position = CGPoint(x: leftX + width / 2, y: y)
        parent.addChild(track)

        guard ratio > 0 else { return }
        let fillW = max(barH, width * ratio)
        let fill = SKShapeNode(rectOf: CGSize(width: fillW, height: barH), cornerRadius: barH / 2)
        fill.fillColor = fillColor
        fill.strokeColor = .clear
        fill.position = CGPoint(x: leftX + fillW / 2, y: y)
        parent.addChild(fill)
    }

    /// Tuile de statistique : le chiffre d'abord, son libellé dessous.
    private func addStatTile(value: String, label: String, tint: UIColor, at position: CGPoint, size: CGSize) {
        let fill = tint.mixedWithWhite(0.45)
        let ink = fill.readableInk()

        let tile = SKShapeNode(rectOf: size, cornerRadius: 24)
        tile.fillColor = fill
        tile.strokeColor = .clear
        tile.position = position
        addChild(tile)

        let valueNode = SKLabelNode(text: value)
        valueNode.fontName = "AvenirNext-Heavy"
        valueNode.fontSize = 30
        valueNode.fontColor = ink
        valueNode.verticalAlignmentMode = .center
        valueNode.position = CGPoint(x: position.x, y: position.y + 13)
        addChild(valueNode)

        let labelNode = SKLabelNode(text: label)
        labelNode.fontName = "AvenirNext-Medium"
        labelNode.fontSize = 15
        labelNode.fontColor = ink.withAlphaComponent(0.8)
        labelNode.verticalAlignmentMode = .center
        labelNode.position = CGPoint(x: position.x, y: position.y - 20)
        // Les libellés longs (de, nl) se réduisent plutôt que de déborder.
        if labelNode.frame.width > size.width - 20 { labelNode.setScale((size.width - 20) / labelNode.frame.width) }
        addChild(labelNode)
    }

    /// Titre de section, cliquable vers l'écran détaillé.
    private func addSectionHeader(_ text: String, trailing: String, name: String, atY y: CGFloat, badged: Bool) {
        let header = SKNode()
        header.name = name
        header.position = CGPoint(x: 0, y: y)
        addChild(header)

        // Zone tactile pleine largeur (alpha non nul : SpriteKit écarte du
        // hit-test une forme entièrement transparente).
        let hit = SKShapeNode(rectOf: CGSize(width: cardWidth, height: 44))
        hit.fillColor = UIColor(white: 1, alpha: 0.001)
        hit.strokeColor = .clear
        hit.name = name
        header.addChild(hit)

        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-Bold"
        label.fontSize = 22
        label.fontColor = theme.logo
        label.horizontalAlignmentMode = .left
        label.verticalAlignmentMode = .center
        label.position = CGPoint(x: -cardWidth / 2 + 4, y: 0)
        header.addChild(label)

        let trailingLabel = SKLabelNode(text: trailing)
        trailingLabel.fontName = "AvenirNext-DemiBold"
        trailingLabel.fontSize = 17
        trailingLabel.fontColor = theme.logo.withAlphaComponent(0.65)
        trailingLabel.horizontalAlignmentMode = .right
        trailingLabel.verticalAlignmentMode = .center
        trailingLabel.position = CGPoint(x: cardWidth / 2 - 6, y: 0)
        header.addChild(trailingLabel)

        if badged {
            let badge = SKShapeNode(circleOfRadius: 7)
            badge.fillColor = UIColor(red: 0.92, green: 0.32, blue: 0.30, alpha: 1)
            badge.strokeColor = UIColor(white: 1, alpha: 0.9)
            badge.lineWidth = 1.5
            badge.position = CGPoint(x: -cardWidth / 2 + 4 + label.frame.width + 14, y: 8)
            header.addChild(badge)
        }
    }

    /// Mission du jour : libellé, barre, puis récompense ou bouton « Réclamer ».
    private func addMissionRow(_ mission: MissionManager.DisplayMission, atY y: CGFloat, height: CGFloat) {
        let def = mission.definition
        let rightColumnW: CGFloat = 148
        let textLeft = -cardWidth / 2 + 22
        let textW = cardWidth - rightColumnW - 36

        let row = SKNode()
        row.name = "missions"
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: 24)
        bg.fillColor = Relief.surface
        bg.strokeColor = .clear
        bg.name = "missions"
        row.addChild(bg)
        Relief.raise(bg, depth: 4)

        let title = SKLabelNode(text: def.localizedTitle)
        title.fontName = "AvenirNext-DemiBold"
        title.fontSize = 17
        title.fontColor = theme.logo
        title.horizontalAlignmentMode = .left
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: textLeft, y: 16)
        if title.frame.width > textW { title.setScale(textW / title.frame.width) }
        row.addChild(title)

        let ratio = def.target > 0 ? min(1, max(0, CGFloat(mission.progress) / CGFloat(def.target))) : 0
        addBar(to: row, width: textW - 74, ratio: ratio,
               track: theme.logo.withAlphaComponent(0.12),
               fill: mission.isCompleted ? theme.color(forValue: 4).ledgeShade : theme.color(forValue: 7),
               leftX: textLeft, y: -16)

        let count = SKLabelNode(text: "\(min(mission.progress, def.target)) / \(def.target)")
        count.fontName = "AvenirNext-Medium"
        count.fontSize = 14
        count.fontColor = theme.logo.withAlphaComponent(0.7)
        count.horizontalAlignmentMode = .right
        count.verticalAlignmentMode = .center
        count.position = CGPoint(x: textLeft + textW, y: -16)
        row.addChild(count)

        let rightX = cardWidth / 2 - rightColumnW / 2 - 4
        if mission.claimed {
            let check = SKLabelNode(text: "✓")
            check.fontName = "AvenirNext-Bold"
            check.fontSize = 30
            check.fontColor = theme.color(forValue: 4).ledgeShade.ledgeShade
            check.verticalAlignmentMode = .center
            check.position = CGPoint(x: rightX, y: 0)
            row.addChild(check)
        } else if mission.isCompleted {
            let claim = SKNode()
            claim.name = "claim_\(def.id)"
            claim.position = CGPoint(x: rightX, y: 2)
            row.addChild(claim)

            let claimBg = SKShapeNode(rectOf: CGSize(width: 128, height: 48), cornerRadius: 24)
            claimBg.fillColor = theme.color(forValue: 4)
            claimBg.strokeColor = .clear
            claimBg.name = claim.name
            claim.addChild(claimBg)
            Relief.raise(claimBg, depth: 4)

            let claimLabel = SKLabelNode(text: String(localized: "missions.claim"))
            claimLabel.fontName = "AvenirNext-Bold"
            claimLabel.fontSize = 16
            claimLabel.fontColor = theme.color(forValue: 4).readableInk()
            claimLabel.verticalAlignmentMode = .center
            if claimLabel.frame.width > 110 { claimLabel.setScale(110 / claimLabel.frame.width) }
            claim.addChild(claimLabel)
        } else {
            // Récompense à venir : pastille dorée, comme le solde de pièces.
            let reward = SKLabelNode(text: "+\(def.coinReward)")
            reward.fontName = "AvenirNext-Bold"
            reward.fontSize = 17
            reward.fontColor = UIColor(red: 0.45, green: 0.34, blue: 0.10, alpha: 1)
            reward.horizontalAlignmentMode = .left
            reward.verticalAlignmentMode = .center
            let contentW = 20 + 7 + reward.frame.width
            let chip = SKShapeNode(rectOf: CGSize(width: contentW + 30, height: 40), cornerRadius: 20)
            chip.fillColor = UIColor(red: 0.99, green: 0.95, blue: 0.80, alpha: 1)
            chip.strokeColor = .clear
            chip.position = CGPoint(x: rightX, y: 0)
            row.addChild(chip)
            let coin = CoinIcon.make(radius: 10)
            coin.position = CGPoint(x: rightX - contentW / 2 + 10, y: 0)
            row.addChild(coin)
            reward.position = CGPoint(x: rightX - contentW / 2 + 27, y: 0)
            row.addChild(reward)
        }
    }

    /// Rangée de médaillons : les succès débloqués d'abord, puis ceux à venir.
    private func addAchievementBadges(_ all: [AchievementManager.DisplayAchievement], atY y: CGFloat, height: CGFloat) {
        let row = SKNode()
        row.name = "achievements"
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let radius = min(36, height / 2 - 6)
        let step = radius * 2 + 16
        let count = max(1, min(all.count, Int((cardWidth + 16) / step)))
        let shown = Array((all.filter(\.isUnlocked) + all.filter { !$0.isUnlocked }).prefix(count))
        let startX = -cardWidth / 2 + radius

        for (index, item) in shown.enumerated() {
            let center = CGPoint(x: startX + CGFloat(index) * step, y: 2)
            let tint = theme.color(forValue: [3, 2, 5, 7, 4, 9, 6][index % 7])
            let medal = SKShapeNode(circleOfRadius: radius)
            medal.fillColor = item.isUnlocked ? tint : theme.logo.withAlphaComponent(0.10)
            medal.strokeColor = .clear
            medal.position = center
            medal.name = "achievements"
            row.addChild(medal)
            if item.isUnlocked { Relief.raise(medal, depth: 4) }

            let symbol = item.isUnlocked ? item.definition.category.icon : "lock.fill"
            let ink = item.isUnlocked ? tint.readableInk() : theme.logo.withAlphaComponent(0.45)
            let config = UIImage.SymbolConfiguration(pointSize: 22, weight: .semibold)
            if let image = UIImage(systemName: symbol, withConfiguration: config)?
                .withTintColor(ink, renderingMode: .alwaysOriginal) {
                let sprite = SKSpriteNode(texture: SKTexture(image: image))
                let maxDim = max(image.size.width, image.size.height)
                let target = radius * 0.9
                sprite.size = CGSize(width: image.size.width / maxDim * target,
                                     height: image.size.height / maxDim * target)
                sprite.position = center
                row.addChild(sprite)
            }
        }
    }

    /// Rangée cliquable vers un autre écran ; le chevron dit qu'elle l'est.
    private func addNavRow(label: String, name: String, atY y: CGFloat, height: CGFloat) {
        let row = SKNode()
        row.name = name
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: cardWidth, height: height), cornerRadius: height / 2)
        bg.fillColor = Relief.surface
        bg.strokeColor = .clear
        bg.name = name
        row.addChild(bg)
        Relief.raise(bg, depth: 4)

        let labelNode = SKLabelNode(text: label)
        labelNode.fontName = "AvenirNext-DemiBold"
        labelNode.fontSize = 18
        labelNode.fontColor = theme.logo
        labelNode.horizontalAlignmentMode = .left
        labelNode.verticalAlignmentMode = .center
        labelNode.position = CGPoint(x: -cardWidth / 2 + 26, y: 0)
        row.addChild(labelNode)

        let chevron = SKLabelNode(text: "›")
        chevron.fontName = "AvenirNext-Medium"
        chevron.fontSize = 26
        chevron.fontColor = theme.logo.withAlphaComponent(0.6)
        chevron.horizontalAlignmentMode = .right
        chevron.verticalAlignmentMode = .center
        chevron.position = CGPoint(x: cardWidth / 2 - 26, y: 2)
        row.addChild(chevron)
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)
        let names = nodes(at: point).compactMap { $0.name ?? $0.parent?.name }

        // « Réclamer » d'abord : le bouton est posé sur une rangée elle-même
        // cliquable vers l'écran Missions.
        if let claim = names.first(where: { $0.hasPrefix("claim_") }) {
            if MissionManager.shared.claim(String(claim.dropFirst("claim_".count))) > 0 {
                HapticManager.medium()
                rebuild()
            }
            return
        }

        for name in names {
            let destination: SKScene?
            switch name {
            case "achievements": destination = AchievementsScene(size: size)
            case "missions":     destination = MissionsScene(size: size)
            case "classement":   destination = LeaderboardScene(size: size)
            default:             destination = nil
            }
            if let destination {
                destination.scaleMode = .aspectFill
                view?.presentScene(destination, transition: SceneTransition.fade(0.28))
                return
            }
        }

        if let tab = TabBar.tab(at: point, in: self), tab != .progress {
            TabBar.present(tab, from: self)
        }
    }
}
