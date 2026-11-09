using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TenGo.Editor
{
    /// Réglages des SDK natifs (étape 7), appelés par `ProjectSetup.Apply` et
    /// avant chaque build (`IPreprocessBuildWithReport`) :
    /// - symboles TENGO_ADMOB / TENGO_FIREBASE / TENGO_IAP pour Android et iOS ;
    /// - identifiants d'app AdMob dans les réglages du plugin Google Mobile Ads ;
    /// - gabarits Gradle (EDM y écrit les dépendances Android) ;
    /// - fichiers Firebase copiés depuis les apps natives (`FirebaseConfig`).
    public static class NativeSetup
    {
        public static readonly string[] Defines = { "TENGO_ADMOB", "TENGO_FIREBASE", "TENGO_IAP" };
        const string PluginDir = "Assets/Plugins/Android";
        static readonly string[] Templates = { "mainTemplate.gradle", "gradleTemplate.properties", "settingsTemplate.gradle" };

        [MenuItem("TenGo/Configurer les SDK natifs")]
        public static void Apply()
        {
            ApplyDefines();
            SetAdMobAppIds();
            EnsureGradleTemplates();
            FirebaseConfig.Copy();
            AssetDatabase.SaveAssets();
            Debug.Log("[NativeSetup] SDK configurés : " + string.Join(";", Defines));
        }

        /// Ajoute les symboles aux cibles Android et iOS (sans toucher aux autres).
        public static void ApplyDefines()
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var current = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (var d in Defines) if (!current.Contains(d)) current.Add(d);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", current));
            }
        }

        /// `GoogleMobileAdsSettings` par réflexion (assembly d'éditeur du plugin).
        public static void SetAdMobAppIds()
        {
            var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (type == null) { Debug.LogWarning("[NativeSetup] Plugin Google Mobile Ads absent."); return; }
            var settings = (ScriptableObject)type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(null, null);
            type.GetProperty("GoogleMobileAdsAndroidAppId")?.SetValue(settings, Services.AdUnits.AndroidAppId);
            type.GetProperty("GoogleMobileAdsIOSAppId")?.SetValue(settings, Services.AdUnits.IosAppId);
            EditorUtility.SetDirty(settings);
        }

        /// Gabarits Gradle personnalisés (copiés de l'installation d'Unity) :
        /// EDM y inscrit les dépendances (mainTemplate) et les dépôts (settingsTemplate).
        public static void EnsureGradleTemplates()
        {
            var source = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools", "GradleTemplates");
            if (!Directory.Exists(source)) { Debug.LogWarning("[NativeSetup] Module Android absent : gabarits Gradle non copiés."); return; }
            Directory.CreateDirectory(PluginDir);
            bool added = false;
            foreach (var name in Templates)
            {
                var dest = Path.Combine(PluginDir, name);
                if (File.Exists(dest)) continue;
                File.Copy(Path.Combine(source, name), dest);
                added = true;
            }
            var props = Path.Combine(PluginDir, "gradleTemplate.properties");
            if (!File.ReadAllText(props).Contains("android.useAndroidX"))
                File.AppendAllText(props, "\nandroid.useAndroidX=true\nandroid.enableJetifier=true\n");
            if (added) AssetDatabase.Refresh();
        }

        /// Résolution des dépendances Android par l'External Dependency Manager.
        public static bool ResolveAndroid()
        {
            var type = Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
            var method = type?.GetMethod("ResolveSync", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool) }, null);
            if (method == null) { Debug.LogWarning("[NativeSetup] External Dependency Manager introuvable."); return false; }
            bool ok = (bool)method.Invoke(null, new object[] { true });
            Debug.Log("[NativeSetup] Résolution des dépendances Android : " + (ok ? "OK" : "ÉCHEC"));
            return ok;
        }
    }

    /// Avant chaque build Android ou iOS : symboles, identifiants AdMob et
    /// fichiers Firebase à jour (les fichiers Firebase ne sont jamais versionnés
    /// dans unity/ : ils viennent de ../ios et ../android à chaque build).
    public sealed class NativePreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            var platform = report.summary.platform;
            if (platform != BuildTarget.Android && platform != BuildTarget.iOS) return;
            NativeSetup.SetAdMobAppIds();
            if (!FirebaseConfig.Copy())
                Debug.LogWarning("[NativeSetup] Fichiers Firebase absents : Firebase restera indisponible dans cette build.");
        }
    }
}
