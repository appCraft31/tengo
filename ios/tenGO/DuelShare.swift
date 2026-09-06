//
//  DuelShare.swift
//  tenGO
//
//  Partage du code d'un duel (issue #31).
//
//  Un duel se joue à deux, et le code est le seul moyen d'y inviter quelqu'un.
//  Tant qu'il n'était qu'affiché à l'écran, il fallait le mémoriser ou le
//  recopier à la main avant de basculer dans sa messagerie : le duel s'arrêtait
//  souvent là.
//
//  Le message porte TOUJOURS le code en clair. Le lien `tengo://` qui le suit
//  est un confort, pas le support : ni WhatsApp, ni Messenger, ni iMessage ne
//  transforment un scheme personnalisé en lien cliquable, et il ne mène nulle
//  part chez quelqu'un qui n'a pas le jeu.
//

import UIKit

enum DuelShare {

    /// Lien profond que `DeepLink` sait décoder (cf. DeepLink.swift).
    static func url(for code: String) -> String { "tengo://duel/\(code)" }

    /// Texte envoyé dans la messagerie choisie.
    static func message(for code: String) -> String {
        String(format: String(localized: "duel.share_message"), code) + "\n" + url(for: code)
    }

    /// Présente la feuille de partage du système.
    ///
    /// `sourceView` et `sourceRect` ne sont pas facultatifs en pratique : sur
    /// iPad, une feuille présentée sans ancrage de popover fait planter l'app.
    static func present(code: String,
                        from presenter: UIViewController?,
                        sourceRect: CGRect? = nil) {
        guard let presenter else { return }
        let sheet = UIActivityViewController(activityItems: [message(for: code)],
                                             applicationActivities: nil)
        sheet.popoverPresentationController?.sourceView = presenter.view
        sheet.popoverPresentationController?.sourceRect = sourceRect ?? CGRect(
            x: presenter.view.bounds.midX, y: presenter.view.bounds.midY, width: 0, height: 0)
        presenter.present(sheet, animated: true)
    }
}
