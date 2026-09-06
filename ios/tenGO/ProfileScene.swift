//
//  ProfileScene.swift
//  tenGO
//
//  Vitrine de la progression du joueur : niveau + XP, statistiques clés,
//  progression cumulée par mode, thème actif. Accessible en un tap depuis
//  la Home (chip niveau).
//

import SpriteKit

class ProfileScene: SKScene {

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = ThemeManager.shared.active.background
        addChild(ThemeBackground.make(for: ThemeManager.shared.active, size: size))
        setupUI()
    }

    // MARK: - UI

    /// Largeur réellement visible en coordonnées scène (scène 750×1334 en
    /// aspectFill : les bords latéraux sont rognés).
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

        let title = SKLabelNode(text: String(localized: "profile.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = UIColor(white: 0.28, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: topY - 110)
        addChild(title)

        var cursorY = topY - 190
        cursorY = addHeaderCard(atY: cursorY)
        cursorY -= 26

        // Ce qui se fait vient avant ce qui se lit : les trois destinations que
        // l'accueil ne porte plus ouvrent cet écran, les chiffres suivent.
        addNavRow(label: String(localized: "menu.achievements"), name: "achievements", atY: cursorY)
        cursorY -= 64
        addNavRow(label: String(localized: "menu.missions"), name: "missions", atY: cursorY,
                  badged: MissionManager.shared.hasUnclaimedReward)
        cursorY -= 64
        addNavRow(label: String(localized: "menu.leaderboard"), name: "classement", atY: cursorY)
        cursorY -= 64

        cursorY -= 18
        cursorY = addSectionLabel(String(localized: "profile.section_stats"), atY: cursorY)
        let stats: [(String, String)] = [
            (String(localized: "profile.best_score"), "\(GameState.highScores().first ?? 0)"),
            (String(localized: "profile.best_chain"), "\(PlayerStatsManager.shared.bestChainEver)"),
            (String(localized: "profile.perfect_count"), "\(PlayerStatsManager.shared.perfectBoardsTotal)"),
            (String(localized: "profile.daily_streak"), "\(StreakManager.shared.current) (\(String(format: String(localized: "profile.best_suffix"), StreakManager.shared.best)))"),
            (String(localized: "profile.rush_best"), "\(GameState.rushBest())"),
        ]
        for (label, value) in stats {
            addStatRow(label: label, value: value, atY: cursorY)
            cursorY -= 62
        }

        cursorY -= 18
        cursorY = addSectionLabel(String(localized: "profile.section_progress"), atY: cursorY)
        let progress: [(String, String)] = [
            (String(localized: "profile.total_chains"), "\(PlayerStatsManager.shared.totalChainsMade)"),
            (String(localized: "profile.total_rush_games"), "\(PlayerStatsManager.shared.totalRushGames)"),
            (String(localized: "profile.total_daily"), "\(PlayerStatsManager.shared.totalDailyCompletions)"),
        ]
        for (label, value) in progress {
            addStatRow(label: label, value: value, atY: cursorY)
            cursorY -= 62
        }

        cursorY -= 18
        let theme = ThemeManager.shared.active
        let themeValue = "\(theme.emoji) " + String(localized: String.LocalizationValue(theme.nameKey))
        addStatRow(label: String(localized: "profile.active_theme"), value: themeValue, atY: cursorY)

        // Onglets en pied d'écran : cet écran EST l'onglet Progression.
        let tabBar = TabBar.make(width: usableWidth, selected: .progress)
        tabBar.position = CGPoint(x: 0, y: bottomY + TabBar.height / 2)
        addChild(tabBar)
    }

    /// Niveau + titre + barre de progression XP. Retourne le y sous la carte.
    private func addHeaderCard(atY y: CGFloat) -> CGFloat {
        let level = LevelManager.shared.level
        let cardW = cardWidth

        let levelLabel = SKLabelNode(text: String(format: String(localized: "level.chip.label"), level))
        levelLabel.fontName = "AvenirNext-Heavy"
        levelLabel.fontSize = 30
        levelLabel.fontColor = UIColor(white: 0.24, alpha: 1)
        levelLabel.verticalAlignmentMode = .center
        levelLabel.position = CGPoint(x: 0, y: y)
        addChild(levelLabel)

        let titleLabel = SKLabelNode(text: LevelManager.shared.currentTitle)
        titleLabel.fontName = "AvenirNext-Medium"
        titleLabel.fontSize = 18
        titleLabel.fontColor = UIColor(white: 0.42, alpha: 1)
        titleLabel.verticalAlignmentMode = .center
        titleLabel.position = CGPoint(x: 0, y: y - 30)
        addChild(titleLabel)

        let into = LevelManager.shared.xpIntoCurrentLevel
        let forNext = LevelManager.shared.xpForNextLevel
        let progressText = forNext > 0
            ? String(format: String(localized: "level.overlay.xp_progress"), into, forNext)
            : String(localized: "level.title.ten_master")
        let progressLabel = SKLabelNode(text: progressText)
        progressLabel.fontName = "AvenirNext-Medium"
        progressLabel.fontSize = 13
        progressLabel.fontColor = UIColor(white: 0.48, alpha: 1)
        progressLabel.verticalAlignmentMode = .center
        progressLabel.position = CGPoint(x: 0, y: y - 54)
        addChild(progressLabel)

        let barW: CGFloat = cardW - 40
        let barH: CGFloat = 9
        let barY = y - 74
        let track = SKShapeNode(rectOf: CGSize(width: barW, height: barH), cornerRadius: barH / 2)
        track.fillColor = UIColor(white: 0.85, alpha: 1)
        track.strokeColor = .clear
        track.position = CGPoint(x: 0, y: barY)
        addChild(track)

        let ratio: CGFloat = forNext > 0 ? min(1, max(0, CGFloat(into) / CGFloat(forNext))) : 1
        let fillW = max(barH, barW * ratio)
        let fill = SKShapeNode(rectOf: CGSize(width: fillW, height: barH), cornerRadius: barH / 2)
        fill.fillColor = ThemeManager.shared.active.accent
        fill.strokeColor = .clear
        fill.position = CGPoint(x: -barW / 2 + fillW / 2, y: barY)
        addChild(fill)

        return barY - 20
    }

    private func addSectionLabel(_ text: String, atY y: CGFloat) -> CGFloat {
        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 17
        label.fontColor = UIColor(white: 0.40, alpha: 0.9)
        label.horizontalAlignmentMode = .left
        label.verticalAlignmentMode = .center
        label.position = CGPoint(x: -cardWidth / 2, y: y)
        addChild(label)
        return y - 34
    }

    private func addStatRow(label: String, value: String, atY y: CGFloat) {
        let width = cardWidth
        let height: CGFloat = 54

        let row = SKNode()
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: 16)
        bg.fillColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 0.9)
        bg.strokeColor = UIColor(white: 0.72, alpha: 0.25)
        bg.lineWidth = 1
        row.addChild(bg)

        let labelNode = SKLabelNode(text: label)
        labelNode.fontName = "AvenirNext-Medium"
        labelNode.fontSize = 17
        labelNode.fontColor = UIColor(white: 0.36, alpha: 1)
        labelNode.horizontalAlignmentMode = .left
        labelNode.verticalAlignmentMode = .center
        labelNode.position = CGPoint(x: -width / 2 + 22, y: 0)
        row.addChild(labelNode)

        let valueNode = SKLabelNode(text: value)
        valueNode.fontName = "AvenirNext-Bold"
        valueNode.fontSize = 18
        valueNode.fontColor = UIColor(white: 0.22, alpha: 1)
        valueNode.horizontalAlignmentMode = .right
        valueNode.verticalAlignmentMode = .center
        valueNode.position = CGPoint(x: width / 2 - 22, y: 0)
        row.addChild(valueNode)
    }

    /// Rangée cliquable vers un autre écran. Même gabarit que `addStatRow`
    /// pour que la liste reste une liste ; le chevron dit ce qui est cliquable.
    private func addNavRow(label: String, name: String, atY y: CGFloat, badged: Bool = false) {
        let width = cardWidth
        let height: CGFloat = 54

        let row = SKNode()
        row.name = name
        row.position = CGPoint(x: 0, y: y)
        addChild(row)

        let bg = SKShapeNode(rectOf: CGSize(width: width, height: height), cornerRadius: 16)
        bg.fillColor = ThemeManager.shared.active.accent.withAlphaComponent(0.16)
        bg.strokeColor = ThemeManager.shared.active.accent.withAlphaComponent(0.35)
        bg.lineWidth = 1
        bg.name = name
        row.addChild(bg)

        let labelNode = SKLabelNode(text: label)
        labelNode.fontName = "AvenirNext-DemiBold"
        labelNode.fontSize = 17
        labelNode.fontColor = UIColor(white: 0.30, alpha: 1)
        labelNode.horizontalAlignmentMode = .left
        labelNode.verticalAlignmentMode = .center
        labelNode.position = CGPoint(x: -width / 2 + 22, y: 0)
        row.addChild(labelNode)

        let chevron = SKLabelNode(text: "›")
        chevron.fontName = "AvenirNext-Medium"
        chevron.fontSize = 26
        chevron.fontColor = UIColor(white: 0.45, alpha: 1)
        chevron.horizontalAlignmentMode = .right
        chevron.verticalAlignmentMode = .center
        chevron.position = CGPoint(x: width / 2 - 22, y: 0)
        row.addChild(chevron)

        if badged {
            let badge = SKShapeNode(circleOfRadius: 7)
            badge.fillColor = UIColor(red: 0.92, green: 0.32, blue: 0.30, alpha: 1)
            badge.strokeColor = UIColor(white: 1, alpha: 0.9)
            badge.lineWidth = 1.5
            badge.position = CGPoint(x: -width / 2 + 22 + labelNode.frame.width + 16, y: 6)
            row.addChild(badge)
        }
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)

        for node in nodes(at: point) {
            let destination: SKScene?
            switch node.name ?? node.parent?.name {
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
