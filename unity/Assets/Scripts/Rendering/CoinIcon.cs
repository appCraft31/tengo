using UnityEngine;

namespace TenGo.Rendering
{
    /// Pièce vectorielle (CoinIcon.swift) pour le solde et les prix : disque or
    /// cerclé d'ambre, liseré intérieur, reflet.
    public static class CoinIcon
    {
        public static readonly Color Gold = new(0.98f, 0.84f, 0.46f);
        public static readonly Color Amber = new(0.84f, 0.64f, 0.28f);

        public static Transform Make(Transform parent, Vector2 pos, float radiusPt, int order)
        {
            var coin = RenderShared.Node("Coin", parent, pos);
            float r = radiusPt;
            ShapeView.Circle("Disc", coin, Vector2.zero, r, Gold, Amber, Mathf.Max(1, r * 0.14f), order);
            ShapeView.Circle("Rim", coin, Vector2.zero, r - Mathf.Max(2, r * 0.3f), Color.clear, Amber.WithAlpha(0.5f), 1, order + 1);
            ShapeView.Circle("Glint", coin, Units.P(-r * 0.32f, r * 0.32f), r * 0.28f, Colors.White(1, 0.55f), default, 0, order + 2);
            return coin;
        }
    }
}
