//
//  CoachMarkOverlay.swift
//  tenGO
//
//  Coach-mark réutilisable : voile sombre, cible mise en avant (remontée
//  au-dessus du voile) avec halo pulsant et bulle de texte. La scène hôte
//  route ses touches via handleTouch(at:) — même patron que SettingsOverlay.
//

import SpriteKit

final class CoachMarkOverlay: SKNode {

    /// `ignored` : le tap est tombé hors cible alors que l'overlay refuse de se
    /// fermer (`blocksOutsideTaps`). L'overlay reste affiché et la scène hôte ne
    /// doit rien faire d'autre de ce tap.
    enum TouchResult { case target, dismissed, ignored }

    /// Quand `true`, seul un tap sur la cible ferme l'overlay : ailleurs, la
    /// cible pulse pour réorienter le joueur. Sert au tutoriel d'achat, qui
    /// insiste sans pour autant enfermer le joueur (une sortie explicite reste
    /// offerte par la scène hôte).
    var blocksOutsideTaps = false

    /// Appelé après la fermeture, avec la raison.
    var onDismiss: ((TouchResult) -> Void)?

    private weak var targetNode: SKNode?
    private weak var haloNode: SKNode?
    private var savedTargetZPosition: CGFloat = 0
    /// Cadre de la cible en coordonnées scène (la cible peut vivre dans un
    /// nœud défilant : son frame parent ne suffit pas).
    private var targetSceneFrame: CGRect = .zero

    private static let scrimZ: CGFloat = 90
    private static let targetZ: CGFloat = 95

    /// Attache l'overlay à la scène et met `target` (enfant direct de la scène)
    /// en avant. `text` s'affiche dans une bulle au-dessus ou en dessous.
    @discardableResult
    static func present(in scene: SKScene, over target: SKNode, text: String) -> CoachMarkOverlay {
        let overlay = CoachMarkOverlay()
        overlay.build(in: scene, over: target, text: text)
        scene.addChild(overlay)
        return overlay
    }

    /// À appeler depuis touchesBegan de la scène hôte. Ferme l'overlay et dit
    /// si la cible a été touchée. Le tap hors cible est rendu à la scène.
    func handleTouch(at point: CGPoint) -> TouchResult {
        let hit = targetSceneFrame.contains(point)
        if !hit && blocksOutsideTaps {
            pulseTarget()
            return .ignored
        }
        let result: TouchResult = hit ? .target : .dismissed
        dismiss()
        onDismiss?(result)
        return result
    }

    /// Rappel visuel quand le joueur tape à côté d'un coach-mark insistant.
    private func pulseTarget() {
        guard let halo = haloNode else { return }
        halo.removeAction(forKey: "nudge")
        halo.run(SKAction.sequence([
            SKAction.scale(to: 1.18, duration: 0.10),
            SKAction.scale(to: 1.0, duration: 0.16)
        ]), withKey: "nudge")
    }

    /// Restaure la zPosition de la cible et retire l'overlay.
    func dismiss() {
        targetNode?.zPosition = savedTargetZPosition
        removeFromParent()
    }

    // MARK: - Construction

    private func build(in scene: SKScene, over target: SKNode, text: String) {
        targetNode = target
        savedTargetZPosition = target.zPosition
        target.zPosition = Self.targetZ
        zPosition = Self.scrimZ

        // Frame en coordonnées scène (translation pure : pas de scale/rotation
        // dans nos hiérarchies de menu/boutique).
        var frame = target.calculateAccumulatedFrame()
        if let parent = target.parent, parent !== scene {
            let offset = parent.convert(CGPoint.zero, to: scene)
            frame = frame.offsetBy(dx: offset.x, dy: offset.y)
        }
        targetSceneFrame = frame

        // Voile sombre plein écran (marges larges : aspectFill rogne les bords).
        let scrim = SKShapeNode(rectOf: CGSize(width: scene.size.width * 2,
                                               height: scene.size.height * 2))
        scrim.fillColor = UIColor(white: 0, alpha: 0.45)
        scrim.strokeColor = .clear
        addChild(scrim)
        scrim.alpha = 0
        scrim.run(SKAction.fadeIn(withDuration: 0.2))

        // Halo pulsant autour de la cible.
        let pad: CGFloat = 14
        let haloSize = CGSize(width: frame.width + pad * 2, height: frame.height + pad * 2)
        let halo = SKShapeNode(rectOf: haloSize, cornerRadius: min(haloSize.height, haloSize.width) / 2)
        halo.fillColor = UIColor(white: 1, alpha: 0.10)
        halo.strokeColor = UIColor(white: 1, alpha: 0.9)
        halo.lineWidth = 3
        halo.position = CGPoint(x: frame.midX, y: frame.midY)
        halo.zPosition = 1
        addChild(halo)
        haloNode = halo
        halo.run(SKAction.repeatForever(SKAction.sequence([
            SKAction.scale(to: 1.06, duration: 0.55),
            SKAction.scale(to: 1.0, duration: 0.55)
        ])))

        // Bulle de texte — au-dessus de la cible si elle est dans la moitié
        // basse de la scène, sinon en dessous.
        let above = frame.midY <= 0
        let label = SKLabelNode(text: text)
        label.fontName = "AvenirNext-DemiBold"
        label.fontSize = 19
        label.fontColor = UIColor(white: 0.25, alpha: 1)
        label.numberOfLines = 0
        label.preferredMaxLayoutWidth = 250
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .center

        let padX: CGFloat = 22, padY: CGFloat = 16
        let bubbleSize = CGSize(width: label.frame.width + padX * 2,
                                height: label.frame.height + padY * 2)
        let bubble = SKShapeNode(rectOf: bubbleSize, cornerRadius: 18)
        bubble.fillColor = UIColor(red: 0.99, green: 0.97, blue: 0.94, alpha: 1)
        bubble.strokeColor = UIColor(white: 0.75, alpha: 0.5)
        bubble.lineWidth = 1
        let gap: CGFloat = 34
        let bubbleY = above
            ? frame.maxY + pad + gap + bubbleSize.height / 2
            : frame.minY - pad - gap - bubbleSize.height / 2
        bubble.position = CGPoint(x: 0, y: bubbleY)
        bubble.zPosition = 2
        addChild(bubble)
        bubble.addChild(label)

        // Petite flèche entre la bulle et la cible.
        let arrow = SKShapeNode(path: {
            let p = UIBezierPath()
            p.move(to: CGPoint(x: -10, y: 0))
            p.addLine(to: CGPoint(x: 10, y: 0))
            p.addLine(to: CGPoint(x: 0, y: above ? -14 : 14))
            p.close()
            return p.cgPath
        }())
        arrow.fillColor = bubble.fillColor
        arrow.strokeColor = .clear
        arrow.position = CGPoint(
            x: frame.midX,
            y: above ? bubbleY - bubbleSize.height / 2 - 2 : bubbleY + bubbleSize.height / 2 + 2)
        arrow.zPosition = 2
        addChild(arrow)

        // Entrée douce du contenu.
        for node in [halo, bubble, arrow] {
            node.alpha = 0
            node.run(SKAction.sequence([
                SKAction.wait(forDuration: 0.1),
                SKAction.fadeIn(withDuration: 0.25)
            ]))
        }
    }
}
