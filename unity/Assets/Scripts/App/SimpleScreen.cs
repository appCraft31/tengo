using System.Collections.Generic;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.App
{
    /// Écran provisoire fait de boutons (`UiButton`) : base du menu de
    /// développement et des écrans d'attente de l'étape 6.
    public abstract class SimpleScreen : IAppScreen
    {
        protected readonly AppRoot App;
        protected readonly List<UiButton> Buttons = new();
        UiButton _pressed;

        public Transform Root { get; }

        protected SimpleScreen(AppRoot app, string name)
        {
            App = app;
            Root = new GameObject(name).transform;
            app.SetBackground(Look.Background);
            ThemeBackground.Make(Root, Look.Theme, new Vector2(750, 1334));
        }

        protected UiButton Button(string name, string label, Vector2 posPt, Vector2 sizePt, Color? fill, System.Action onTap,
            float fontPt = 24, NunitoWeight weight = NunitoWeight.ExtraBold)
        {
            var color = fill ?? Relief.Surface;
            var b = UiButton.Create(Root, name, Units.P(posPt.x, posPt.y), sizePt, sizePt.y / 2, fill, label, weight, fontPt,
                color.ReadableInk(), 20, onTap);
            if (b.Label != null) b.Label.ShrinkToFit(sizePt.x - 24);
            Buttons.Add(b);
            return b;
        }

        protected TenGoText Title(string text, float yPt, float sizePt = 44) =>
            TenGoText.Create(Root, text, Fonts.AvenirHeavy, sizePt, Look.Logo, Units.P(0, yPt), 20).ShrinkToFit(640);

        public virtual void Tick(float dt) { }

        public virtual void PointerDown(Vector2 pt)
        {
            var world = Units.P(pt.x, pt.y);
            _pressed = null;
            foreach (var b in Buttons) if (b != null && b.Hit(world)) { _pressed = b; break; }
            if (_pressed != null) _pressed.transform.localScale = Vector3.one * 0.94f;
        }

        public virtual void PointerMove(Vector2 pt) { }

        public virtual void PointerUp(Vector2 pt)
        {
            var b = _pressed;
            _pressed = null;
            if (b == null) return;
            b.transform.localScale = Vector3.one;
            if (b.Hit(Units.P(pt.x, pt.y))) b.Tap();
        }

        public virtual void PointerCancel()
        {
            if (_pressed != null) _pressed.transform.localScale = Vector3.one;
            _pressed = null;
        }

        public virtual void Back() => App.Router.ShowMenu();
        public virtual void AppPaused() { }
        public virtual void GeometryChanged(SceneGeometry g) { }
        public virtual void Dispose() { }

        /// Bouton « point de scène » (pilote automatique) : centre du bouton nommé.
        public Vector2? ButtonPoint(string name)
        {
            foreach (var b in Buttons) if (b != null && b.name == name) return (Vector2)b.transform.position * Units.PointsPerUnit;
            return null;
        }
    }
}
