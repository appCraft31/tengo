#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TenGo.Platform.Legacy
{
    /// Pont vers `Plugins/iOS/TenGoLegacy.mm` : lecture des UserDefaults de
    /// l'app iOS native (clés du jeu uniquement), en JSON typé.
    public static class IosLegacyDefaults
    {
#if UNITY_IOS && !UNITY_EDITOR
        /// Chaîne allouée par `strdup` côté natif ; le marshalling de Mono la libère.
        [DllImport("__Internal")]
        private static extern string TenGoLegacy_ReadDefaults(string requestJson);
#endif

        /// Réponse du greffon, ou null hors iOS.
        public static string Read(string requestJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return TenGoLegacy_ReadDefaults(requestJson);
#else
            return null;
#endif
        }
    }
}
