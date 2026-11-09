using UnityEngine;

namespace TenGo.Rendering
{
    /// Pièce vectorielle (CoinIcon.swift) pour le solde et les prix. Refonte 4.0
    /// (`coin()` de design/refonte/systeme.js) : disque or cerclé d'un filet plus
    /// foncé, sans liseré ni reflet.
    public static class CoinIcon
    {
        public static readonly Color Gold = Ui.Design.Gold;
        public static readonly Color Amber = Ui.Design.GoldRim;

        public static Transform Make(Transform parent, Vector2 pos, float radiusPt, int order)
        {
            var coin = RenderShared.Node("Coin", parent, pos);
            float rim = Mathf.Max(1f, radiusPt * 0.2f);
            ShapeView.Circle("Disc", coin, Vector2.zero, radiusPt - rim / 2f, Gold, Amber, rim, order);
            return coin;
        }
    }
}
