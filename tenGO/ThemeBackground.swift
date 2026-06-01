//
//  ThemeBackground.swift
//  tenGO
//
//  Ambiance de fond animée propre à chaque thème (vagues, étoiles, feuilles…).
//  100 % vectoriel/SpriteKit, sans asset. Subtil et placé derrière le jeu (zPosition -1).
//

import SpriteKit

enum ThemeEffect {
    case bubbles   // pastel par défaut
    case waves     // océan
    case stars     // espace
    case leaves    // forêt
    case sand      // désert
    case moon      // nuit
    case candy     // bonbon
}

extension Theme {
    var effect: ThemeEffect {
        switch id {
        case "ocean":  return .waves
        case "space":  return .stars
        case "forest": return .leaves
        case "desert": return .sand
        case "night":  return .moon
        case "candy":  return .candy
        default:       return .bubbles
        }
    }
}

enum ThemeBackground {

    /// Construit le nœud d'ambiance du thème, à ajouter à la scène (déjà en zPosition -1).
    static func make(for theme: Theme, size: CGSize) -> SKNode {
        let node = SKNode()
        node.zPosition = -1
        switch theme.effect {
        case .bubbles: buildBubbles(node, size, theme)
        case .waves:   buildWaves(node, size, theme)
        case .stars:   buildStars(node, size, theme, count: 70, withMoon: false)
        case .leaves:  buildLeaves(node, size, theme)
        case .sand:    buildSand(node, size, theme)
        case .moon:    buildStars(node, size, theme, count: 45, withMoon: true)
        case .candy:   buildCandy(node, size, theme)
        }
        return node
    }

    // MARK: - Bulles (défaut)

    private static func buildBubbles(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        let configs: [(r: CGFloat, x: CGFloat, y: CGFloat, c: Int, d: Double)] = [
            (88, -280, 480, 0, 7.2), (62, 260, 350, 4, 9.5), (110, -180, -150, 6, 11.0),
            (74, 310, -320, 1, 8.3), (96, -60, 560, 3, 10.1), (54, 200, -500, 7, 6.8),
            (80, -320, -480, 2, 12.4), (68, 80, 200, 5, 9.0), (50, -200, -30, 8, 7.6),
        ]
        for cfg in configs {
            let bubble = SKShapeNode(circleOfRadius: cfg.r)
            bubble.fillColor = theme.bubbles[cfg.c].withAlphaComponent(0.18)
            bubble.strokeColor = .clear
            bubble.position = CGPoint(x: cfg.x, y: cfg.y)
            node.addChild(bubble)
            let up = SKAction.moveBy(x: 0, y: 18, duration: cfg.d)
            let down = SKAction.moveBy(x: 0, y: -18, duration: cfg.d)
            up.timingMode = .easeInEaseOut
            down.timingMode = .easeInEaseOut
            bubble.run(.repeatForever(.sequence([up, down])))
        }
    }

    // MARK: - Vagues (océan)

    private static func buildWaves(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        let layers: [(y: CGFloat, amp: CGFloat, len: CGFloat, c: Int, alpha: CGFloat, speed: Double)] = [
            (size.height * 0.34, 16, 240, 8, 0.16, 11),
            (size.height * 0.10, 22, 300, 5, 0.18, 14),
            (-size.height * 0.12, 18, 200, 2, 0.16, 9),
            (-size.height * 0.34, 26, 340, 6, 0.20, 16),
        ]
        for layer in layers {
            let wave = makeSineNode(width: size.width * 2.4, amplitude: layer.amp,
                                    wavelength: layer.len, color: theme.bubbles[layer.c].withAlphaComponent(layer.alpha))
            wave.position = CGPoint(x: 0, y: layer.y)
            node.addChild(wave)
            // Défilement horizontal continu (période = longueur d'onde → raccord invisible)
            let scroll = SKAction.sequence([
                SKAction.moveBy(x: -layer.len, y: 0, duration: layer.speed),
                SKAction.moveBy(x: layer.len, y: 0, duration: 0),
            ])
            wave.run(.repeatForever(scroll))
            // Léger bercement vertical
            let bobUp = SKAction.moveBy(x: 0, y: 8, duration: layer.speed * 0.5)
            let bobDown = SKAction.moveBy(x: 0, y: -8, duration: layer.speed * 0.5)
            bobUp.timingMode = .easeInEaseOut
            bobDown.timingMode = .easeInEaseOut
            wave.run(.repeatForever(.sequence([bobUp, bobDown])))
        }
    }

