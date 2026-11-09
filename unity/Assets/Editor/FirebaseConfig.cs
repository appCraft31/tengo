using System.IO;
using UnityEditor;
using UnityEngine;

namespace TenGo.Editor
{
    /// Fichiers de configuration Firebase (projet tengo-b62b1), repris des
    /// apps natives — la même app Firebase, donc les mêmes données :
    /// - `../ios/tenGO/GoogleService-Info.plist` → `Assets/GoogleService-Info.plist`
    /// - `../android/app/google-services.json` → `Assets/google-services.json`
    /// Ignorés par git (unity/.gitignore, comme le module FirebaseApp.androidlib
    /// que le plugin en génère) : ne jamais les committer. Appelé par
    /// `NativeSetup.Apply` et avant chaque build (`NativePreBuild`).
    public static class FirebaseConfig
    {
        public const string IosSource = "../ios/tenGO/GoogleService-Info.plist";
        public const string AndroidSource = "../android/app/google-services.json";
        public const string IosDest = "Assets/GoogleService-Info.plist";
        public const string AndroidDest = "Assets/google-services.json";

        [MenuItem("TenGo/Copier la configuration Firebase")]
        public static void CopyMenu() => Copy();

        /// Vrai si les deux fichiers sont en place.
        public static bool Copy()
        {
            bool ios = CopyOne(IosSource, IosDest);
            bool android = CopyOne(AndroidSource, AndroidDest);
            return ios && android;
        }

        static bool CopyOne(string source, string dest)
        {
            var src = Path.GetFullPath(source);
            if (!File.Exists(src))
            {
                Debug.LogWarning("[Firebase] Source introuvable : " + src);
                return File.Exists(dest);
            }
            if (File.Exists(dest) && File.ReadAllText(dest) == File.ReadAllText(src)) return true;
            File.Copy(src, dest, true);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Firebase] Copié : " + dest);
            return true;
        }
    }
}
