using System;
using System.Collections.Generic;
using TenGo.Fx;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Bulle de la grille (BubbleNode.swift) : disque de la couleur de sa valeur
    /// selon le thème, chiffre centré, matière cosmétique, et ses états
    /// (sélection qui respire, souffle, éclatement, chute avec écrasement,
    /// ancre, givre, fleur de graine). L'ordre d'affichage `Order` vaut la
    /// `zPosition` SpriteKit × 10.
    ///
    /// Refonte 4.0 (direction A, `design/refonte/01_partie_A`) : aplat sans
    /// contour, chiffre encre en Nunito Bold ; sélection = agrandissement 1,08
    /// cerclé d'un anneau crème puis d'un anneau encre de 3 pt ; gel = anneau
    /// blanc en pointillés ; ancre = point encre. Rayon de référence 39 unités
    /// (la partie passe le sien, calculé sur la largeur de l'écran).
    [DisallowMultipleComponent]
    public sealed class BubbleView : MonoBehaviour
    {
        public const float RadiusPt = 39f;
        public const float DigitSizePt = 36f;
        /// Chiffre : 47 % du diamètre (22 pt pour une bulle de 47 pt dans la maquette).
        public const float DigitRatio = 0.47f;

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
        /// Anneaux de sélection (crème puis encre), visibles quand la bulle est choisie.
        public Transform SelectionRing => _ring;
        public float Radius => _radius;

        ShapeView _circle;
        TenGoText _digit;
        Transform _frost;
        Transform _corolla;
        Transform _ring;
        float _radius;
        Color _color;
        Color _baseStroke;
        float _baseLineWidth;
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
            _circle = ShapeView.Circle("Circle", transform, Vector2.zero, radius, _color, default, 0, order);
            _digit = TenGoText.Create(transform, value.ToString(), NunitoWeight.Bold, DigitRatio * 2f * radius,
                Look.Digit, Vector2.zero, order + 2);
            ApplyMaterial();
            _baseStroke = _circle.Stroke;
            _baseLineWidth = _circle.LineWidthPt;
        }

        /// Matière de bulle (cosmétique, purement visuelle), revue pour l'aplat
        /// (refonte 4.0) : aucun dégradé ni ombre, le chiffre reste sur un fond
        /// opaque pour garder son contraste.
        /// - classique : l'aplat du thème ;
        /// - mate : teinte adoucie vers le fond, cerclée d'un filet ton sur ton ;
        /// - brillante : l'aplat et un reflet plat en haut à gauche ;
        /// - verre : aplat éclairci, liseré blanc et second anneau intérieur ;
        /// - néon : liseré clair et anneau plat extérieur.
        void ApplyMaterial()
        {
            switch (Material)
            {
                case BubbleMaterial.Matte:
                    _circle.Fill = Color.Lerp(_color, Look.Background, 0.18f);
                    _circle.Stroke = _color.Darkened(0.12f);
                    _circle.LineWidthPt = _radius * 0.06f;
                    break;
                case BubbleMaterial.Glossy:
                    AddGlint();
                    break;
                case BubbleMaterial.Glass:
                    _circle.Fill = _color.MixedWithWhite(0.3f);
                    _circle.Stroke = Colors.White(1, 0.9f);
                    _circle.LineWidthPt = _radius * 0.07f;
                    ShapeView.Circle("Inner", transform, Vector2.zero, _radius * 0.8f, Color.clear, Colors.White(1, 0.55f), _radius * 0.04f, Order + 1);
                    AddGlint();
                    break;
                case BubbleMaterial.Neon:
                    _circle.Stroke = _color.Lightened(0.3f);
                    _circle.LineWidthPt = _radius * 0.07f;
                    ShapeView.Circle("Glow", transform, Vector2.zero, _radius * 1.12f, Color.clear, _color.WithAlpha(0.45f), _radius * 0.06f, Order - 1);
                    break;
            }
        }

        void AddGlint()
        {
            var g = ShapeView.Ellipse("Glint", transform, Units.P(-_radius * 0.28f, _radius * 0.34f),
                new Vector2(_radius * 0.62f, _radius * 0.34f), Colors.White(1, 0.55f), default, 0, Order + 1);
            g.transform.localRotation = Quaternion.Euler(0, 0, -0.5f * Mathf.Rad2Deg);
        }

        /// Bloc-obstacle inerte (case bloquée) : carré arrondi pierre en aplat,
        /// 86 % du diamètre d'une bulle, sans chiffre.
        public static ShapeView MakeObstacle(Transform parent, Vector2 localPos, float cellSizePt, int order = 0,
            float bubbleDiameterPt = 0f)
        {
            if (bubbleDiameterPt <= 0f) bubbleDiameterPt = cellSizePt * (2f * RadiusPt / 84f);
            float side = bubbleDiameterPt * 0.86f;
            var fill = Look.Background.PerceivedLuminance() > 0.5f ? Design.Stone : Colors.White(0.80f);
            return ShapeView.Rect("Stone", parent, localPos, new Vector2(side, side), side * 0.35f, fill, default, 0, order);
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

        /// Bulle ancrée : ne tombe pas. Repère sobre = point encre fixe sous le chiffre.
        public void SetAnchored(bool anchored)
        {
            IsAnchored = anchored;
            if (!anchored) return;
            float r = _radius * (3f / 23.5f);
            ShapeView.Circle("Anchor", transform, Units.P(0, -_radius + _radius * (5f / 23.5f) + r), r,
                Look.Digit.WithAlpha(0.6f), default, 0, Order + 2);
        }

        /// Bulle gelée : anneau blanc en pointillés, rentré de 3 pt.
        public void SetFrozen(bool frozen)
        {
            if (!frozen) return;
            IsFrozen = true;
            _frost = RenderShared.Node("Frost", transform);
            FxAlpha.Of(_frost.gameObject);
            // Mesures de la maquette rapportées au rayon (bulle de 47 pt : retrait 3, trait 2,5).
            float k = _radius / 23.5f;
            float width = 2.5f * k, ringR = _radius - 3f * k - width / 2f;
            var pts = new List<Vector2>();
            const int n = StrokeView.MaxPoints - 1;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI * 2f / n;
                pts.Add(Units.P(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR));
            }
            var ring = StrokeView.Create("Ice", _frost, pts, Colors.White(1, 0.95f), width, Order + 1);
            ring.SetDash(2.6f * k, 3.4f * k);
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

            var badge = RenderShared.Node("Badge", transform, Units.P(_radius * 0.72f, _radius * 0.72f));
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
                ShowRing(true);
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
                ShowRing(false);
                _circle.Stroke = _baseStroke;
                _circle.LineWidthPt = _baseLineWidth;
                Tw.Run(gameObject, Tw.Scale(transform, 1f, 0.08f), "select");
            }
        }

        /// Anneau crème de 3 pt puis anneau encre de 3 pt autour du disque
        /// (`box-shadow: 0 0 0 3px bg, 0 0 0 6px ink`), au-dessus des voisines.
        void ShowRing(bool on)
        {
            if (!on) { if (_ring != null) _ring.gameObject.SetActive(false); return; }
            if (_ring == null)
            {
                _ring = RenderShared.Node("Ring", transform);
                float w = _radius * (3f / 23.5f);
                ShapeView.Circle("Cream", _ring, Vector2.zero, _radius + w / 2f, Color.clear, Look.Background, w, Order + 3);
                ShapeView.Circle("Ink", _ring, Vector2.zero, _radius + w * 1.5f, Color.clear, Design.Ink, w + 0.6f, Order + 3);
            }
            _ring.gameObject.SetActive(true);
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
