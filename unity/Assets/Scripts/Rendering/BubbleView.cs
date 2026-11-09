using System;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Bulle de la grille (BubbleNode.swift) : disque de la couleur de sa valeur
    /// selon le thème, chiffre centré, matière cosmétique, et ses états
    /// (sélection qui respire, souffle, éclatement, chute avec écrasement,
    /// ancre, givre, fleur de graine). Rayon 39 pt ; l'ordre d'affichage `Order`
    /// vaut la `zPosition` SpriteKit × 10.
    [DisallowMultipleComponent]
    public sealed class BubbleView : MonoBehaviour
    {
        public const float RadiusPt = 39f;
        public const float DigitSizePt = 36f;

        /// Couleur des pétales : un or chaud, lisible sur tous les thèmes.
        public static readonly Color BloomColor = new(0.98f, 0.76f, 0.28f, 1f);

        public int Value { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsAnchored { get; private set; }
        public int Bloom { get; private set; }
        public bool IsSelected { get; private set; }
        public BubbleMaterial Material { get; private set; }
        public int Order { get; private set; }
        public ShapeView Circle => _circle;
        public TenGoText Digit => _digit;

        ShapeView _circle;
        TenGoText _digit;
        Transform _frost;
        Transform _corolla;
        float _radius;
        Color _color;
        Vector3? _nudgeOrigin;

        // MARK: - Création

        /// Bulle de `value` aux couleurs du thème actif (`Look`), matière
        /// `material` (par défaut la matière active), rayon `radiusPt`.
        public static BubbleView Create(Transform parent, int value, Vector2 localPos, int order = 0,
            BubbleMaterial? material = null, float radiusPt = RadiusPt)
        {
            var go = new GameObject($"Bubble{value}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            FxAlpha.Of(go);
            var b = go.AddComponent<BubbleView>();
            b.Build(value, order, material ?? Look.Bubble, radiusPt);
            return b;
        }

        void Build(int value, int order, BubbleMaterial material, float radius)
        {
            Value = value;
            Order = order;
            Material = material;
            _radius = radius;
            _color = Look.BubbleColor(value);
            _circle = ShapeView.Circle("Circle", transform, Vector2.zero, radius, _color, _color.Darkened(0.12f), 1.5f, order);
            _digit = TenGoText.Create(transform, value.ToString(), Fonts.AvenirMedium, DigitSizePt * (radius / RadiusPt),
                Look.Digit, Vector2.zero, order + 2);
            ApplyMaterial();
        }

        /// Matière de bulle (cosmétique, purement visuelle).
        void ApplyMaterial()
        {
            switch (Material)
            {
                case BubbleMaterial.Matte:
                    _circle.Stroke = Color.clear;
                    _circle.LineWidthPt = 0;
                    break;
                case BubbleMaterial.Glossy:
                    AddGlint();
                    break;
                case BubbleMaterial.Glass:
                    _circle.Fill = _color.WithAlpha(0.5f);
                    _circle.Stroke = Colors.White(1, 0.6f);
                    _circle.LineWidthPt = 2;
                    AddGlint();
                    break;
                case BubbleMaterial.Neon:
                    _circle.Fill = _color.WithAlpha(0.9f);
                    _circle.Stroke = _color.Lightened(0.25f);
                    _circle.LineWidthPt = 2.5f;
                    ShapeView.Circle("Glow", transform, Vector2.zero, _radius + 4, _color.WithAlpha(0.28f), default, 0, Order - 1);
                    break;
            }
        }

        void AddGlint()
        {
            var g = ShapeView.Ellipse("Glint", transform, Units.P(-_radius * 0.28f, _radius * 0.34f),
                new Vector2(_radius * 0.7f, _radius * 0.42f), Colors.White(1, 0.4f), default, 0, Order + 1);
            g.transform.localRotation = Quaternion.Euler(0, 0, -0.5f * Mathf.Rad2Deg);
        }

        /// Bloc-obstacle inerte (case bloquée) : pierre pastel sans chiffre.
        public static ShapeView MakeObstacle(Transform parent, Vector2 localPos, float cellSizePt, int order = 0)
        {
            float side = cellSizePt * 0.66f;
            return ShapeView.Rect("Stone", parent, localPos, new Vector2(side, side), 13, Colors.White(0.80f),
                Colors.White(0.62f, 0.8f), 1.5f, order);
        }

        /// Fleur autonome (accueil, aperçus) : bulle `value` fleurie ×`power`, au rayon `radiusPt`.
        public static BubbleView MakeFlowerVisual(Transform parent, Vector2 localPos, int power, int value = 7,
            float radiusPt = 26f, int order = 20)
        {
            var b = Create(parent, value, localPos, order);
            b.SetBloom(power);
            b.transform.localScale = Vector3.one * (radiusPt / RadiusPt);
            return b;
        }

        // MARK: - Twists du défi du jour

        /// Bulle ancrée : ne tombe pas. Repère sobre = petit point fixe sous le chiffre.
        public void SetAnchored(bool anchored)
        {
            IsAnchored = anchored;
            if (!anchored) return;
            ShapeView.Circle("Anchor", transform, Units.P(0, -_radius + 12), 4, Colors.White(0.45f, 0.9f), default, 0, Order + 2);
        }

        /// Bulle gelée : voile glacé translucide + chiffre atténué.
        public void SetFrozen(bool frozen)
        {
            if (!frozen) return;
            IsFrozen = true;
            _frost = RenderShared.Node("Frost", transform);
            FxAlpha.Of(_frost.gameObject);
            ShapeView.Circle("Ice", _frost, Vector2.zero, _radius, new Color(0.82f, 0.90f, 0.98f, 0.55f),
                Colors.White(1, 0.7f), 2, Order + 1);
            _digit.Alpha = 0.45f;
        }

        /// Fait fondre le givre (un chemin adjacent a été validé).
        public void Thaw()
        {
            if (!IsFrozen) return;
            IsFrozen = false;
            var digit = _digit;
            Tw.Run(digit.gameObject, Tw.To(() => digit.Alpha, v => { if (digit != null) digit.Alpha = v; }, 1f, 0.3f));
            if (_frost != null)
            {
                var f = _frost;
                Tw.Run(f.gameObject, Tw.Seq(Tw.Par(Tw.FadeOut(f.gameObject, 0.3f), Tw.Scale(f, 1.25f, 0.3f)), Tw.Remove(f.gameObject)));
            }
            _frost = null;
        }

        // MARK: - Fleur de dix (graine éclose)

        /// Corolle de dix pétales et pastille « ×N ». Le chiffre reste au centre.
        public void SetBloom(int power)
        {
            if (power <= 1) return;
            Bloom = power;
            // Au-dessus des voisines : sinon leurs disques mangent les pétales.
            ShiftOrder(10);

            _corolla = RenderShared.Node("Corolla", transform);
            const int petals = 10;
            for (int i = 0; i < petals; i++)
            {
                float angle = i / (float)petals * Mathf.PI * 2f;
                float r = _radius + 3;
                var p = ShapeView.Ellipse("Petal", _corolla, Units.P(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r),
                    new Vector2(15, 24), BloomColor.WithAlpha(0.9f), BloomColor.Darkened(0.18f), 1, Order - 1);
                p.transform.localRotation = Quaternion.Euler(0, 0, (angle - Mathf.PI / 2f) * Mathf.Rad2Deg);
            }
            if (JuiceSettings.MotionEnabled)
                Tw.Run(_corolla.gameObject, Tw.Forever(Tw.RotateBy(_corolla, Mathf.PI * 2f, 24f)));

            var badge = RenderShared.Node("Badge", transform, Units.P(RadiusPt * 0.72f, RadiusPt * 0.72f));
            ShapeView.Circle("Disc", badge, Vector2.zero, 15, new Color(0.52f, 0.34f, 0.06f), Colors.White(1, 0.95f), 2, Order + 3);
            TenGoText.Create(badge, "×" + power, Fonts.AvenirHeavy, 14, Color.white, Vector2.zero, Order + 4);
        }

        /// Décale l'ordre d'affichage de tous les rendus de la bulle (`zPosition += 1`).
        public void ShiftOrder(int delta)
        {
            Order += delta;
            foreach (var s in GetComponentsInChildren<ShapeView>(true)) s.SortingOrder += delta;
            foreach (var t in GetComponentsInChildren<TMPro.TextMeshPro>(true)) t.sortingOrder += delta;
        }

        // MARK: - Sélection

        /// `tension` ∈ [0,1] = progression du chemin vers 10 : plus le joueur
        /// approche, plus la bulle enfle et respire vite.
        public void SetSelected(bool selected, float tension = 0f)
        {
            Tw.Kill(gameObject, "select");
            Tw.Kill(gameObject, "breathe");
            IsSelected = selected;
            if (selected)
            {
                _circle.Stroke = Colors.White(1, 0.9f);
                _circle.LineWidthPt = 2.5f;
                float t = Mathf.Clamp01(tension);
                float target = 1.08f + 0.06f * t;
                Tw.Run(gameObject, Tw.Scale(transform, target, 0.1f), "select");
                if (!JuiceSettings.MotionEnabled) return;
                float period = 0.9f - 0.45f * t;
                var breathe = Tw.Forever(Tw.Seq(Tw.Scale(transform, target + 0.03f, period / 2f), Tw.Scale(transform, target, period / 2f)));
                Tw.Run(gameObject, Tw.Seq(Tw.Wait(0.1f), breathe), "breathe");
            }
            else
            {
                // iOS remet le filet standard (assombri de 0,12, 1,5 pt) quelle que
                // soit la matière : reproduit tel quel.
                _circle.Stroke = _color.Darkened(0.12f);
                _circle.LineWidthPt = 1.5f;
                Tw.Run(gameObject, Tw.Scale(transform, 1f, 0.08f), "select");
            }
        }

        // MARK: - Souffle (voisines d'une chaîne validée)

        /// Poussée de (dx, dy) points puis retour, en positions absolues.
        public void Nudge(float dxPt, float dyPt)
        {
            if (Tw.Has(gameObject, "nudge")) return;
            var origin = _nudgeOrigin ?? transform.localPosition;
            _nudgeOrigin = origin;
            var o2 = (Vector2)origin;
            Tw.Run(gameObject, Tw.Seq(
                Tw.MoveTo(transform, o2 + Units.P(dxPt, dyPt), 0.06f).Ease(TwEase.EaseOut),
                Tw.MoveTo(transform, o2, 0.16f).Ease(TwEase.EaseOut),
                Tw.Call(() => _nudgeOrigin = null)), "nudge");
        }

        /// Interrompt un souffle en restaurant la position exacte (avant une chute).
        public void CancelNudge()
        {
            if (_nudgeOrigin == null) return;
            Tw.Kill(gameObject, "nudge");
            transform.localPosition = _nudgeOrigin.Value;
            _nudgeOrigin = null;
        }

        // MARK: - Animations

        /// Éclatement : gonfle, puis rétrécit en s'effaçant ; `completion` puis retrait.
        public void PlayPop(Action completion = null)
        {
            Tw.Kill(gameObject, "breathe");
            Tw.Run(gameObject, Tw.Seq(
                Tw.Scale(transform, 1.35f, 0.08f),
                Tw.Par(Tw.Scale(transform, 0f, 0.15f), Tw.FadeOut(gameObject, 0.15f)),
                Tw.Call(() => completion?.Invoke()),
                Tw.Remove(gameObject)));
        }

        /// Chute vers l'ordonnée locale `toY` (unités) en `duration` s, accélérée.
        /// `completion` tombe à l'instant d'arrivée ; l'écrasement se joue après.
        public void PlayFall(float toY, float duration, Action completion = null)
        {
            CancelNudge();
            Tw.Kill(gameObject, "breathe");
            if (JuiceSettings.MotionEnabled)
            {
                float stretch = Mathf.Min(0.1f, duration);
                Tw.Run(gameObject, Tw.Par(Tw.ScaleX(transform, 0.94f, stretch), Tw.ScaleY(transform, 1.08f, stretch)), "squash");
            }
            Tw.Run(gameObject, Tw.Seq(
                Tw.MoveToY(transform, toY, duration).Ease(TwEase.EaseIn),
                Tw.Call(() => { completion?.Invoke(); if (this != null) PlayLandingSquash(); })), "fall");
        }

        void PlayLandingSquash()
        {
            Tw.Kill(gameObject, "squash");
            if (!JuiceSettings.MotionEnabled) { transform.localScale = Vector3.one; return; }
            var t = transform;
            Tw.Run(gameObject, Tw.Seq(
                Tw.Par(Tw.ScaleX(t, 1.12f, 0.06f), Tw.ScaleY(t, 0.86f, 0.06f)),
                Tw.Par(Tw.ScaleX(t, 0.97f, 0.08f), Tw.ScaleY(t, 1.06f, 0.08f)),
                Tw.Par(Tw.ScaleX(t, 1f, 0.06f), Tw.ScaleY(t, 1f, 0.06f)),
                // Remise à 1 stricte : une bulle est réutilisée d'une chute à l'autre.
                Tw.Call(() => { if (t != null) t.localScale = Vector3.one; })), "squash");
        }
    }
}
