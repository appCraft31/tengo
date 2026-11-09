using System;
using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.App
{
    /// Écran d'attente d'un écran de l'étape 6 (boutique, duel, liste des
    /// puzzles, tutoriel) : son titre, quelques raccourcis utiles au test, et
    /// le retour.
    public sealed class PlaceholderScreen : SimpleScreen
    {
        readonly Action _back;

        public PlaceholderScreen(AppRoot app, string title, Action back) : base(app, "Placeholder " + title)
        {
            _back = back ?? app.Router.ShowMenu;
            Title(title, 420);
            TenGoText.Create(Root, "Écran de l'étape 6", Fonts.AvenirMedium, 18, Look.Logo.WithAlpha(0.6f), Units.P(0, 370), 20);
            Button("back", Loc.T("game_over.home"), new Vector2(0, -420), new Vector2(300, 64), null, () => _back());
        }

        public PlaceholderScreen AddButton(string name, string label, float yPt, Color? fill, Action onTap)
        {
            Button(name, label, new Vector2(0, yPt), new Vector2(420, 68), fill, onTap);
            return this;
        }

        public override void Back() => _back();
    }
}
