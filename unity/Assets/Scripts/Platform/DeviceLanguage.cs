using UnityEngine;

namespace TenGo.Platform
{
    /// Langue de l'appareil au format des codes iOS (`fr`, `pt-BR`, `zh-Hans`…).
    /// `Override` impose une langue (tests, captures).
    public static class DeviceLanguage
    {
        public static string Override;

        public static string Current()
        {
            if (!string.IsNullOrEmpty(Override)) return Override;
            return Application.systemLanguage switch
            {
                SystemLanguage.French => "fr",
                SystemLanguage.English => "en",
                SystemLanguage.Spanish => "es",
                SystemLanguage.German => "de",
                SystemLanguage.Italian => "it",
                SystemLanguage.Portuguese => "pt-BR",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Chinese or SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional => "zh-Hans",
                SystemLanguage.Dutch => "nl",
                _ => "en",
            };
        }
    }
}