    private static func makeSineNode(width: CGFloat, amplitude: CGFloat, wavelength: CGFloat, color: UIColor) -> SKShapeNode {
        let path = CGMutablePath()
        let step: CGFloat = 8
        var x = -width / 2
        path.move(to: CGPoint(x: x, y: sin(x / wavelength * 2 * .pi) * amplitude))
        while x <= width / 2 {
            let y = sin(x / wavelength * 2 * .pi) * amplitude
            path.addLine(to: CGPoint(x: x, y: y))
            x += step
        }
        let wave = SKShapeNode(path: path)
        wave.strokeColor = color
        wave.lineWidth = 4
        wave.lineCap = .round
        wave.fillColor = .clear
        return wave
    }

    // MARK: - Étoiles (espace / nuit)

    private static func buildStars(_ node: SKNode, _ size: CGSize, _ theme: Theme, count: Int, withMoon: Bool) {
        let w = size.width, h = size.height
        for _ in 0..<count {
            let r = CGFloat.random(in: 0.8...2.4)
            let star = SKShapeNode(circleOfRadius: r)
            // Surtout blanc, parfois une teinte du thème
            star.fillColor = Bool.random() && Bool.random()
                ? theme.bubbles[Int.random(in: 0..<theme.bubbles.count)].withAlphaComponent(0.9)
                : UIColor(white: 1, alpha: 0.95)
            star.strokeColor = .clear
            star.position = CGPoint(x: .random(in: -w / 2...w / 2), y: .random(in: -h / 2...h / 2))
            star.alpha = .random(in: 0.3...0.9)
            node.addChild(star)
            // Scintillement
            let lo = CGFloat.random(in: 0.2...0.45)
            let hi = CGFloat.random(in: 0.8...1.0)
            let dur = Double.random(in: 0.8...2.6)
            star.run(.repeatForever(.sequence([
                .fadeAlpha(to: lo, duration: dur),
                .fadeAlpha(to: hi, duration: dur),
            ])), withKey: "twinkle")
            star.run(.wait(forDuration: .random(in: 0...2)))
        }
        if withMoon { addMoon(node, size, theme) }
    }

    private static func addMoon(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        let moonR: CGFloat = 38
        let cx = size.width * 0.26, cy = size.height * 0.30
        // Disque lunaire
        let moon = SKShapeNode(circleOfRadius: moonR)
        moon.fillColor = UIColor(white: 0.95, alpha: 0.92)
        moon.strokeColor = .clear
        moon.position = CGPoint(x: cx, y: cy)
        node.addChild(moon)
        // Halo doux
        let halo = SKShapeNode(circleOfRadius: moonR + 14)
        halo.fillColor = UIColor(white: 1, alpha: 0.08)
        halo.strokeColor = .clear
        halo.position = CGPoint(x: cx, y: cy)
        halo.zPosition = -1
        node.addChild(halo)
        // Croissant : disque couleur du fond décalé qui "creuse" la lune
        let carve = SKShapeNode(circleOfRadius: moonR)
        carve.fillColor = theme.background
        carve.strokeColor = .clear
        carve.position = CGPoint(x: cx + 18, y: cy + 10)
        node.addChild(carve)
    }

    // MARK: - Feuilles (forêt)

