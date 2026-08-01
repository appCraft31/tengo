//
//  JuiceDirector.swift
//  tenGO
//
//  Point d'entrée unique du « juice ». La scène ne connaît que des verbes de
//  jeu (onSelect, onCommit, onWin…) ; les durées, seuils, couleurs et le
//  gating accessibilité vivent ici.
//
//  RÈGLE D'OR — aucun effet déclenché depuis ce fichier ne touche à
//  `isAnimating`, `gridModel`, `bubbleNodes` ou `currentPath`, et aucun n'est
//  attendu par le chemin critique (le wait(0.26) de commitPath et le
//  maxDuration+0.05 de afterPop restent maîtres). Si les timings de jeu
//  diffèrent entre juice activé et désactivé, c'est un bug.
//

import SpriteKit
import QuartzCore
import UIKit

/// Palier d'intensité d'une validation, dérivé de la longueur de la chaîne.
enum PopTier {
    case small   // 2-3 bulles
    case medium  // 4-5
    case large   // 6+

    init(length: Int) {
        switch length {
        case ...3: self = .small
        case 4...5: self = .medium
        default: self = .large
        }
    }

    /// Monte d'un cran — utilisé quand le joueur bat son record de chaîne
    /// dans la partie en cours (sans que le score, lui, ne change).
    var boosted: PopTier {
        switch self {
        case .small: return .medium
        case .medium, .large: return .large
        }
    }

    /// ⚠️ Calibrage : le palier `small` couvre les chaînes de 2-3 bulles, qui
    /// sont l'immense majorité des coups. Il doit rester plus riche que l'ancien
    /// rendu uniforme (12 éclats pour une paire, 14 pour un triplet), sinon
    /// l'escalade appauvrit le quotidien pour n'enrichir que l'exceptionnel.
    var burstCount: Int {
        switch self {
        case .small: return 16
        case .medium: return 24
        case .large: return 34
        }
    }

    var ringCount: Int {
        switch self {
        case .small: return 0
        case .medium: return 1
        case .large: return 2
        }
    }

    var shakeAmplitude: CGFloat {
        switch self {
        case .small: return 0
        case .medium: return 4
        case .large: return 9
        }
    }

    var shakeDuration: TimeInterval {
        switch self {
        case .small: return 0
        case .medium: return 0.14
        case .large: return 0.24
        }
    }

    /// Zoom avant bref de la caméra. Beaucoup plus lisible qu'une translation,
    /// et sans aucun risque sur les zones tactiles — d'où sa présence dès le
    /// palier `small`, pour que CHAQUE coup ait un impact.
    var cameraPunch: CGFloat {
        switch self {
        case .small: return 0.995
        case .medium: return 0.986
        case .large: return 0.974
        }
    }

    /// Suspension brève de l'action à l'impact. C'est le procédé le plus
    /// efficace pour donner du poids à un coup — et le seul de ce fichier qui
    /// touche au temps du jeu (il retarde la gravité d'autant). Arbitrage
    /// assumé, réservé aux chaînes de 4+.
    var hitStop: TimeInterval {
        switch self {
        case .small: return 0
        case .medium: return 0.035
        case .large: return 0.060
        }
    }

    var flashAlpha: CGFloat {
        switch self {
        case .small, .medium: return 0
        case .large: return 0.18
        }
    }

    /// Poussée appliquée aux bulles voisines de la chaîne.
    var neighborPush: CGFloat {
        switch self {
        case .small: return 0
        case .medium: return 6
        case .large: return 11
        }
    }
}

final class JuiceDirector {

    private unowned let scene: SKScene
    private let fxLayer: SKNode
    private weak var shakeCamera: SKCameraNode?

    /// La scène décide si un effet plein écran est acceptable : le panel de fin
    /// et l'overlay de réglages sont enfants de la scène et bougeraient avec elle.
    var allowsScreenEffects: () -> Bool = { true }

    // Anti-répétition du retour de refus : `walkPath` boucle et peut heurter
    // la même cellule plusieurs fois dans la même frame.
    private var lastRejectedCell: (row: Int, col: Int)?
    private var lastRejectedAt: TimeInterval = 0
    private static let rejectThrottle: TimeInterval = 0.12

    init(scene: SKScene, fxLayer: SKNode, camera: SKCameraNode?) {
        self.scene = scene
        self.fxLayer = fxLayer
        self.shakeCamera = camera
    }

