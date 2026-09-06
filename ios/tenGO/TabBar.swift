//
//  TabBar.swift
//  tenGO
//
//  Navigation principale, présente au bas des quatre écrans de premier
//  niveau. Elle règle un problème de fond : l'accueil grossissait d'un bouton
//  à chaque fonctionnalité livrée, au point que le neuvième sortait de
//  l'écran. Les destinations secondaires vivent désormais dans un onglet, et
//  l'accueil ne grandit plus.
//

import SpriteKit

enum Tab: String, CaseIterable {
    case play, progress, social, shop

    var icon: VectorIcon {
        switch self {
        case .play:     return .play
        case .progress: return .progress
        case .social:   return .social
        case .shop:     return .shop
        }
    }

    var titleKey: String { "tab.\(rawValue)" }
}

enum TabBar {

    /// Hauteur totale réservée par les scènes hôtes : la capsule, plus la
    /// marge qui la fait flotter au-dessus du bord bas.
    static let height: CGFloat = 106

    /// Nom du nœud racine : il sert aussi à retrouver la barre pour le
    /// hit-test géométrique (cf. `tab(at:in:)`).
    static let nodeName = "tabBar"

    private static let capsuleHeight: CGFloat = 92
    /// Retrait latéral : la capsule ne touche pas les bords de l'écran.
    private static let sideInset: CGFloat = 22
    /// Marge intérieure entre le bord de la capsule et la pilule active.
    private static let padding: CGFloat = 7
    /// Centre de la capsule dans le nœud : décalé vers le haut, la marge
    /// restante passe sous elle.
    private static var capsuleY: CGFloat { (height - capsuleHeight) / 2 }

    /// Construit la barre pour la scène hôte. `width` est la largeur
    /// réellement visible (la scène est rognée par aspectFill).
    ///
    /// La barre n'est pas un chrome système posé sous la page : c'est une
    /// capsule flottante, la même forme que les boutons « Jouer » et
    /// « Reprendre » qui la surplombent, et l'onglet actif est lui-même une
    /// pilule accentuée — pas une nuance de gris de plus.
    static func make(width: CGFloat, selected: Tab) -> SKNode {
        let theme = ThemeManager.shared.active
        let capsuleW = width - sideInset * 2

        let bar = SKNode()
        bar.name = nodeName
        bar.zPosition = 20
        // Géométrie mémorisée : le hit-test découpe la capsule en parts
        // égales plutôt que de compter sur les nœuds sous le doigt.
        bar.userData = ["width": capsuleW]

        // Ombre portée simulée (SpriteKit n'en a pas sur les formes) : même
        // capsule, décalée et translucide.
        let shadow = SKShapeNode(rectOf: CGSize(width: capsuleW, height: capsuleHeight),
                                 cornerRadius: capsuleHeight / 2)
        shadow.fillColor = UIColor(white: 0, alpha: 0.10)
        shadow.strokeColor = .clear
        shadow.position = CGPoint(x: 0, y: capsuleY - 5)
        bar.addChild(shadow)

        let capsule = SKShapeNode(rectOf: CGSize(width: capsuleW, height: capsuleHeight),
                                  cornerRadius: capsuleHeight / 2)
        // Surface dérivée du fond du thème : elle se détache sur le crème du
        // thème Pastel comme sur le bleu nuit du thème Nuit.
        let lift: CGFloat = theme.background.perceivedLuminance > 0.5 ? 0.78 : 0.14
        capsule.fillColor = theme.background.mixedWithWhite(lift)
        capsule.strokeColor = theme.logo.withAlphaComponent(0.12)
        capsule.lineWidth = 1
        capsule.position = CGPoint(x: 0, y: capsuleY)
        bar.addChild(capsule)

        let tabs = Tab.allCases
        let inner = capsuleW - padding * 2
        let slot = inner / CGFloat(tabs.count)
        let pillH = capsuleHeight - padding * 2

        for (index, tab) in tabs.enumerated() {
            let isSelected = tab == selected
            let x = -inner / 2 + slot / 2 + CGFloat(index) * slot

            let item = SKNode()
            item.name = "tab_\(tab.rawValue)"
            item.position = CGPoint(x: x, y: capsuleY)
            item.zPosition = 1
            bar.addChild(item)

            // Zone tactile pleine hauteur. L'alpha n'est pas nul : SpriteKit
            // écarte du hit-test une forme entièrement transparente, et le
            // coach-mark a besoin d'un cadre à entourer.
            let hit = SKShapeNode(rectOf: CGSize(width: slot, height: capsuleHeight))
            hit.fillColor = UIColor(white: 1, alpha: 0.001)
            hit.strokeColor = .clear
            hit.name = item.name
            item.addChild(hit)

            let tint: UIColor
            if isSelected {
                let accent = theme.color(forValue: 4)
                let pill = SKShapeNode(rectOf: CGSize(width: slot, height: pillH),
                                       cornerRadius: pillH / 2)
                pill.fillColor = accent
                pill.strokeColor = .clear
                pill.name = item.name
                item.addChild(pill)
                tint = accent.readableInk()
            } else {
                tint = theme.logo.withAlphaComponent(0.55)
            }

            let icon = tab.icon.node(size: 34, color: tint)
            icon.position = CGPoint(x: 0, y: 12)
            item.addChild(icon)

            let label = SKLabelNode(text: String(localized: String.LocalizationValue(tab.titleKey)))
            label.fontName = isSelected ? "AvenirNext-Bold" : "AvenirNext-Medium"
            label.fontSize = 13
            label.fontColor = tint
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: -20)
            item.addChild(label)
        }
        return bar
    }

    /// Onglet touché, s'il y en a un. Le découpage est géométrique et non
    /// fondé sur les nœuds sous le doigt : un tap entre l'icône et le libellé
    /// doit compter, et il ne comptait pas quand la cible était une forme
    /// transparente. La scène hôte décide de la navigation ; la barre ne
    /// connaît pas les scènes.
    static func tab(at point: CGPoint, in scene: SKScene) -> Tab? {
        guard let bar = scene.childNode(withName: "//\(nodeName)"),
              let capsuleW = bar.userData?["width"] as? CGFloat else { return nil }
        let local = bar.convert(point, from: scene)
        guard abs(local.y - capsuleY) <= capsuleHeight / 2,
              abs(local.x) <= capsuleW / 2 else { return nil }
        let index = Int((local.x + capsuleW / 2) / (capsuleW / CGFloat(Tab.allCases.count)))
        return Tab.allCases[min(max(index, 0), Tab.allCases.count - 1)]
    }

    /// Ouvre l'écran d'un onglet. Regroupé ici pour que les quatre scènes de
    /// premier niveau naviguent exactement de la même façon.
    ///
    /// Sans transition, volontairement : les quatre écrans forment un même
    /// espace, pas une succession de pages. Une animation, si courte soit-elle,
    /// les ferait paraître plus éloignés les uns des autres qu'ils ne le sont,
    /// et ajouterait une attente à chaque aller-retour.
    static func present(_ tab: Tab, from scene: SKScene) {
        let size = scene.size
        let destination: SKScene
        switch tab {
        case .play:     destination = MenuScene(size: size)
        case .progress: destination = ProfileScene(size: size)
        case .social:   destination = DuelScene(size: size)
        case .shop:     destination = BoutiqueScene(size: size)
        }
        destination.scaleMode = .aspectFill
        scene.view?.presentScene(destination)
    }
}
