//
//  WinCelebration.swift
//  tenGO
//
//  Célébration de la victoire (grille entièrement vidée).
//
//  Deux temps : une onde qui part du centre de la grille — elle dit
//  « le plateau est vide », qui est précisément le sens de la victoire ici —
//  puis une pluie de confettis.
//
//  Les confettis sont le SEUL SKEmitterNode du projet, et il est construit en
//  code (pas de fichier .sks) : 200 particules sur 1,5 s coûteraient autant de
//  nodes et d'actions en SKAction, alors que l'émetteur est simulé en C pour
//  ~1 draw call. Il est isolé en fin de partie, sans gameplay concurrent.
//

import SpriteKit

enum WinCelebration {

    static func make(center: CGPoint, accent: UIColor, sceneSize: CGSize) -> SKNode {
        let container = SKNode()
        container.zPosition = 30

        container.addChild(makeSweep(center: center, accent: accent))
        container.addChild(makeConfetti(accent: accent, sceneSize: sceneSize))

        // Filet de sécurité : la célébration se démonte seule même si la scène
        // reste affichée (retour au menu, restart immédiat…).
        container.run(SKAction.sequence([
            SKAction.wait(forDuration: 3.2),
            SKAction.removeFromParent()
        ]))
        return container
    }

    // MARK: - Onde de vidage

    private static func makeSweep(center: CGPoint, accent: UIColor) -> SKNode {
        let node = SKNode()
        for i in 0..<3 {
            let ring = SKSpriteNode(texture: FXTextures.ring)
            ring.size = CGSize(width: 120, height: 120)
            ring.position = center
            ring.color = accent
            ring.colorBlendFactor = 1
            ring.alpha = 0
            ring.setScale(0.4)
            node.addChild(ring)

            let delay = TimeInterval(i) * 0.14
            let expand = SKAction.scale(to: 9.0, duration: 0.75)
            expand.timingMode = .easeOut
            ring.run(SKAction.sequence([
                SKAction.wait(forDuration: delay),
                SKAction.fadeAlpha(to: 0.55, duration: 0.08),
                SKAction.group([expand, SKAction.fadeOut(withDuration: 0.75)]),
                SKAction.removeFromParent()
            ]))
        }
        return node
    }

    // MARK: - Confettis

    private static func makeConfetti(accent: UIColor, sceneSize: CGSize) -> SKNode {
        let emitter = SKEmitterNode()
        emitter.particleTexture = FXTextures.dot
        emitter.particleBirthRate = 180
        // Borné : l'émetteur s'éteint tout seul, aucune extinction à piloter.
        emitter.numParticlesToEmit = 220
        emitter.particleLifetime = 2.2
        emitter.particleLifetimeRange = 0.8

        // Émission sur toute la largeur, depuis le haut de l'écran.
        emitter.position = CGPoint(x: 0, y: sceneSize.height / 2 + 40)
        emitter.particlePositionRange = CGVector(dx: sceneSize.width, dy: 10)

        emitter.emissionAngle = -.pi / 2
        emitter.emissionAngleRange = .pi / 6
        emitter.particleSpeed = 260
        emitter.particleSpeedRange = 120
        emitter.yAcceleration = -320

        emitter.particleScale = 0.32
        emitter.particleScaleRange = 0.18
        emitter.particleScaleSpeed = -0.06

        emitter.particleAlpha = 0.95
        emitter.particleAlphaSpeed = -0.35

        emitter.particleRotationRange = .pi * 2
        emitter.particleRotationSpeed = 3.0

        // Teintes dérivées de l'accent du thème actif, pour rester cohérent
        // avec la palette choisie par le joueur.
        emitter.particleColor = accent
        emitter.particleColorBlendFactor = 1
        emitter.particleColorBlendFactorRange = 0.4
        emitter.particleColorSequence = nil
        emitter.particleBlendMode = .alpha

        emitter.zPosition = 1
        emitter.targetNode = nil

        emitter.run(SKAction.sequence([
            SKAction.wait(forDuration: 3.0),
            SKAction.removeFromParent()
        ]))
        return emitter
    }
}
