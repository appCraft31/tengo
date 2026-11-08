import SpriteKit

/// Retour haptique léger sur les boutons des écrans de menu (parité Android
/// `tapFeedback()`). Un nœud nommé sous le doigt = un bouton ; quelques noms
/// de conteneurs non cliquables sont exclus, et les « Réclamer » ont déjà
/// leur propre retour, plus marqué.
extension SKScene {
    private static let tapExcluded: Set<String> = ["tabBar", "duelRowHeader"]

    func tapFeedback(at point: CGPoint) {
        for node in nodes(at: point) {
            var current: SKNode? = node
            for _ in 0..<3 {
                guard let n = current else { break }
                if let name = n.name, !name.isEmpty, !SKScene.tapExcluded.contains(name) {
                    if !name.hasPrefix("claim_") { HapticManager.light() }
                    return
                }
                current = n.parent
            }
        }
    }
}
