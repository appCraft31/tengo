//
//  LevelProgressOverlay.swift
//  tenGO
//
//  Panneau de détail du niveau joueur (XP/Levels) : titre, progression vers
//  le niveau suivant. Ouvert au tap sur le chip niveau de la Home ; se
//  ferme au tap n'importe où (pas de cible à préserver, contrairement à
//  CoachMarkOverlay). Même patron d'intégration : la scène hôte route ses
//  touches via handleTouch(at:).
//

import SpriteKit

final class LevelProgressOverlay: SKNode {

    private static let scrimZ: CGFloat = 90

    @discardableResult
    static func present(in scene: SKScene) -> LevelProgressOverlay {
        let overlay = LevelProgressOverlay()
        overlay.build(in: scene)
        scene.addChild(overlay)
        return overlay
    }

    /// À appeler depuis touchesBegan de la scène hôte. Ferme toujours
    /// l'overlay, quel que soit l'endroit touché.
    func handleTouch(at point: CGPoint) {
        removeFromParent()
    }

    // MARK: - Construction

    private func build(in scene: SKScene) {
        zPosition = Self.scrimZ

        let scrim = SKShapeNode(rectOf: CGSize(width: scene.size.width * 2, height: scene.size.height * 2))
        scrim.fillColor = UIColor(white: 0, alpha: 0.45)
        scrim.strokeColor = .clear
        scrim.alpha = 0
        addChild(scrim)
        scrim.run(SKAction.fadeIn(withDuration: 0.2))

        let card = SKNode()
        card.zPosition = 1
        addChild(card)

        let cardW: CGFloat = 300
        let cardH: CGFloat = 190
        let bg = SKShapeNode(rectOf: CGSize(width: cardW, height: cardH), cornerRadius: 26)
        bg.fillColor = UIColor(red: 0.97, green: 0.95, blue: 0.92, alpha: 1)
        bg.strokeColor = UIColor(white: 0.72, alpha: 0.30)
        bg.lineWidth = 1
        card.addChild(bg)

        let level = LevelManager.shared.level
        let levelLabel = SKLabelNode(text: String(format: String(localized: "level.chip.label"), level))
        levelLabel.fontName = "AvenirNext-Heavy"
        levelLabel.fontSize = 28
        levelLabel.fontColor = UIColor(white: 0.24, alpha: 1)
        levelLabel.verticalAlignmentMode = .center
        levelLabel.position = CGPoint(x: 0, y: 62)
        card.addChild(levelLabel)

        let titleLabel = SKLabelNode(text: LevelManager.shared.currentTitle)
        titleLabel.fontName = "AvenirNext-Medium"
        titleLabel.fontSize = 18
        titleLabel.fontColor = UIColor(white: 0.42, alpha: 1)
        titleLabel.verticalAlignmentMode = .center
        titleLabel.position = CGPoint(x: 0, y: 30)
        card.addChild(titleLabel)

        let intoLevel = LevelManager.shared.xpIntoCurrentLevel
        let forNext = LevelManager.shared.xpForNextLevel
        let progressText = forNext > 0
            ? String(format: String(localized: "level.overlay.xp_progress"), intoLevel, forNext)
            : String(localized: "level.title.ten_master")
        let progressLabel = SKLabelNode(text: progressText)
        progressLabel.fontName = "AvenirNext-Medium"
        progressLabel.fontSize = 15
        progressLabel.fontColor = UIColor(white: 0.48, alpha: 1)
        progressLabel.verticalAlignmentMode = .center
        progressLabel.position = CGPoint(x: 0, y: -10)
        card.addChild(progressLabel)

        // Barre de progression vers le niveau suivant.
        let barW: CGFloat = 220
        let barH: CGFloat = 10
        let track = SKShapeNode(rectOf: CGSize(width: barW, height: barH), cornerRadius: barH / 2)
        track.fillColor = UIColor(white: 0.85, alpha: 1)
        track.strokeColor = .clear
        track.position = CGPoint(x: 0, y: -46)
        card.addChild(track)

        let ratio: CGFloat = forNext > 0 ? min(1, max(0, CGFloat(intoLevel) / CGFloat(forNext))) : 1
        let fillW = max(barH, barW * ratio)
        let fill = SKShapeNode(rectOf: CGSize(width: fillW, height: barH), cornerRadius: barH / 2)
        fill.fillColor = ThemeManager.shared.active.accent
        fill.strokeColor = .clear
        fill.position = CGPoint(x: -barW / 2 + fillW / 2, y: -46)
        card.addChild(fill)

        card.setScale(0.9)
        card.alpha = 0
        card.run(SKAction.group([
            SKAction.fadeIn(withDuration: 0.18),
            SKAction.scale(to: 1.0, duration: 0.18)
        ]))
    }
}
