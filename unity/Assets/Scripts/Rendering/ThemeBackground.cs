using TenGo.Core;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Fond de chaque thème. Refonte 4.0 (direction A « plat crème ») : un
    /// aplat de la couleur du thème pour tous, sans les ambiances animées de
    /// ThemeBackground.swift (bulles floues, vagues, étoiles, feuilles,
    /// poussières, lune, bonbons), qui brouillaient la lecture de la grille.
    /// L'aplat est posé derrière le jeu (z −1), sur la surface de la scène.
    public static class ThemeBackground
    {
        public static Transform Make(Transform parent, Theme theme, Vector2 sizePt, System.Random rng = null, int order = Layers.Background)
        {
            var node = RenderShared.Node("ThemeBackground", parent);
            ShapeView.Rect("Flat", node, Vector2.zero, sizePt * 3f, 0, Colors.FromRgb(theme.Background), default, 0, order);
            return node;
        }
    }
}
