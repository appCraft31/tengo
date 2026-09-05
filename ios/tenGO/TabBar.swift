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

    static let height: CGFloat = 84

    /// Construit la barre pour la scène hôte. `width` est la largeur
    /// réellement visible (la scène est rognée par aspectFill).
    static func make(width: CGFloat, selected: Tab) -> SKNode {
        let bar = SKNode()
        bar.zPosition = 20

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

            // Zone tactile généreuse : les libellés sont petits, la cible ne
            // doit pas l'être.
            let hit = SKShapeNode(rectOf: CGSize(width: slot, height: height))
            hit.fillColor = .clear
            hit.strokeColor = .clear
            hit.name = item.name
            item.addChild(hit)

            let accent = ThemeManager.shared.active.logo
            let tint = isSelected ? accent : accent.withAlphaComponent(0.42)

            let icon = tab.icon.node(size: 24, color: tint)
            icon.position = CGPoint(x: 0, y: 10)
            item.addChild(icon)

            let label = SKLabelNode(text: String(localized: String.LocalizationValue(tab.titleKey)))
            label.fontName = isSelected ? "AvenirNext-DemiBold" : "AvenirNext-Medium"
            label.fontSize = 11
            label.fontColor = tint
            label.verticalAlignmentMode = .center
            label.position = CGPoint(x: 0, y: -12)
            item.addChild(label)
        }
        return bar
    }

    /// Onglet touché, s'il y en a un. La scène hôte décide de la navigation :
    /// la barre ne connaît pas les scènes.
    static func tab(at point: CGPoint, in scene: SKScene) -> Tab? {
        for node in scene.nodes(at: point) {
            guard let name = node.name ?? node.parent?.name, name.hasPrefix("tab_") else { continue }
            return Tab(rawValue: String(name.dropFirst("tab_".count)))
        }
        return nil
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
