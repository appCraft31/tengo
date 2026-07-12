//
//  SettingsOverlay.swift
//  tenGO
//
//  Popup paramètres complet :
//    - Son (toggle)          - Retours haptiques (toggle)
//    - Revoir le tutoriel    - Options de confidentialité
//    - Noter l'application   - Partager tenGO
//    - Contacter le support
//

import SpriteKit
import UIKit
import StoreKit
import MessageUI

final class SettingsOverlay: SKNode {

    // MARK: - Actions gérées par la scène appelante

    enum Action {
        case replayTutorial
    }

    var onAction: ((Action) -> Void)?

    // MARK: - Dépendances

    private weak var presenter: UIViewController?
    private let sceneSize: CGSize

    // MARK: - UI

    private var card: SKNode!
    private var dimNode: SKSpriteNode!

    private var soundToggleBg: SKShapeNode!
    private var soundToggleKnob: SKShapeNode!
    private var soundStatus: SKLabelNode!

    private var hapticToggleBg: SKShapeNode!
    private var hapticToggleKnob: SKShapeNode!
    private var hapticStatus: SKLabelNode!

    private static let cardW: CGFloat = 490
    private static let cardH: CGFloat = 680

    // MARK: - Init

    init(sceneSize: CGSize, presenter: UIViewController?) {
        self.sceneSize = sceneSize
        self.presenter = presenter
        super.init()
        zPosition = 100
        buildUI()
    }

    required init?(coder: NSCoder) { fatalError() }

    // MARK: - API

    func present(in parent: SKNode) {
        alpha = 0
        parent.addChild(self)
        run(SKAction.fadeIn(withDuration: 0.18))
        card.setScale(0.92)
        card.run(SKAction.scale(to: 1.0, duration: 0.22))
    }

    @discardableResult
    func handleTouch(at scenePoint: CGPoint) -> Bool {
        let local = convert(scenePoint, from: parent!)
        for node in nodes(at: local) {
            let name = node.name ?? node.parent?.name ?? ""
            switch name {
            case "closeBtn", "closeBg":
                dismiss(); return true
            case "row_sound", "row_sound_toggle":
                toggleSound(); return true
            case "row_haptic", "row_haptic_toggle":
                toggleHaptic(); return true
            case "row_tutorial":
                animateRow(named: "row_tutorial")
                dismiss()
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { [weak self] in
                    self?.onAction?(.replayTutorial)
                }
                return true
            case "row_privacy":
                animateRow(named: "row_privacy"); presentPrivacyOptions(); return true
            case "row_rate":
                animateRow(named: "row_rate"); requestReview(); return true
            case "row_share":
                animateRow(named: "row_share"); presentShareSheet(); return true
            case "row_support":
                animateRow(named: "row_support"); openSupportMail(); return true
            default: continue
            }
        }
        if dimNode.contains(local) { dismiss() }
        return true
    }

    func dismiss() {
        run(SKAction.sequence([
            SKAction.fadeOut(withDuration: 0.15),
            SKAction.removeFromParent()
        ]))
    }

    // MARK: - Build UI

