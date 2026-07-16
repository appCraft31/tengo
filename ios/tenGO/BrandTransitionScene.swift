//
//  BrandTransitionScene.swift
//  tenGO
//
//  Écran de marque animé (logo TEN•GO « pop + flash ») destiné à servir de
//  transition vidéo entre une séquence UGC et la séquence gameplay dans les
//  publicités. Rendu identique au jeu (police, point coloré, fond à bulles)
//  pour une cohérence de marque parfaite. Activé via l'env BRAND_MODE=1.
//

import SpriteKit

final class BrandTransitionScene: SKScene {

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        let theme = ThemeManager.shared.active
        backgroundColor = theme.background

        // Fond de marque (bulles pastel flottantes) — identique au jeu.
        addChild(ThemeBackground.make(for: theme, size: size))

        let logo = makeLogo(theme: theme)
        logo.zPosition = 10
        addChild(logo)

        // Voile blanc plein écran pour le flash.
        let flash = SKShapeNode(rectOf: CGSize(width: size.width * 2, height: size.height * 2))
        flash.fillColor = .white
        flash.strokeColor = .clear
        flash.alpha = 0
        flash.zPosition = 50
        addChild(flash)

        animate(logo: logo, flash: flash)
    }

    // MARK: - Logo (recette identique au menu)

    private func makeLogo(theme: Theme) -> SKNode {
        let container = SKNode()

        let ten = SKLabelNode(text: "TEN")
        ten.fontName = "AvenirNext-Heavy"
        ten.fontSize = 72
        ten.fontColor = theme.logo
        ten.verticalAlignmentMode = .center
        ten.horizontalAlignmentMode = .right
        ten.position = CGPoint(x: -18, y: 0)
        container.addChild(ten)

        // Point coloré central (déterministe : rose, index 6 — comme le menu).
        let dot = SKShapeNode(circleOfRadius: 14)
        dot.fillColor = theme.color(forValue: 7)   // rose pastel
        dot.strokeColor = .clear
        dot.position = CGPoint(x: 0, y: 4)
        dot.zPosition = 1
        dot.name = "dot"
        container.addChild(dot)

        let go = SKLabelNode(text: "GO")
        go.fontName = "AvenirNext-Heavy"
        go.fontSize = 72
        go.fontColor = theme.logo
        go.verticalAlignmentMode = .center
        go.horizontalAlignmentMode = .left
        go.position = CGPoint(x: 18, y: 0)
        container.addChild(go)

        return container
    }

    // MARK: - Animation « pop + flash »
    //
    // L'animation est bouclée : la capture vidéo peut démarrer à n'importe quel
    // moment et tomber sur un cycle propre (~1,75 s). Le montage isole un cycle.

    private func animate(logo: SKNode, flash: SKNode) {
        let cycle = SKAction.sequence([
            SKAction.run { [weak self] in self?.resetLogo(logo) },
            SKAction.wait(forDuration: 0.15),
            SKAction.run { [weak self] in self?.playPop(logo: logo, flash: flash) },
            SKAction.wait(forDuration: 1.60)   // hold + respiration entre deux cycles
        ])
        run(SKAction.repeatForever(cycle), withKey: "brandLoop")
    }

    private func resetLogo(_ logo: SKNode) {
        logo.removeAllActions()
        logo.setScale(0.2)
        logo.alpha = 0
        logo.childNode(withName: "dot")?.setScale(1.0)
    }

    private func playPop(logo: SKNode, flash: SKNode) {
        // Pop : surgit avec rebond.
        let scaleUp = SKAction.scale(to: 1.18, duration: 0.18); scaleUp.timingMode = .easeOut
        let settle = SKAction.scale(to: 1.0, duration: 0.12); settle.timingMode = .easeInEaseOut
        logo.run(SKAction.group([
            SKAction.fadeIn(withDuration: 0.10),
            SKAction.sequence([scaleUp, settle])
        ]))

        // Flash blanc synchronisé sur le pop.
        flash.run(SKAction.sequence([
            SKAction.fadeAlpha(to: 0.85, duration: 0.05),
            SKAction.fadeAlpha(to: 0.0, duration: 0.28)
        ]))

        // Le point coloré pulse juste après — éclat d'énergie.
        logo.childNode(withName: "dot")?.run(SKAction.sequence([
            SKAction.wait(forDuration: 0.18),
            SKAction.scale(to: 1.4, duration: 0.12),
            SKAction.scale(to: 1.0, duration: 0.18)
        ]))
    }
}
