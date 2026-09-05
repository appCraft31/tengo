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

    static let height: CGFloat = 96

    /// Nom du nœud racine : il sert aussi à retrouver la barre pour le
    /// hit-test géométrique (cf. `tab(at:in:)`).
    static let nodeName = "tabBar"

    /// Construit la barre pour la scène hôte. `width` est la largeur
    /// réellement visible (la scène est rognée par aspectFill).
    static func make(width: CGFloat, selected: Tab) -> SKNode {
        let bar = SKNode()
        bar.name = nodeName
        bar.zPosition = 20
        // Largeur mémorisée : le hit-test découpe la barre en parts égales
        // plutôt que de compter sur les nœuds sous le doigt.
        bar.userData = ["width": width]

        let separator = SKShapeNode(rectOf: CGSize(width: width, height: 1))
        separator.fillColor = ThemeManager.shared.active.logo.withAlphaComponent(0.14)
        separator.strokeColor = .clear
        separator.position = CGPoint(x: 0, y: height / 2)
        bar.addChild(separator)

        let tabs = Tab.allCases
        let slot = width / CGFloat(tabs.count)
        for (index, tab) in tabs.enumerated() {
            let isSelected = tab == selected
            let x = -width / 2 + slot / 2 + CGFloat(index) * slot

            let item = SKNode()
            item.name = "tab_\(tab.rawValue)"
            item.position = CGPoint(x: x, y: 0)
            bar.addChild(item)

            // Zone tactile pleine largeur. L'alpha n'est pas nul : SpriteKit
            // écarte du hit-test une forme entièrement transparente, et le
            // coach-mark a besoin d'un cadre à entourer.
            let hit = SKShapeNode(rectOf: CGSize(width: slot, height: height))
            hit.fillColor = UIColor(white: 1, alpha: 0.001)
            hit.strokeColor = .clear
            hit.name = item.name
            item.addChild(hit)

            let accent = ThemeManager.shared.active.logo
            let tint = isSelected ? accent : accent.withAlphaComponent(0.45)

            let icon = tab.icon.node(size: 40, color: tint)
            icon.position = CGPoint(x: 0, y: 14)
            item.addChild(icon)

            let label = SKLabelNode(text: String(localized: String.LocalizationValue(tab.titleKey)))
            label.fontName = isSelected ? "AvenirNext-DemiBold" : "AvenirNext-Medium"
            label.fontSize = 14
            label.fontColor = tint
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: -25)
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
              let width = bar.userData?["width"] as? CGFloat else { return nil }
        let local = bar.convert(point, from: scene)
        guard abs(local.y) <= height / 2, abs(local.x) <= width / 2 else { return nil }
        let index = Int((local.x + width / 2) / (width / CGFloat(Tab.allCases.count)))
        return Tab.allCases[min(max(index, 0), Tab.allCases.count - 1)]
    }

    /// Ouvre l'écran d'un onglet. Regroupé ici pour que les quatre scènes de
    /// premier niveau naviguent exactement de la même façon.
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
        scene.view?.presentScene(destination, transition: SKTransition.fade(withDuration: 0.2))
    }
}
