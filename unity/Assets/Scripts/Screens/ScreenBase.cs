using System;
using System.Collections.Generic;
using TenGo.App;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// Base des écrans hors partie (les `SKScene` de menu d'iOS) : racine,
    /// fond animé du thème, géométrie visible (aspectFill), cibles tactiles
    /// nommées (les `node.name` testés dans `touchesBegan`), barre d'onglets,
    /// page défilante facultative et surcouche modale (réglages).
    ///
    /// Les cibles se testent de la dernière ajoutée à la première (la plus
    /// haute d'abord). Écrans sans défilement : l'action part à l'appui, comme
    /// `touchesBegan` ; écrans défilants : au relâchement, et jamais après un
    /// glissé (`touchesEnded` de la boutique).
    public abstract class ScreenBase : IAppScreen
    {
        /// Cible tactile : un nœud, une zone (points, centrée sur le nœud), une action.
        public sealed class Target
        {
            public string Name;
            public Transform Node;
            public Vector2 SizePt;
            /// Décalage du centre de la zone par rapport au nœud (points).
            public Vector2 OffsetPt;
            public Action Action;
            /// Rebond `animateTap` (0,93 → 1) puis action 0,12 s plus tard.
            public bool Animate = true;
            /// Retour haptique léger (`tapFeedback`) ; faux pour les « Réclamer ».
            public bool Haptic = true;
            public bool Enabled = true;
            /// Cible posée dans la page défilante : ignorée hors de la zone visible.
            public bool InScroll;

            public bool Contains(Vector2 pt)
            {
                if (!Enabled || Node == null || !Node.gameObject.activeInHierarchy) return false;
                if (FxAlpha.Inherited(Node) < 0.05f) return false;
                Vector2 c = (Vector2)Node.position * Units.PointsPerUnit + OffsetPt;
                var s = Node.lossyScale;
                return Mathf.Abs(pt.x - c.x) <= SizePt.x * 0.5f * s.x && Mathf.Abs(pt.y - c.y) <= SizePt.y * 0.5f * s.y;
            }
        }

        protected readonly AppRoot App;
        protected AppServices S => App.Services;
        protected readonly List<Target> Targets = new();
        protected ScrollArea Scroll;
        protected IGameOverlay Overlay;
        /// Surcouche ouverte (réglages…), `null` sinon (tests).
        public IGameOverlay CurrentOverlay => Overlay != null && Overlay.IsOpen ? Overlay : null;
        /// Onglet porté par l'écran (null : pas de barre).
        protected Tab? SelectedTab;
        protected Transform TabBarNode;
        protected bool TapOnRelease;

        Target _pressed;
        Vector2 _downPt;
        bool _downOnTabBar;
        readonly string _name;

        public Transform Root { get; }
        public SceneGeometry Geo => App.Geometry;

        /// Largeur réellement visible (aspectFill rogne les côtés d'un téléphone haut).
        public float UsableW => Mathf.Min(Geo.VisibleWidth, SceneGeometry.RefWidth);
        public float UsableH => Mathf.Min(Geo.VisibleHeight, SceneGeometry.RefHeight);
        public float HalfH => UsableH / 2f;
        /// `max(safeAreaInsets.top, 47) / scale`.
        public float TopInset => Geo.TopInset;
        /// `max(safeAreaInsets.bottom, 20) / scale`.
        public float BottomInset => Mathf.Max(Geo.SafeBottomPx, 20f * Geo.PixelsPerPoint) / Geo.PixelsPerScene;
        /// Rangée du haut des écrans de premier niveau (`halfH - topInset - 26`).
        public float TopRowY => HalfH - TopInset - 26f;
        public Vector2 SceneSize => new(750, 1334);

        protected ScreenBase(AppRoot app, string name, bool themedBackground = true)
        {
            App = app;
            _name = name;
            Root = new GameObject(name).transform;
            FxAlpha.Of(Root.gameObject);
            if (themedBackground)
            {
                app.SetBackground(Look.Background);
                ThemeBackground.Make(Root, Look.Theme, SceneSize);
            }
        }

        // MARK: - Cibles

        protected Target AddTarget(string name, Transform node, Vector2 sizePt, Action action, bool animate = true,
            bool haptic = true, Vector2 offsetPt = default)
        {
            var t = new Target { Name = name, Node = node, SizePt = sizePt, Action = action, Animate = animate, Haptic = haptic,
                                 OffsetPt = offsetPt, InScroll = Scroll != null && node != null && node.IsChildOf(Scroll.Content) };
            Targets.Add(t);
            return t;
        }

        protected void RemoveTargets(Predicate<Target> match) => Targets.RemoveAll(match);

        /// Cible nommée (tests, pilote automatique).
        public Target FindTarget(string name)
        {
            for (int i = Targets.Count - 1; i >= 0; i--) if (Targets[i].Name == name) return Targets[i];
            return null;
        }

        /// Centre (points de scène) de la cible nommée, `null` si absente.
        public Vector2? TargetPoint(string name)
        {
            var t = FindTarget(name);
            if (t == null || t.Node == null) return null;
            return (Vector2)t.Node.position * Units.PointsPerUnit + t.OffsetPt;
        }

        public IEnumerable<string> TargetNames() { foreach (var t in Targets) yield return t.Name; }

        Target HitTarget(Vector2 pt)
        {
            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                var t = Targets[i];
                if (t.InScroll && Scroll != null && !Scroll.ViewportContains(pt)) continue;
                if (t.Contains(pt)) return t;
            }
            return null;
        }

        /// `animateTap` : 0,93 en 0,07 s puis 1 en 0,12 s.
        protected static void AnimateTap(Transform node)
        {
            if (node == null) return;
            var s0 = node.localScale.x;
            Tw.Run(node.gameObject, Tw.Seq(Tw.Scale(node, s0 * 0.93f, 0.07f), Tw.Scale(node, s0, 0.12f)), "tap");
        }

        /// Petit tremblement horizontal (achat refusé, pub indisponible).
        protected static void Shake(Transform node, float amp = 7f)
        {
            if (node == null) return;
            Tw.Kill(node.gameObject, "shake");
            Tw.Run(node.gameObject, Tw.Seq(
                Tw.MoveBy(node, Units.P(amp, 0), 0.05f), Tw.MoveBy(node, Units.P(-amp * 2, 0), 0.05f),
                Tw.MoveBy(node, Units.P(amp * 2, 0), 0.05f), Tw.MoveBy(node, Units.P(-amp, 0), 0.05f)), "shake");
        }

        /// `run(.wait(d)) { … }` sur la racine de l'écran.
        protected void After(float seconds, Action action) =>
            Tw.Run(Root.gameObject, Tw.Seq(Tw.Wait(seconds), Tw.Call(action)));

        protected void Fire(Target t)
        {
            if (t.Haptic) TapFeedback.Play();
            if (t.Animate)
            {
                AnimateTap(t.Node);
                After(0.12f, () => t.Action?.Invoke());
            }
            else t.Action?.Invoke();
        }

        /// Tap simulé sur une cible nommée (tests) ; vrai si elle existe.
        public bool TapTarget(string name)
        {
            var t = FindTarget(name);
            if (t == null) return false;
            Fire(t);
            return true;
        }

        // MARK: - Barre d'onglets

        protected void AddTabBar(Tab selected)
        {
            SelectedTab = selected;
            TabBarNode = TabBar.Make(Root, UsableW, selected, -HalfH + BottomInset + TabBar.Height / 2f);
        }

        /// Hauteur occupée en bas par la barre d'onglets.
        protected float TabBarTop => -HalfH + BottomInset + TabBar.Height;

        protected virtual void OnTab(Tab tab)
        {
            if (tab == SelectedTab) return;
            App.ShowTab(tab);
        }

        // MARK: - Entrée

        public virtual void PointerDown(Vector2 pt)
        {
            _downPt = pt;
            _pressed = null;
            _downOnTabBar = false;
            if (Overlay != null)
            {
                if (Overlay.IsOpen) { Overlay.HandleTouch(pt); App.FlushSave(); return; }
                Overlay = null;
            }
            if (InterceptDown(pt)) return;
            Scroll?.Begin(pt);
            var hit = HitTarget(pt);
            if (hit == null && TabBarNode != null && TabBar.TabAt(TabBarNode, pt) != null) { _downOnTabBar = true; return; }
            if (hit == null) { OnEmptyTap(pt); return; }
            if (TapOnRelease) { _pressed = hit; return; }
            Fire(hit);
        }

        public virtual void PointerMove(Vector2 pt)
        {
            if (Scroll != null && !_downOnTabBar) Scroll.Drag(pt);
        }

        public virtual void PointerUp(Vector2 pt)
        {
            bool dragged = Scroll != null && Scroll.IsDragging;
            Scroll?.End(pt);
            if (_downOnTabBar)
            {
                _downOnTabBar = false;
                var tab = TabBar.TabAt(TabBarNode, pt);
                if (tab != null) OnTab(tab.Value);
                return;
            }
            var p = _pressed;
            _pressed = null;
            if (p == null || dragged) return;
            if (p.Contains(pt) || p.Contains(_downPt)) Fire(p);
        }

        public virtual void PointerCancel()
        {
            _pressed = null;
            _downOnTabBar = false;
            Scroll?.Cancel();
        }

        /// Toucher prioritaire (coach-mark…) ; vrai = consommé.
        protected virtual bool InterceptDown(Vector2 pt) => false;
        /// Appui hors de toute cible.
        protected virtual void OnEmptyTap(Vector2 pt) { }

        // MARK: - Cycle de vie

        public virtual void Tick(float dt) => Scroll?.Tick(dt);

        public virtual void Back()
        {
            if (Overlay != null && Overlay.IsOpen) { Overlay.Close(); return; }
            App.ShowMenu();
        }

        public virtual void AppPaused() { }
        public virtual void GeometryChanged(SceneGeometry g) { }
        public virtual void Dispose() { }

        public override string ToString() => _name;
    }
}
