using TenGo.Core;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// Onglets de premier niveau (TabBar.swift `Tab`).
    public enum Tab { Play, Progress, Social, Shop }

    /// Navigation principale (TabBar.swift), refaite pour la 4.0 (direction A,
    /// `design/refonte/02_accueil`) : barre pleine largeur posée au bas de
    /// l'écran, fond du thème et filet au-dessus (elle ne flotte plus sur le
    /// contenu), quatre onglets libellés (Jouer, Profil, Duel, Boutique) ;
    /// l'onglet actif est à l'encre dans une pilule enfoncée. Le découpage du
    /// toucher est géométrique (parts égales). Les onglets s'ouvrent sans
    /// transition (`TabBar.present`).
    public static class TabBar
    {
        /// Hauteur réservée par les scènes hôtes au-dessus de la marge basse (unités de scène).
        public static float Height => Design.Pt(ContentPt);
        public const string NodeName = "tabBar";
        /// Contenu : marge 8 + pilule 30 + écart 4 + libellé 15 + marge 5 (points de maquette).
        const float ContentPt = 62f;
        public static readonly Tab[] All = { Tab.Play, Tab.Progress, Tab.Social, Tab.Shop };
        /// Ordre d'affichage (`zPosition` 20).
        public const int Order = 200;

        public static VectorIconName Icon(Tab t) => t switch
        {
            Tab.Play => VectorIconName.LinePlay,
            Tab.Progress => VectorIconName.LineProfile,
            Tab.Social => VectorIconName.LineDuel,
            _ => VectorIconName.LineShop,
        };

        public static string TitleKey(Tab t) => "tab." + t.ToString().ToLowerInvariant();

        /// Libellé affiché sous l'icône (Jouer, Profil, Duel, Boutique).
        public static string Label(Tab t) => t switch
        {
            Tab.Play => Loc.T("tab.play"),
            Tab.Progress => Loc.T("profile.title"),
            Tab.Social => Loc.T("menu.duel"),
            _ => Loc.T("tab.shop"),
        };

        /// Construit la barre ; `width` = largeur réellement visible, `y` = centre du contenu.
        public static Transform Make(Transform parent, float width, Tab selected, float y)
        {
            float h = Height;
            var bar = RenderShared.Node(NodeName, parent, Units.P(0, y));
            bar.gameObject.AddComponent<TabBarInfo>().CapsuleWidth = width;

            // Fond opaque jusqu'au bas de l'écran (marge des gestes comprise) et filet au-dessus.
            const float below = 400f;
            float bgH = h + below;
            ShapeView.Rect("Bg", bar, Units.P(0, h / 2 - bgH / 2), new Vector2(width + 40, bgH), 0, Design.Bg, default, 0, Order - 2);
            ShapeView.Rect("Line", bar, Units.P(0, h / 2), new Vector2(width + 40, Design.Pt(1)), 0, Design.Line, default, 0, Order - 1);

            float slot = width / All.Length;
            float top = h / 2;
            float pillY = top - Design.Pt(8 + 15);
            float labelY = top - Design.Pt(8 + 30 + 4 + 7);
            for (int i = 0; i < All.Length; i++)
            {
                var tab = All[i];
                bool isSelected = tab == selected;
                float x = -width / 2 + slot / 2 + i * slot;
                var item = RenderShared.Node("tab_" + tab.ToString().ToLowerInvariant(), bar, Units.P(x, pillY));
                var tint = isSelected ? Design.Ink : Design.Ink3;
                if (isSelected)
                    ShapeView.Rect("Pill", item, Vector2.zero, Design.Pt(56, 30), Design.Pt(15), Design.Sunk, default, 0, Order + 1);
                VectorIcon.Create(item, Icon(tab), Vector2.zero, Design.Pt(24), tint, Order + 3);
                TenGoText.Create(item, Label(tab), NunitoWeight.ExtraBold, Design.Pt(11), tint, Units.P(0, labelY - pillY), Order + 3)
                    .ShrinkToFit(slot - Design.Pt(6));
            }
            return bar;
        }

        /// Onglet sous le point (points de scène), s'il y en a un.
        public static Tab? TabAt(Transform bar, Vector2 pt)
        {
            if (bar == null) return null;
            var info = bar.GetComponent<TabBarInfo>();
            if (info == null) return null;
            Vector2 local = pt - (Vector2)bar.position * Units.PointsPerUnit;
            float w = info.CapsuleWidth;
            if (Mathf.Abs(local.y) > Height / 2 || Mathf.Abs(local.x) > w / 2) return null;
            int index = Mathf.FloorToInt((local.x + w / 2) / (w / All.Length));
            return All[Mathf.Clamp(index, 0, All.Length - 1)];
        }

        /// Centre (points de scène) d'un onglet (tests, coach-mark).
        public static Vector2 TabCenter(Transform bar, Tab tab)
        {
            var item = bar.Find("tab_" + tab.ToString().ToLowerInvariant());
            return (Vector2)item.position * Units.PointsPerUnit;
        }
    }

    /// Géométrie mémorisée de la barre (`userData["width"]`) : largeur des onglets.
    public sealed class TabBarInfo : MonoBehaviour
    {
        public float CapsuleWidth;
    }
}
