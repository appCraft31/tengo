using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// Onglets de premier niveau (TabBar.swift `Tab`).
    public enum Tab { Play, Progress, Social, Shop }

    /// Navigation principale (TabBar.swift) : capsule flottante au bas des
    /// quatre écrans de premier niveau, icônes seules, onglet actif en pilule
    /// accentuée. Le découpage du toucher est géométrique (parts égales).
    /// Les onglets s'ouvrent sans transition (`TabBar.present`).
    public static class TabBar
    {
        /// Hauteur réservée par les scènes hôtes (capsule 72 + marges).
        public const float Height = 86f;
        public const string NodeName = "tabBar";
        const float CapsuleHeight = 72f;
        const float SideInset = 22f;
        const float Padding = 7f;
        static float CapsuleY => (Height - CapsuleHeight) / 2f;
        public static readonly Tab[] All = { Tab.Play, Tab.Progress, Tab.Social, Tab.Shop };
        /// Ordre d'affichage (`zPosition` 20).
        public const int Order = 200;

        public static VectorIconName Icon(Tab t) => t switch
        {
            Tab.Play => VectorIconName.IconPlay,
            Tab.Progress => VectorIconName.IconProgress,
            Tab.Social => VectorIconName.IconSocial,
            _ => VectorIconName.IconShop,
        };

        public static string TitleKey(Tab t) => "tab." + t.ToString().ToLowerInvariant();

        /// Construit la barre ; `width` = largeur réellement visible, `y` = centre du nœud.
        public static Transform Make(Transform parent, float width, Tab selected, float y)
        {
            var theme = Look.Theme;
            float capsuleW = width - SideInset * 2;
            var bar = RenderShared.Node(NodeName, parent, Units.P(0, y));
            bar.gameObject.AddComponent<TabBarInfo>().CapsuleWidth = capsuleW;

            // Ombre portée simulée.
            ShapeView.Rect("Shadow", bar, Units.P(0, CapsuleY - 5), new Vector2(capsuleW, CapsuleHeight), CapsuleHeight / 2,
                Colors.White(0, 0.10f), default, 0, Order - 4);
            var bg = Look.Background;
            float lift = bg.PerceivedLuminance() > 0.5f ? 0.78f : 0.14f;
            var capsule = ShapeView.Rect("Capsule", bar, Units.P(0, CapsuleY), new Vector2(capsuleW, CapsuleHeight), CapsuleHeight / 2,
                bg.MixedWithWhite(lift), Look.Logo.WithAlpha(0.12f), 1, Order);
            Relief.Raise(capsule, 5);

            float inner = capsuleW - Padding * 2;
            float slot = inner / All.Length;
            float pillH = CapsuleHeight - Padding * 2;
            for (int i = 0; i < All.Length; i++)
            {
                var tab = All[i];
                bool isSelected = tab == selected;
                float x = -inner / 2 + slot / 2 + i * slot;
                var item = RenderShared.Node("tab_" + tab.ToString().ToLowerInvariant(), bar, Units.P(x, CapsuleY));
                Color tint;
                if (isSelected)
                {
                    var accent = Colors.FromRgb(theme.ColorForValue(4));
                    var pill = ShapeView.Rect("Pill", item, Vector2.zero, new Vector2(slot, pillH), pillH / 2, accent, default, 0, Order + 3);
                    Relief.Raise(pill, 3);
                    tint = accent.ReadableInk();
                }
                else tint = Look.Logo.WithAlpha(0.55f);
                VectorIcon.Create(item, Icon(tab), Vector2.zero, 32, tint, Order + 6);
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
            if (Mathf.Abs(local.y - CapsuleY) > CapsuleHeight / 2 || Mathf.Abs(local.x) > w / 2) return null;
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

    /// Géométrie mémorisée de la barre (`userData["width"]`).
    public sealed class TabBarInfo : MonoBehaviour
    {
        public float CapsuleWidth;
    }
}
