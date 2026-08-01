//
//  HapticManager.swift
//  tenGO
//
//  Wrapper sur UIImpactFeedbackGenerator avec toggle persisté.
//
//  Pas de Core Haptics : `impactOccurred(intensity:)` couvre déjà l'escalade
//  dont le jeu a besoin, et un second moteur dupliquerait la gestion
//  d'interruption/background déjà nécessaire pour AVAudioEngine.
//

import UIKit

enum HapticManager {

    static var isEnabled: Bool {
        get { UserDefaults.standard.object(forKey: "tenGO_hapticsEnabled") as? Bool ?? true }
        set { UserDefaults.standard.set(newValue, forKey: "tenGO_hapticsEnabled") }
    }

    /// Impact léger — sélection/connexion
    static let lightGenerator = UIImpactFeedbackGenerator(style: .light)

    /// Impact medium — validation combo
    static let mediumGenerator = UIImpactFeedbackGenerator(style: .medium)

    /// Impact fort — fin des grandes chaînes
    static let heavyGenerator = UIImpactFeedbackGenerator(style: .heavy)

    /// Texture sèche/douce — nuance le tracé sans le rendre bruyant
    static let rigidGenerator = UIImpactFeedbackGenerator(style: .rigid)
    static let softGenerator = UIImpactFeedbackGenerator(style: .soft)

    static let notificationGenerator = UINotificationFeedbackGenerator()

    static func prepare() {
        lightGenerator.prepare()
        mediumGenerator.prepare()
        heavyGenerator.prepare()
        rigidGenerator.prepare()
        softGenerator.prepare()
        notificationGenerator.prepare()
    }

    static func light() {
        guard isEnabled else { return }
        lightGenerator.impactOccurred()
    }

    static func medium() {
        guard isEnabled else { return }
        mediumGenerator.impactOccurred()
    }

    static func heavy() {
        guard isEnabled else { return }
        heavyGenerator.impactOccurred()
    }

    /// Impact d'intensité continue — c'est ce qui permet à la tension du tracé
    /// de se sentir progressivement, sans paliers audibles.
    static func impact(_ style: UIImpactFeedbackGenerator.FeedbackStyle,
                       intensity: CGFloat) {
        guard isEnabled else { return }
        let generator: UIImpactFeedbackGenerator
        switch style {
        case .heavy: generator = heavyGenerator
        case .medium: generator = mediumGenerator
        case .rigid: generator = rigidGenerator
        case .soft: generator = softGenerator
        default: generator = lightGenerator
        }
        generator.impactOccurred(intensity: max(0, min(1, intensity)))
    }

    static func success() {
        guard isEnabled else { return }
        notificationGenerator.notificationOccurred(.success)
    }

    static func warning() {
        guard isEnabled else { return }
        notificationGenerator.notificationOccurred(.warning)
    }

    /// Motif de validation, proportionné à la longueur de la chaîne.
    /// Volontairement en `asyncAfter` et non en SKAction : le retour haptique
    /// ne doit jamais dépendre de la boucle de rendu ni du verrou d'animation.
    static func chain(length: Int) {
        guard isEnabled else { return }
        switch length {
        case ...3:
            impact(.medium, intensity: 0.7)
        case 4...5:
            impact(.medium, intensity: 0.9)
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.055) {
                impact(.rigid, intensity: 0.7)
            }
        default:
            // 6+ : trois impacts croissants, le joueur sent la chaîne « monter ».
            impact(.medium, intensity: 0.6)
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.045) {
                impact(.rigid, intensity: 0.8)
            }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.090) {
                impact(.heavy, intensity: 1.0)
            }
        }
    }
}