    private func buildUI() {
        dimNode = SKSpriteNode(color: UIColor(white: 0, alpha: 0.55),
                               size: CGSize(width: sceneSize.width * 2.5,
                                            height: sceneSize.height * 2.5))
        dimNode.zPosition = 0
        addChild(dimNode)

        card = SKNode()
        card.zPosition = 1
        addChild(card)

        let bg = SKShapeNode(rectOf: CGSize(width: Self.cardW, height: Self.cardH),
                             cornerRadius: 28)
        bg.fillColor = UIColor(red: 0.98, green: 0.96, blue: 0.93, alpha: 1)
        bg.strokeColor = UIColor(white: 0.70, alpha: 0.25)
        bg.lineWidth = 1
        card.addChild(bg)

        // Titre
        let title = SKLabelNode(text: String(localized: "settings.title"))
        title.fontName = "AvenirNext-Heavy"
        title.fontSize = 30
        title.fontColor = UIColor(white: 0.25, alpha: 1)
        title.verticalAlignmentMode = .center
        title.position = CGPoint(x: 0, y: Self.cardH / 2 - 48)
        card.addChild(title)

        // Close button
        let closeNode = SKNode()
        closeNode.name = "closeBtn"
        closeNode.position = CGPoint(x: Self.cardW / 2 - 32, y: Self.cardH / 2 - 32)
        card.addChild(closeNode)

        let closeBg = SKShapeNode(circleOfRadius: 18)
        closeBg.name = "closeBg"
        closeBg.fillColor = UIColor(white: 0.92, alpha: 1)
        closeBg.strokeColor = .clear
        closeNode.addChild(closeBg)

        let closeIcon = SKLabelNode(text: "×")
        closeIcon.name = "closeBtn"
        closeIcon.fontName = "AvenirNext-Medium"
        closeIcon.fontSize = 28
        closeIcon.fontColor = UIColor(white: 0.40, alpha: 1)
        closeIcon.verticalAlignmentMode = .center
        closeIcon.horizontalAlignmentMode = .center
        closeIcon.position = CGPoint(x: 0, y: 1)
        closeNode.addChild(closeIcon)

        // --- Rows
        // Y de la première ligne (sous le titre)
        var y: CGFloat = Self.cardH / 2 - 110
        let rowStep: CGFloat = 58

        // Son (toggle)
        let soundViews = addToggleRow(name: "row_sound", title: String(localized: "settings.sound"), y: y)
        soundToggleBg = soundViews.bg
        soundToggleKnob = soundViews.knob
        soundStatus = soundViews.status
        y -= rowStep

        // Retours haptiques
        let hapticViews = addToggleRow(name: "row_haptic", title: String(localized: "settings.haptics"), y: y)
        hapticToggleBg = hapticViews.bg
        hapticToggleKnob = hapticViews.knob
        hapticStatus = hapticViews.status
        y -= rowStep

        addSeparator(y: y + 8)
        y -= 10

        addActionRow(name: "row_tutorial", title: String(localized: "settings.replay_tutorial"), y: y); y -= rowStep
        addActionRow(name: "row_privacy", title: String(localized: "settings.privacy_options"), y: y); y -= rowStep

        addSeparator(y: y + 8)
        y -= 10

        addActionRow(name: "row_rate", title: String(localized: "settings.rate_app"), y: y); y -= rowStep
        addActionRow(name: "row_share", title: String(localized: "settings.share_tengo"), y: y); y -= rowStep
        addActionRow(name: "row_support", title: String(localized: "settings.contact_support"), y: y); y -= rowStep

        // Footer version
        let footer = SKLabelNode(text: AppConfig.appVersion)
        footer.fontName = "AvenirNext-Regular"
        footer.fontSize = 13
        footer.fontColor = UIColor(white: 0.55, alpha: 1)
        footer.verticalAlignmentMode = .center
        footer.position = CGPoint(x: 0, y: -Self.cardH / 2 + 24)
        card.addChild(footer)

        updateSoundVisual(animated: false)
        updateHapticVisual(animated: false)
    }

    // MARK: - Row builders

