using UnityEngine;

namespace TenGo
{
    /// Couleurs du thème « default » de la refonte 4.0 (`design/refonte/systeme.css`),
    /// pour ce qui vit hors du thème actif (écran de démarrage, tests de rendu).
    public static class Palette
    {
        /// Fond crème #F7F3EC.
        public static readonly Color Background = Ui.Design.BgHex;
        /// Chiffres des bulles, encre #34312C.
        public static readonly Color Digit = Ui.Design.InkHex;
        /// Bulles 1 à 9, dans l'ordre des valeurs.
        public static readonly Color[] Bubbles = System.Array.ConvertAll(Ui.Design.BubblePaletteHex, h => Ui.Design.Hex(h));
        /// Bulle 1 des tests de rendu.
        public static Color Coral => Bubbles[0];

        public static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
    }
}