    private static func buildLeaves(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        let w = size.width, h = size.height
        let count = 14
        for i in 0..<count {
            let leaf = SKShapeNode(ellipseOf: CGSize(width: 16, height: 9))
            leaf.fillColor = theme.bubbles[i % theme.bubbles.count].withAlphaComponent(0.55)
            leaf.strokeColor = .clear
            leaf.zRotation = .random(in: 0...(2 * .pi))
            let startX = CGFloat.random(in: -w / 2...w / 2)
            leaf.position = CGPoint(x: startX, y: .random(in: -h / 2...h / 2))
            node.addChild(leaf)

            let fallDur = Double.random(in: 9...16)
            func fallLoop() {
                leaf.position = CGPoint(x: .random(in: -w / 2...w / 2), y: h / 2 + 30)
                let down = SKAction.moveBy(x: .random(in: -40...40), y: -(h + 60), duration: fallDur)
                leaf.run(.sequence([down, .run(fallLoop)]))
            }
            // Démarrage échelonné
            leaf.run(.sequence([.wait(forDuration: Double(i) * 0.7), .run(fallLoop)]))
            // Balancement horizontal + rotation continue
            let swayR = CGFloat.random(in: 14...28)
            let swayD = Double.random(in: 1.6...2.8)
            let right = SKAction.moveBy(x: swayR, y: 0, duration: swayD)
            let left = SKAction.moveBy(x: -swayR, y: 0, duration: swayD)
            right.timingMode = .easeInEaseOut
            left.timingMode = .easeInEaseOut
            leaf.run(.repeatForever(.sequence([right, left])))
            leaf.run(.repeatForever(.rotate(byAngle: Bool.random() ? .pi : -.pi, duration: .random(in: 3...6))))
        }
    }

    // MARK: - Sable / soleil (désert)

    private static func buildSand(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        // Soleil chaud diffus en haut
        let cx = size.width * 0.24, cy = size.height * 0.32
        for (i, rr) in [110, 80, 54].enumerated() {
            let sun = SKShapeNode(circleOfRadius: CGFloat(rr))
            sun.fillColor = theme.accent.withAlphaComponent(0.10 + CGFloat(i) * 0.05)
            sun.strokeColor = .clear
            sun.position = CGPoint(x: cx, y: cy)
            node.addChild(sun)
        }
        // Poussières chaudes qui dérivent doucement
        let w = size.width, h = size.height
        for i in 0..<22 {
            let mote = SKShapeNode(circleOfRadius: .random(in: 2...6))
            mote.fillColor = theme.bubbles[i % theme.bubbles.count].withAlphaComponent(0.22)
            mote.strokeColor = .clear
            mote.position = CGPoint(x: .random(in: -w / 2...w / 2), y: .random(in: -h / 2...h / 2))
            node.addChild(mote)
            let dx = CGFloat.random(in: 20...60)
            let dur = Double.random(in: 6...12)
            let a = SKAction.moveBy(x: dx, y: 8, duration: dur)
            let b = SKAction.moveBy(x: -dx, y: -8, duration: dur)
            a.timingMode = .easeInEaseOut
            b.timingMode = .easeInEaseOut
            mote.run(.repeatForever(.sequence([a, b])))
        }
    }

    // MARK: - Bonbon (bulles + étincelles)

    private static func buildCandy(_ node: SKNode, _ size: CGSize, _ theme: Theme) {
        buildBubbles(node, size, theme)
        let w = size.width, h = size.height
        for _ in 0..<14 {
            let sparkle = makeSparkle(color: UIColor(white: 1, alpha: 0.9))
            sparkle.position = CGPoint(x: .random(in: -w / 2...w / 2), y: .random(in: -h / 2...h / 2))
            sparkle.setScale(.random(in: 0.5...1.0))
            sparkle.alpha = 0
            node.addChild(sparkle)
            let dur = Double.random(in: 0.7...1.6)
            sparkle.run(.sequence([
                .wait(forDuration: .random(in: 0...2.5)),
                .repeatForever(.sequence([
                    .group([.fadeAlpha(to: 0.9, duration: dur), .scale(to: 1.1, duration: dur)]),
                    .group([.fadeAlpha(to: 0.0, duration: dur), .scale(to: 0.5, duration: dur)]),
                ])),
            ]))
        }
    }

    private static func makeSparkle(color: UIColor) -> SKNode {
        let s = SKNode()
        let path = CGMutablePath()
        let arm: CGFloat = 7, thin: CGFloat = 1.6
        path.addRect(CGRect(x: -thin / 2, y: -arm, width: thin, height: arm * 2))
        path.addRect(CGRect(x: -arm, y: -thin / 2, width: arm * 2, height: thin))
        let shape = SKShapeNode(path: path)
        shape.fillColor = color
        shape.strokeColor = .clear
        s.addChild(shape)
        return s
    }
}
