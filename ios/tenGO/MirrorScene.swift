//
//  MirrorScene.swift
//  tenGO
//
//  « Ton cerveau » : les quatre aptitudes du miroir cognitif, comparées à la
//  semaine précédente, puis le carnet des 41 façons de faire 10.
//  On y arrive depuis l'onglet Progression.
//

import SpriteKit

final class MirrorScene: SKScene {

    private var theme: Theme { ThemeManager.shared.active }
    private var cardWidth: CGFloat = 340

    override func didMove(to view: SKView) {
        anchorPoint = CGPoint(x: 0.5, y: 0.5)
        backgroundColor = theme.background
        addChild(ThemeBackground.make(for: theme, size: size))
        setupUI()
        AnalyticsService.mirrorOpened(gamesMeasured: CognitiveMirror.shared.gamesMeasured,
                                      repertoire: CognitiveMirror.shared.repertoireCount)
    }

    // MARK: - UI

    private func setupUI() {
        guard let view = view else { return }
        let scale = max(view.bounds.width / size.width, view.bounds.height / size.height)
        let usableWidth = view.bounds.width / scale
        let visibleHalfH = view.bounds.height / scale / 2
        let safeTop = max(view.safeAreaInsets.top, 47) / scale
        let bottomY = -visibleHalfH + view.safeAreaInsets.bottom / scale
        cardWidth = min(usableWidth - 48, 600)

        let titleY = visibleHalfH - safeTop - 36
        let title = SKLabelNode(text: String(localized: "mirror.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 36
        title.fontColor = theme.logo
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: titleY)
        addChild(title)

        let mirror = CognitiveMirror.shared
        let subtitle = SKLabelNode(text: mirror.gamesMeasured == 0
                                   ? String(localized: "mirror.calibrating")
                                   : String(format: String(localized: "mirror.subtitle"), mirror.gamesMeasured))
        subtitle.fontName = "AvenirNext-Medium"
        subtitle.fontSize = 15
        subtitle.fontColor = theme.logo.withAlphaComponent(0.7)
        subtitle.numberOfLines = 2
        subtitle.preferredMaxLayoutWidth = cardWidth
        subtitle.verticalAlignmentMode = .center
        subtitle.position = CGPoint(x: 0, y: titleY - 40)
        addChild(subtitle)

        addBackButton(atY: bottomY + 70)

        // Le contenu est construit à l'échelle 1 puis tassé s'il dépasse la
        // place disponible (iPad, petits iPhone).
        let content = SKNode()
        addChild(content)
        var cursor: CGFloat = 0

        let gap: CGFloat = 14
        let cardH: CGFloat = 156
        let cardW = (cardWidth - gap) / 2
        for (index, skill) in MirrorSkill.allCases.enumerated() {
            let column = index % 2, row = index / 2
            let center = CGPoint(x: (column == 0 ? -1 : 1) * (cardW + gap) / 2,
                                 y: cursor - cardH / 2 - CGFloat(row) * (cardH + gap))
            addSkillCard(skill, at: center, size: CGSize(width: cardW, height: cardH), to: content)
        }
        cursor -= 2 * cardH + gap + 34

        cursor = addNotebook(to: content, top: cursor)

        let top = titleY - 76
        let bottom = bottomY + 120
        let available = top - bottom
        let height = -cursor
        let fit = min(1, available / height)
        content.setScale(fit)
        content.position = CGPoint(x: 0, y: top - max(0, (available - height * fit) / 2))
    }

    private func addSkillCard(_ skill: MirrorSkill, at center: CGPoint, size: CGSize, to parent: SKNode) {
        let reading = CognitiveMirror.shared.weeklyReading(skill)
        let tint = theme.color(forValue: [3, 5, 7, 9][MirrorSkill.allCases.firstIndex(of: skill) ?? 0])
        let fill = tint.mixedWithWhite(0.55)
        let ink = fill.readableInk()

        let card = SKShapeNode(rectOf: size, cornerRadius: 24)
        card.fillColor = fill
        card.strokeColor = .clear
        card.position = center
        parent.addChild(card)
        Relief.raise(card, depth: 4)

        let left = center.x - size.width / 2 + 20
        func label(_ text: String, font: String, fontSize: CGFloat, alpha: CGFloat, y: CGFloat) -> SKLabelNode {
            let node = SKLabelNode(text: text)
            node.fontName = font
            node.fontSize = fontSize
            node.fontColor = ink.withAlphaComponent(alpha)
            node.horizontalAlignmentMode = .left
            node.verticalAlignmentMode = .center
            node.position = CGPoint(x: left, y: center.y + y)
            if node.frame.width > size.width - 36 { node.setScale((size.width - 36) / node.frame.width) }
            parent.addChild(node)
            return node
        }

        _ = label(String(localized: String.LocalizationValue(skill.titleKey)),
                  font: "AvenirNext-Bold", fontSize: 18, alpha: 1, y: 50)
        _ = label(CognitiveMirror.displayValue(skill, reading.value),
                  font: "AvenirNext-Heavy", fontSize: 34, alpha: 1, y: 12)
        let trend = trendText(skill, reading)
        _ = label(trend.text, font: "AvenirNext-DemiBold", fontSize: 15,
                  alpha: trend.positive ? 1 : 0.62, y: -26)
        _ = label(String(localized: String.LocalizationValue(skill.explainKey)),
                  font: "AvenirNext-Medium", fontSize: 13, alpha: 0.72, y: -54)
    }

    /// Évolution lisible. Une baisse est dite sans dramatiser : le jeu est zen.
    private func trendText(_ skill: MirrorSkill, _ reading: MirrorReading) -> (text: String, positive: Bool) {
        if skill == .repertoire {
            let gained = Int(reading.value ?? 0) - Int(reading.previous ?? 0)
            return gained > 0
                ? ("↑ " + String(format: String(localized: "mirror.trend.new_week"), gained), true)
                : (String(localized: "mirror.trend.stable"), false)
        }
        guard let value = reading.value, let previous = reading.previous else {
            return (String(localized: "mirror.trend.none"), false)
        }
        let improvement = reading.improvement(lowerIsBetter: CognitiveMirror.lowerIsBetter(skill)) ?? 0
        guard abs(improvement) >= 0.03 else { return (String(localized: "mirror.trend.stable"), false) }
        let better = improvement > 0
        let arrow = better ? "↑ " : "↓ "
        let text: String
        switch skill {
        case .look:
            text = String(format: String(localized: better ? "mirror.trend.faster" : "mirror.trend.slower"),
                          CognitiveMirror.formatDecimal(abs(value - previous)))
        case .vision:
            text = String(format: String(localized: better ? "mirror.trend.more" : "mirror.trend.less"),
                          CognitiveMirror.formatDecimal(abs(value - previous)))
        case .anticipation:
            text = String(format: String(localized: better ? "mirror.trend.points_up" : "mirror.trend.points_down"),
                          Int((abs(value - previous) * 100).rounded()))
        case .repertoire:
            text = ""
        }
        return (arrow + text, better)
    }

    /// Carnet des 10 : les 41 décompositions, groupées par nombre de bulles.
    /// Les trouvées sont écrites, les autres restent à deviner.
    private func addNotebook(to parent: SKNode, top: CGFloat) -> CGFloat {
        let mirror = CognitiveMirror.shared
        var cursor = top

        let header = SKLabelNode(text: String(localized: "mirror.notebook"))
        header.fontName = "AvenirNext-Bold"
        header.fontSize = 22
        header.fontColor = theme.logo
        header.horizontalAlignmentMode = .left
        header.verticalAlignmentMode = .center
        header.position = CGPoint(x: -cardWidth / 2 + 4, y: cursor)
        parent.addChild(header)

        let count = SKLabelNode(text: "\(mirror.repertoireCount) / \(CognitiveMirror.repertoireTotal)")
        count.fontName = "AvenirNext-DemiBold"
        count.fontSize = 18
        count.fontColor = theme.logo.withAlphaComponent(0.7)
        count.horizontalAlignmentMode = .right
        count.verticalAlignmentMode = .center
        count.position = CGPoint(x: cardWidth / 2 - 4, y: cursor)
        parent.addChild(count)
        cursor -= 36

        let groups = Dictionary(grouping: Partition.all, by: \.count)
        let chipH: CGFloat = 30, chipGap: CGFloat = 8, labelW: CGFloat = 92
        let flowLeft = -cardWidth / 2 + labelW
        let flowRight = cardWidth / 2
        let accent = theme.accent

        for size in groups.keys.sorted() {
            let parts = groups[size] ?? []
            let found = parts.filter { mirror.discovered[Partition.key(for: $0)] != nil }.count
            let groupLabel = SKLabelNode(text: String(format: String(localized: "mirror.notebook.group"), size))
            groupLabel.fontName = "AvenirNext-DemiBold"
            groupLabel.fontSize = 14
            groupLabel.fontColor = theme.logo.withAlphaComponent(found == parts.count ? 1 : 0.7)
            groupLabel.horizontalAlignmentMode = .left
            groupLabel.verticalAlignmentMode = .center
            groupLabel.position = CGPoint(x: -cardWidth / 2 + 4, y: cursor - chipH / 2)
            if groupLabel.frame.width > labelW - 10 { groupLabel.setScale((labelW - 10) / groupLabel.frame.width) }
            parent.addChild(groupLabel)

            var x = flowLeft
            var rowY = cursor - chipH / 2
            for part in parts {
                let key = Partition.key(for: part)
                let isFound = mirror.discovered[key] != nil
                let text = isFound
                    ? part.map(String.init).joined(separator: "+")
                    : Array(repeating: "?", count: part.count).joined(separator: "+")
                let label = SKLabelNode(text: text)
                label.fontName = isFound ? "AvenirNext-Bold" : "AvenirNext-Medium"
                label.fontSize = 14
                label.verticalAlignmentMode = .center
                let width = label.frame.width + 18
                if x + width > flowRight {
                    x = flowLeft
                    rowY -= chipH + chipGap
                }
                let chip = SKShapeNode(rectOf: CGSize(width: width, height: chipH), cornerRadius: chipH / 2)
                chip.fillColor = isFound ? accent.mixedWithWhite(0.35) : UIColor(white: 1, alpha: 0.45)
                chip.strokeColor = isFound ? .clear : theme.logo.withAlphaComponent(0.18)
                chip.lineWidth = 1
                chip.position = CGPoint(x: x + width / 2, y: rowY)
                parent.addChild(chip)
                label.fontColor = isFound ? accent.mixedWithWhite(0.35).readableInk() : theme.logo.withAlphaComponent(0.4)
                label.position = chip.position
                parent.addChild(label)
                x += width + chipGap
            }
            cursor = rowY - chipH / 2 - 14
        }
        return cursor
    }

    private func addBackButton(atY y: CGFloat) {
        let back = SKNode()
        back.name = "back"
        back.position = CGPoint(x: 0, y: y)
        addChild(back)

        let circle = SKShapeNode(circleOfRadius: 36)
        circle.fillColor = UIColor(red: 0.94, green: 0.91, blue: 0.88, alpha: 1)
        circle.strokeColor = UIColor(white: 0.68, alpha: 0.45)
        circle.lineWidth = 1.5
        back.addChild(circle)
        Relief.raise(circle, depth: 4)

        let icon = SKLabelNode(text: "‹")
        icon.fontName = "AvenirNext-Medium"
        icon.fontSize = 32
        icon.fontColor = UIColor(white: 0.45, alpha: 1)
        icon.verticalAlignmentMode = .center
        icon.horizontalAlignmentMode = .center
        back.addChild(icon)
    }

    // MARK: - Touch

    override func touchesBegan(_ touches: Set<UITouch>, with event: UIEvent?) {
        guard let touch = touches.first else { return }
        let point = touch.location(in: self)
        tapFeedback(at: point)
        for node in nodes(at: point) where (node.parent?.name ?? node.name) == "back" {
            let profile = ProfileScene(size: size)
            profile.scaleMode = .aspectFill
            view?.presentScene(profile, transition: SceneTransition.fade(0.28))
            return
        }
    }
}
