using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Accès aux polices et conversion des tailles SpriteKit.
    public static class Fonts
    {
        public const string ResourcePath = "TenGoFonts";

        /// Taille TMP d'un `fontSize` SpriteKit : pour un `TextMeshPro` (3D), un
        /// cadratin vaut `fontSize` unités (mesuré sur les captures), et
        /// 1 unité = 100 points.
        public const float PtToTmp = 0.01f;

        /// Avenir Next du jeu iOS → Nunito. Nunito est plus ronde et plus
        /// grasse à graisse égale : on descend d'un cran (Medium 500 → SemiBold 600
        /// paraît équivalent sur les chiffres des bulles, vérifié en capture).
        public const NunitoWeight AvenirMedium = NunitoWeight.SemiBold;
        public const NunitoWeight AvenirDemiBold = NunitoWeight.Bold;
        public const NunitoWeight AvenirBold = NunitoWeight.ExtraBold;
        public const NunitoWeight AvenirHeavy = NunitoWeight.Black;

        static TenGoFontSet _set;

        public static TenGoFontSet Set
        {
            get
            {
                if (_set == null) _set = Resources.Load<TenGoFontSet>(ResourcePath);
                return _set;
            }
        }

        /// Police TMP de la graisse demandée (police TMP par défaut si absente).
        public static TMP_FontAsset Get(NunitoWeight weight)
        {
            var f = Set != null ? Set.Get(weight) : null;
            return f != null ? f : TMP_Settings.defaultFontAsset;
        }

        /// Ordonne les replis CJK selon la langue de l'interface : les idéogrammes
        /// communs prennent la forme chinoise en chinois, japonaise sinon.
        /// `language` : code iOS (`ja`, `ko`, `zh-Hans`…).
        public static void ApplyLanguage(string language)
        {
            var set = Set;
            if (set == null) return;
            var order = new List<TMP_FontAsset>();
            void Add(TMP_FontAsset f) { if (f != null && !order.Contains(f)) order.Add(f); }
            if (language != null && language.StartsWith("zh")) Add(set.chineseSimplified);
            if (language != null && language.StartsWith("ko")) Add(set.korean);
            Add(set.japanese); Add(set.korean); Add(set.chineseSimplified);
            foreach (var w in set.Weights())
                if (w != null) w.fallbackFontAssetTable = new List<TMP_FontAsset>(order);
        }
    }
}
