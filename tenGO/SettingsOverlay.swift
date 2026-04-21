//
//  SettingsOverlay.swift
//  tenGO
//
//  Popup de paramètres réutilisable (menu + écran de jeu).
//  Contient actuellement le toggle son, extensible à d'autres options.
//

import SpriteKit
import UIKit

final class SettingsOverlay: SKNode {

    private let sceneSize: CGSize
    private var toggleBg: SKShapeNode!
    private var toggleKnob: SKShapeNode!
    private var toggleLabel: SKLabelNode!
    private var dimNode: SKSpriteNode!
    private var card: SKNode!

    init(sceneSize: CGSize) {
        self.sceneSize = sceneSize
        super.init()
        zPosition = 100
        buildUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    // MARK: - API publique

    /// Présente avec fade-in
    func present(in parent: SKNode) {
        alpha = 0
        parent.addChild(self)
        run(SKAction.fadeIn(withDuration: 0.18))
        card.setScale(0.92)
        card.run(SKAction.scale(to: 1.0, duration: 0.22))
    }

    /// Renvoie true si le touch est consommé
    @discardableResult
    func handleTouch(at scenePoint: CGPoint) -> Bool {
        let localPoint = convert(scenePoint, from: parent!)
        for node in nodes(at: localPoint) {
            switch node.name {
            case "closeBtn", "closeBg":
                dismiss()
                return true
            case "soundToggle", "toggleBg", "toggleKnob":
                toggleSound()
                return true
            default: continue
            }
        }
        // Tap sur le dim (hors carte) ferme aussi
        if dimNode.contains(localPoint) {
            dismiss()
        }
        return true
    }

    func dismiss() {
        run(SKAction.sequence([
            SKAction.fadeOut(withDuration: 0.15),
            SKAction.removeFromParent()
        ]))
    }

    // MARK: - Construction

    private func buildUI() {
        // Fond assombrissant plein écran (×2 pour couvrir aspectFill)
        dimNode = SKSpriteNode(color: UIColor(white: 0, alpha: 0.55),
                               size: CGSize(width: sceneSize.width * 2.5,
                                            height: sceneSize.height * 2.5))
        dimNode.zPosition = 0
        addChild(dimNode)

        // Carte centrale
        card = SKNode()
        card.zPosition = 1
        addChild(card)

        let cardW: CGFloat = 460
        let cardH: CGFloat = 340
        let bg = SKShapeNode(rectOf: CGSize(width: cardW, height: cardH), cornerRadius: 28)
        bg.fillColor = UIColor(red: 0.98, green: 0.96, blue: 0.93, alpha: 1)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.25)
        bg.lineWidth = 1
        card.addChild(bg)

        // Titre
        let title = SKLabelNode(text: "Paramètres")
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 30
        title.fontColor = UIColor(white: 0.25, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: cardH / 2 - 50)
        card.addChild(title)

        // Bouton fermer (×) en haut à droite
        let closeNode = SKNode()
        closeNode.name = "closeBtn"
        closeNode.position = CGPoint(x: cardW / 2 - 32, y: cardH / 2 - 32)
        card.addChild(closeNode)

        let closeBg = SKShapeNode(circleOfRadius: 18)
        closeBg.name = "closeBg"
        closeBg.fillColor = UIColor(white: 0.92, alpha: 1)
        closeBg.strokeColor = .clear
        closeNode.addChild(closeBg)

        let closeIcon = SKLabelNode(text: "×")
        closeIcon.name = "closeBtn"
        closeIcon.fontName = "AvenirNext-Medium"
        closeIcon.fontSize = 28
        closeIcon.fontColor = UIColor(white: 0.40, alpha: 1)
        closeIcon.verticalAlignmentMode = .center
        closeIcon.horizontalAlignmentMode = .center
        closeIcon.position = CGPoint(x: 0, y: 1)
        closeNode.addChild(closeIcon)

        // Ligne "Son"
        let rowY: CGFloat = 10

        let soundLabel = SKLabelNode(text: "Son")
        soundLabel.fontName = "AvenirNext-Medium"
        soundLabel.fontSize = 24
        soundLabel.fontColor = UIColor(white: 0.30, alpha: 1)
        soundLabel.verticalAlignmentMode = .center
        soundLabel.horizontalAlignmentMode = .left
        soundLabel.position = CGPoint(x: -cardW / 2 + 40, y: rowY)
        card.addChild(soundLabel)

        // Toggle pill
        let toggleContainer = SKNode()
        toggleContainer.name = "soundToggle"
        toggleContainer.position = CGPoint(x: cardW / 2 - 80, y: rowY)
        card.addChild(toggleContainer)

        toggleBg = SKShapeNode(rectOf: CGSize(width: 72, height: 36), cornerRadius: 18)
        toggleBg.name = "toggleBg"
        toggleBg.lineWidth = 0
        toggleContainer.addChild(toggleBg)

        toggleKnob = SKShapeNode(circleOfRadius: 14)
        toggleKnob.name = "toggleKnob"
        toggleKnob.fillColor = .white
        toggleKnob.strokeColor = UIColor(white: 0.85, alpha: 1)
        toggleKnob.lineWidth = 1
        toggleContainer.addChild(toggleKnob)

        // Label statut ("Activé" / "Muet") sous le toggle
        toggleLabel = SKLabelNode(text: "")
        toggleLabel.fontName = "AvenirNext-Regular"
        toggleLabel.fontSize = 13
        toggleLabel.fontColor = UIColor(white: 0.55, alpha: 1)
        toggleLabel.verticalAlignmentMode = .center
        toggleLabel.horizontalAlignmentMode = .center
        toggleLabel.position = CGPoint(x: cardW / 2 - 80, y: rowY - 32)
        card.addChild(toggleLabel)

        // Note de bas de carte
        let footer = SKLabelNode(text: "Version 1.0")
        footer.fontName = "AvenirNext-Regular"
        footer.fontSize = 13
        footer.fontColor = UIColor(white: 0.55, alpha: 1)
        footer.verticalAlignmentMode = .center
        footer.position = CGPoint(x: 0, y: -cardH / 2 + 30)
        card.addChild(footer)

        updateToggleVisual(animated: false)
    }

    // MARK: - Toggle son

    private func toggleSound() {
        SoundManager.shared.isMuted.toggle()
        updateToggleVisual(animated: true)
    }

    private func updateToggleVisual(animated: Bool) {
        let muted = SoundManager.shared.isMuted
        let bgColor = muted
            ? UIColor(white: 0.82, alpha: 1)
            : UIColor(red: 0.55, green: 0.82, blue: 0.65, alpha: 1)
        let knobX: CGFloat = muted ? -18 : 18
        let text = muted ? "Muet" : "Activé"

        if animated {
            toggleBg.run(SKAction.customAction(withDuration: 0.18) { [weak self] node, _ in
                (node as? SKShapeNode)?.fillColor = bgColor
                self?.toggleLabel.text = text
            })
            toggleKnob.run(SKAction.moveTo(x: knobX, duration: 0.18))
        } else {
            toggleBg.fillColor = bgColor
            toggleKnob.position = CGPoint(x: knobX, y: 0)
            toggleLabel.text = text
        }
    }
}
