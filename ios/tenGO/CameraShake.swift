//
//  CameraShake.swift
//  tenGO
//
//  Secousse de caméra amortie.
//
//  La caméra ne déplace QUE le rendu : les touches sont lues en coordonnées
//  scène (`touch.location(in: self)`), donc la zone tactile ne bouge jamais.
//

import SpriteKit

enum CameraShake {

    /// Amplitude maximale tolérée — au-delà, le plateau devient illisible
    /// et la secousse gêne la lecture des chiffres.
    static let maxAmplitude: CGFloat = 10

    static func shake(_ camera: SKCameraNode,
                      amplitude: CGFloat,
                      duration: TimeInterval) {
        guard JuiceSettings.motionEnabled else { return }
        let amp = min(amplitude, maxAmplitude)
        guard amp > 0, duration > 0 else { return }

        // Toujours repartir du centre : sans ça, deux secousses qui se
        // chevauchent laissent une dérive permanente du cadrage.
        camera.removeAction(forKey: "shake")
        camera.position = .zero

        let wobble = SKAction.customAction(withDuration: duration) { node, elapsed in
            let progress = 1 - (elapsed / CGFloat(duration))
            let damped = amp * max(0, progress)
            guard damped > 0.01 else {
                node.position = .zero
                return
            }
            node.position = CGPoint(x: CGFloat.random(in: -damped...damped),
                                    y: CGFloat.random(in: -damped...damped))
        }
        let settle = SKAction.run { camera.position = .zero }
        camera.run(SKAction.sequence([wobble, settle]), withKey: "shake")
    }

    /// Zoom avant bref puis retour. Contrairement à la secousse, le punch reste
    /// parfaitement lisible : rien ne tremble, l'image « avance » d'un cran.
    static func punch(_ camera: SKCameraNode, scale: CGFloat) {
        guard JuiceSettings.motionEnabled, scale < 1 else { return }
        camera.removeAction(forKey: "punch")

        let zoomIn = SKAction.scale(to: scale, duration: 0.045)
        zoomIn.timingMode = .easeOut
        let back = SKAction.scale(to: 1.0, duration: 0.20)
        back.timingMode = .easeOut
        camera.run(SKAction.sequence([
            zoomIn, back,
            // Remise stricte : une caméra qui dérive d'échelle recadrerait
            // tout le plateau de partie en partie.
            SKAction.run { camera.setScale(1.0) }
        ]), withKey: "punch")
    }

    static func reset(_ camera: SKCameraNode) {
        camera.removeAction(forKey: "shake")
        camera.removeAction(forKey: "punch")
        camera.position = .zero
        camera.setScale(1.0)
    }
}
