using UnityEngine;

namespace TenGo
{
    /// Couleurs du thème « default » du jeu iOS (ThemeManager.swift).
    /// Provisoire : remplacé par le catalogue des thèmes de Core à l'étape 4.
    public static class Palette
    {
        /// Fond crème (0,97 ; 0,95 ; 0,92).
        public static readonly Color Background = Rgb(0.97f, 0.95f, 0.92f);
        /// Chiffres des bulles (0,25).
        public static readonly Color Digit = Rgb(0.25f, 0.25f, 0.25f);
        /// Bulles 1 à 9, dans l'ordre des valeurs.
        public static readonly Color[] Bubbles =
        {
            Rgb(1.00f, 0.62f, 0.62f), Rgb(1.00f, 0.78f, 0.62f), Rgb(1.00f, 0.96f, 0.62f),
            Rgb(0.67f, 0.95f, 0.75f), Rgb(0.62f, 0.86f, 1.00f), Rgb(0.80f, 0.72f, 1.00f),
            Rgb(1.00f, 0.72f, 0.86f), Rgb(0.75f, 0.91f, 0.75f), Rgb(0.72f, 0.76f, 1.00f),
        };
        /// Corail des tests de rendu (bulle 1).
        public static Color Coral => Bubbles[0];

        public static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
    }
}
