using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Les 5 polices TMP de Nunito et leurs replis CJK (Noto Sans JP, KR, SC),
    /// chargés par Resources (`Assets/Fonts/Resources/TenGoFonts.asset`, créé par
    /// `TenGo.Editor.ProjectSetup.Apply`).
    [CreateAssetMenu(menuName = "TenGo/Jeu de polices")]
    public sealed class TenGoFontSet : ScriptableObject
    {
        public TMP_FontAsset regular;
        public TMP_FontAsset semiBold;
        public TMP_FontAsset bold;
        public TMP_FontAsset extraBold;
        public TMP_FontAsset black;
        /// Replis : japonais, coréen, chinois simplifié (ordre par défaut).
        public TMP_FontAsset japanese;
        public TMP_FontAsset korean;
        public TMP_FontAsset chineseSimplified;

        public TMP_FontAsset Get(NunitoWeight w) => w switch
        {
            NunitoWeight.Regular => regular,
            NunitoWeight.SemiBold => semiBold,
            NunitoWeight.ExtraBold => extraBold,
            NunitoWeight.Black => black,
            _ => bold,
        };

        public IEnumerable<TMP_FontAsset> Weights()
        {
            yield return regular; yield return semiBold; yield return bold; yield return extraBold; yield return black;
        }
    }
}
