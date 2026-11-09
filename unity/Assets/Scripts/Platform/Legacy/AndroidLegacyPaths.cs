using UnityEngine;

namespace TenGo.Platform.Legacy
{
    /// Dossiers internes de l'app Android, obtenus par JNI. Attention :
    /// `Application.persistentDataPath` vaut `getExternalFilesDir()` ou
    /// `getFilesDir()` selon les réglages, et n'est donc pas un repère fiable
    /// pour le DataStore de l'app native, qui vit toujours dans `getFilesDir()`.
    public static class AndroidLegacyPaths
    {
        /// (`files/`, `shared_prefs/`) de l'app, ou null hors Android.
        public static (string filesDir, string sharedPrefsDir)? Resolve()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (var filesDir = context.Call<AndroidJavaObject>("getFilesDir"))
            using (var dataDir = context.Call<AndroidJavaObject>("getDataDir")) // API 24+, minimum Unity 26
            {
                string files = filesDir.Call<string>("getAbsolutePath");
                string data = dataDir.Call<string>("getAbsolutePath");
                return (files, System.IO.Path.Combine(data, "shared_prefs"));
            }
#else
            return null;
#endif
        }
    }
}