    @discardableResult
    private func addToggleRow(name: String, title: String, y: CGFloat)
    -> (bg: SKShapeNode, knob: SKShapeNode, status: SKLabelNode) {

        // Hitbox invisible couvrant toute la ligne (pour tap facile)
        let hit = SKShapeNode(rectOf: CGSize(width: Self.cardW - 40, height: 48))
        hit.name = name
        hit.fillColor = .clear
        hit.strokeColor = .clear
        hit.position = CGPoint(x: 0, y: y)
        card.addChild(hit)

        let label = SKLabelNode(text: title)
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 21
        label.fontColor = UIColor(white: 0.30, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        label.position = CGPoint(x: -Self.cardW / 2 + 36, y: y + 2)
        card.addChild(label)

        let container = SKNode()
        container.name = "\(name)_toggle"
        container.position = CGPoint(x: Self.cardW / 2 - 72, y: y)
        card.addChild(container)

        let bg = SKShapeNode(rectOf: CGSize(width: 64, height: 32), cornerRadius: 16)
        bg.name = "\(name)_toggle"
        bg.lineWidth = 0
        container.addChild(bg)

        let knob = SKShapeNode(circleOfRadius: 12)
        knob.name = "\(name)_toggle"
        knob.fillColor = .white
        knob.strokeColor = UIColor(white: 0.85, alpha: 1)
        knob.lineWidth = 1
        container.addChild(knob)

        let status = SKLabelNode(text: "")
        status.fontName = "AvenirNext-Regular"
        status.fontSize = 12
        status.fontColor = UIColor(white: 0.55, alpha: 1)
        status.verticalAlignmentMode = .center
        status.horizontalAlignmentMode = .center
        status.position = CGPoint(x: Self.cardW / 2 - 72, y: y - 26)
        card.addChild(status)

        return (bg, knob, status)
    }

    private func addActionRow(name: String, title: String, y: CGFloat) {
        let hit = SKShapeNode(rectOf: CGSize(width: Self.cardW - 40, height: 48))
        hit.name = name
        hit.fillColor = .clear
        hit.strokeColor = .clear
        hit.position = CGPoint(x: 0, y: y)
        card.addChild(hit)

        let label = SKLabelNode(text: title)
        label.name = name
        label.fontName = "AvenirNext-Medium"
        label.fontSize = 21
        label.fontColor = UIColor(white: 0.30, alpha: 1)
        label.verticalAlignmentMode = .center
        label.horizontalAlignmentMode = .left
        label.position = CGPoint(x: -Self.cardW / 2 + 36, y: y + 2)
        card.addChild(label)

        let chevron = SKLabelNode(text: "›")
        chevron.name = name
        chevron.fontName = "AvenirNext-Medium"
        chevron.fontSize = 28
        chevron.fontColor = UIColor(white: 0.55, alpha: 1)
        chevron.verticalAlignmentMode = .center
        chevron.horizontalAlignmentMode = .right
        chevron.position = CGPoint(x: Self.cardW / 2 - 36, y: y + 2)
        card.addChild(chevron)
    }

    private func addSeparator(y: CGFloat) {
        let w = Self.cardW - 72
        let line = SKShapeNode(rectOf: CGSize(width: w, height: 1))
        line.fillColor = UIColor(white: 0.85, alpha: 1)
        line.strokeColor = .clear
        line.position = CGPoint(x: 0, y: y)
        card.addChild(line)
    }

    private func animateRow(named name: String) {
        guard let node = card.childNode(withName: name) else { return }
        node.run(SKAction.sequence([
            SKAction.fadeAlpha(to: 0.4, duration: 0.08),
            SKAction.fadeAlpha(to: 1.0, duration: 0.18)
        ]))
    }

    // MARK: - Toggle son

    private func toggleSound() {
        SoundManager.shared.isMuted.toggle()
        updateSoundVisual(animated: true)
    }

    private func updateSoundVisual(animated: Bool) {
        let muted = SoundManager.shared.isMuted
        let bgColor = muted
            ? UIColor(white: 0.82, alpha: 1)
            : UIColor(red: 0.55, green: 0.82, blue: 0.65, alpha: 1)
        let knobX: CGFloat = muted ? -16 : 16
        let text = muted ? String(localized: "settings.toggle_off") : String(localized: "settings.toggle_on")

        if animated {
            soundToggleBg.run(SKAction.customAction(withDuration: 0.18) { [weak self] node, _ in
                (node as? SKShapeNode)?.fillColor = bgColor
                self?.soundStatus.text = text
            })
            soundToggleKnob.run(SKAction.moveTo(x: knobX, duration: 0.18))
        } else {
            soundToggleBg.fillColor = bgColor
            soundToggleKnob.position = CGPoint(x: knobX, y: 0)
            soundStatus.text = text
        }
    }

    // MARK: - Toggle haptique

    private func toggleHaptic() {
        HapticManager.isEnabled.toggle()
        if HapticManager.isEnabled { HapticManager.light() }  // feedback immédiat
        updateHapticVisual(animated: true)
    }

    private func updateHapticVisual(animated: Bool) {
        let enabled = HapticManager.isEnabled
        let bgColor = enabled
            ? UIColor(red: 0.55, green: 0.82, blue: 0.65, alpha: 1)
            : UIColor(white: 0.82, alpha: 1)
        let knobX: CGFloat = enabled ? 16 : -16
        let text = enabled ? String(localized: "settings.toggle_on") : String(localized: "settings.toggle_off")

        if animated {
            hapticToggleBg.run(SKAction.customAction(withDuration: 0.18) { [weak self] node, _ in
                (node as? SKShapeNode)?.fillColor = bgColor
                self?.hapticStatus.text = text
            })
            hapticToggleKnob.run(SKAction.moveTo(x: knobX, duration: 0.18))
        } else {
            hapticToggleBg.fillColor = bgColor
            hapticToggleKnob.position = CGPoint(x: knobX, y: 0)
            hapticStatus.text = text
        }
    }

    // MARK: - Actions UIKit

    private func requestReview() {
        guard let scene = presenter?.view.window?.windowScene else { return }
        if #available(iOS 14.0, *) {
            SKStoreReviewController.requestReview(in: scene)
        } else {
            SKStoreReviewController.requestReview()
        }
    }

    private func presentShareSheet() {
        guard let vc = presenter else { return }
        let items: [Any] = [AppConfig.shareMessage]
        let sheet = UIActivityViewController(activityItems: items, applicationActivities: nil)
        sheet.popoverPresentationController?.sourceView = vc.view
        sheet.popoverPresentationController?.sourceRect = CGRect(
            x: vc.view.bounds.midX, y: vc.view.bounds.midY, width: 0, height: 0)
        vc.present(sheet, animated: true)
    }

    private func openSupportMail() {
        let subject = String(localized: "settings.support_subject")
        let body = "\n\n---\n\(AppConfig.appVersion) • iOS \(UIDevice.current.systemVersion)"
        let enc = { (s: String) -> String in
            s.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? s
        }
        let url = URL(string: "mailto:\(AppConfig.supportEmail)?subject=\(enc(subject))&body=\(enc(body))")
        guard let url = url else { return }
        UIApplication.shared.open(url, options: [:], completionHandler: nil)
    }

    private func presentPrivacyOptions() {
        // Réglages système de l'app, où vit le toggle « Autoriser le suivi »
        // (plus de formulaire UMP : le consentement se résume à l'ATT).
        guard let url = URL(string: UIApplication.openSettingsURLString) else { return }
        UIApplication.shared.open(url, options: [:], completionHandler: nil)
    }

}