    // MARK: - Tracé

    /// Première bulle d'un chemin.
    func onSelect() {
        HapticManager.impact(.soft, intensity: 0.35)
    }

    /// Bulle ajoutée au chemin. `tension` ∈ [0,1] = somme courante / 10.
    /// L'haptique est déclenchée ici et non depuis SoundManager : elle doit
    /// être immédiate (pas de passage par l'arpégiateur) et survivre à la
    /// coupure du son.
    func onAppend(tension: CGFloat) {
        HapticManager.impact(.soft, intensity: 0.35 + 0.5 * max(0, min(1, tension)))
    }

    /// Bulle refusée parce qu'elle ferait dépasser 10.
    /// Ne révèle aucun chiffre : dit seulement « pas celle-là ».
    func onRejected(node: SKNode, at cell: (row: Int, col: Int)) {
        let now = CACurrentMediaTime()
        if let last = lastRejectedCell,
           last == cell,
           now - lastRejectedAt < Self.rejectThrottle {
            return
        }
        lastRejectedCell = cell
        lastRejectedAt = now

        SoundManager.shared.playRejected()

        guard JuiceSettings.motionEnabled else { return }
        guard node.action(forKey: "reject") == nil else { return }
        let nudge = SKAction.sequence([
            SKAction.moveBy(x: 3, y: 0, duration: 0.04),
            SKAction.moveBy(x: -6, y: 0, duration: 0.04),
            SKAction.moveBy(x: 3, y: 0, duration: 0.04)
        ])
        node.run(nudge, withKey: "reject")
    }

    // MARK: - Validation

    /// Appelé au moment où une chaîne est validée, en parallèle du chemin
    /// critique — cette méthode ne doit jamais être attendue.
    func onCommit(tier: PopTier, length: Int) {
        HapticManager.chain(length: length)

        guard JuiceSettings.motionEnabled, allowsScreenEffects() else { return }

        if let camera = shakeCamera {
            CameraShake.punch(camera, scale: tier.cameraPunch)
            if tier.shakeAmplitude > 0 {
                CameraShake.shake(camera,
                                  amplitude: tier.shakeAmplitude,
                                  duration: tier.shakeDuration)
            }
        }
        if tier.flashAlpha > 0 {
            flashScreen(alpha: tier.flashAlpha, duration: 0.06)
        }
        if tier.hitStop > 0 {
            hitStop(duration: tier.hitStop)
        }
    }

    // MARK: - Hit-stop

    /// Jeton d'invalidation : deux chaînes rapprochées ne doivent pas voir la
    /// première restaurer la vitesse pendant que la seconde ralentit encore.
    private var hitStopToken = 0

    private func hitStop(duration: TimeInterval) {
        hitStopToken += 1
        let token = hitStopToken
        scene.speed = 0.04
        // Restauration en temps RÉEL : un SKAction serait lui-même ralenti par
        // le hit-stop et ne rendrait jamais la main.
        DispatchQueue.main.asyncAfter(deadline: .now() + duration) { [weak self] in
            guard let self = self, self.hitStopToken == token else { return }
            self.scene.speed = 1.0
        }
    }

    /// Filet de sécurité : à appeler quand la scène change d'état (fin de
    /// partie, retour au menu) pour ne jamais laisser la scène au ralenti.
    func resetTimeScale() {
        hitStopToken += 1
        scene.speed = 1.0
    }

    // MARK: - Fin de partie

    func onWin(at center: CGPoint, accent: UIColor) {
        HapticManager.success()
        guard JuiceSettings.motionEnabled else { return }
        fxLayer.addChild(WinCelebration.make(center: center,
                                             accent: accent,
                                             sceneSize: scene.size))
    }

    func onLose() {
        HapticManager.warning()
    }

    // MARK: - Effets plein écran

    private func flashScreen(alpha: CGFloat, duration: TimeInterval) {
        let flash = SKSpriteNode(color: .white,
                                 size: CGSize(width: scene.size.width * 1.4,
                                              height: scene.size.height * 1.4))
        flash.alpha = 0
        flash.zPosition = 40
        flash.blendMode = .add
        fxLayer.addChild(flash)
        flash.run(SKAction.sequence([
            SKAction.fadeAlpha(to: alpha, duration: duration),
            SKAction.fadeOut(withDuration: duration * 2),
            SKAction.removeFromParent()
        ]))
    }
}
