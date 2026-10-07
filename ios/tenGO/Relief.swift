//
//  Relief.swift
//  tenGO
//
//  Finition commune des boutons, cartes et pastilles : un socle de la même
//  teinte (plus soutenue) sous la forme, une ombre douce et un reflet en haut.
//  L'ambiance reste celle du jeu — aplats pastel, formes rondes — mais les
//  éléments touchables prennent de l'épaisseur au lieu d'être des aplats
//  cernés d'un filet gris.
//
//  Tout est dérivé de la couleur de la forme et du thème actif : le même appel
//  tient sur le crème du thème Pastel comme sur le bleu nuit.
//

import SpriteKit

enum Relief {

    /// Surface neutre des éléments secondaires (pastilles, boutons discrets) :
    /// le fond du thème tiré vers le blanc, comme la capsule des onglets.
    static var surface: UIColor {
        let background = ThemeManager.shared.active.background
        return background.mixedWithWhite(background.perceivedLuminance > 0.5 ? 0.85 : 0.16)
    }

    /// Donne du relief à une forme DÉJÀ ajoutée à son parent.
    ///
    /// Le socle et l'ombre sont insérés juste avant elle dans le même parent,
    /// au même `zPosition` : un `zPosition` négatif les ferait passer sous le
    /// panneau qui porte le bouton (`ignoresSiblingOrder`).
    ///
    /// - Parameters:
    ///   - depth: hauteur du socle visible sous la forme.
    ///   - surface: remplace la couleur par `Relief.surface` (élément secondaire).
    ///   - fill: remplace la couleur par celle-ci.
    static func raise(_ shape: SKShapeNode, depth: CGFloat = 6,
                      surface: Bool = false, fill: UIColor? = nil) {
        guard let parent = shape.parent, let path = shape.path else { return }

        if let fill { shape.fillColor = fill }
        if surface { shape.fillColor = Relief.surface }
        // Un aplat translucide donnerait un socle visible au travers.
        shape.fillColor = shape.fillColor.flattened(over: ThemeManager.shared.active.background)
        shape.strokeColor = .clear

        let base = shape.fillColor
        let index = parent.children.firstIndex(of: shape) ?? 0

        let ledge = SKShapeNode(path: path)
        ledge.fillColor = base.ledgeShade
        ledge.strokeColor = .clear
        ledge.position = CGPoint(x: shape.position.x, y: shape.position.y - depth)
        ledge.zPosition = shape.zPosition
        ledge.name = shape.name

        // SpriteKit n'a pas d'ombre floue sur les formes : un second aplat,
        // plus bas, plus étroit et très transparent, en tient lieu.
        let halo = SKShapeNode(path: path)
        halo.fillColor = base.ledgeShade.withAlphaComponent(0.20)
        halo.strokeColor = .clear
        halo.position = CGPoint(x: shape.position.x, y: shape.position.y - depth - 6)
        halo.xScale = 0.95
        halo.zPosition = shape.zPosition
        halo.name = shape.name

        parent.insertChild(ledge, at: index)
        parent.insertChild(halo, at: index)

        // Reflet : un trait clair sous le bord haut des boutons. Sans objet sur un rond.
        let bounds = path.boundingBox
        let inset = min(bounds.height / 2, 26)
        let length = bounds.width - inset * 2
        // Ni sur une grande carte, où le trait flotterait au-dessus du contenu.
        guard length > 12, bounds.height < 150 else { return }
        let line = CGMutablePath()
        line.move(to: CGPoint(x: bounds.minX + inset, y: bounds.maxY - 5))
        line.addLine(to: CGPoint(x: bounds.maxX - inset, y: bounds.maxY - 5))
        let highlight = SKShapeNode(path: line)
        highlight.strokeColor = UIColor(white: 1, alpha: base.perceivedLuminance > 0.5 ? 0.50 : 0.16)
        highlight.lineWidth = 3
        highlight.lineCap = .round
        highlight.name = shape.name
        shape.addChild(highlight)
    }

    /// Rond clair derrière une icône, pour la détacher d'une tuile colorée.
    static func iconChip(radius: CGFloat, on color: UIColor) -> SKShapeNode {
        let chip = SKShapeNode(circleOfRadius: radius)
        chip.fillColor = UIColor(white: 1, alpha: color.perceivedLuminance > 0.5 ? 0.50 : 0.14)
        chip.strokeColor = .clear
        return chip
    }
}

extension UIColor {

    /// Teinte du socle : la même couleur, plus soutenue et un peu plus sombre.
    var ledgeShade: UIColor {
        var h: CGFloat = 0, s: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        guard getHue(&h, saturation: &s, brightness: &b, alpha: &a) else { return self }
        // Les neutres (blanc, crème) restent neutres : seule la valeur baisse.
        let saturation = s < 0.08 ? s + 0.04 : min(1, s * 1.25 + 0.04)
        return UIColor(hue: h, saturation: saturation, brightness: b * (b > 0.35 ? 0.86 : 0.70), alpha: 1)
    }

    /// Couleur opaque équivalente à celle-ci posée sur `background`.
    func flattened(over background: UIColor) -> UIColor {
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        getRed(&r, green: &g, blue: &b, alpha: &a)
        guard a < 1, a > 0 else { return self }
        var br: CGFloat = 0, bg: CGFloat = 0, bb: CGFloat = 0, ba: CGFloat = 0
        background.getRed(&br, green: &bg, blue: &bb, alpha: &ba)
        return UIColor(red: r * a + br * (1 - a),
                       green: g * a + bg * (1 - a),
                       blue: b * a + bb * (1 - a),
                       alpha: 1)
    }
}
