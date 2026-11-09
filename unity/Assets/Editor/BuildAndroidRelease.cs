using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TenGo.Editor
{
    /// AAB de production signé, pour Google Play :
    /// Tools/unity.sh TenGo.Editor.BuildAndroidRelease.Run -buildTarget Android
    /// Identifiant `com.appcraft31.tengo`, version et versionCode de
    /// `ProjectSetup`, IL2CPP ARM64, build non développement, symboles natifs
    /// inclus dans le bundle. Signé avec la clé d'upload de l'app native
    /// (`../android/tengo-upload.jks`), dont les mots de passe sont lus dans
    /// `../android/keystore.properties` au moment du build, gardés en mémoire
    /// seulement et effacés des réglages à la fin (rien ne doit finir dans
    /// ProjectSettings.asset ni dans le dépôt).
    public static class BuildAndroidRelease
    {
        public static readonly string Output = $"Builds/Android/tengo-{ProjectSetup.Version}-{ProjectSetup.AndroidVersionCode}.aab";
        const string KeystoreProperties = "../android/keystore.properties";

        public static void Run()
        {
            var signing = ReadSigning();
            bool previousDev = EditorUserBuildSettings.development;
            try
            {
                ProjectSetup.Apply(); // réglages du Player + NativeSetup (symboles TENGO_*, Firebase de prod)
                MainScene.Ensure();
                if (!NativeSetup.ResolveAndroid()) throw new System.Exception("Résolution des dépendances Android échouée");

                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ProjectSetup.AndroidId);
                PlayerSettings.bundleVersion = ProjectSetup.Version;
                PlayerSettings.Android.bundleVersionCode = ProjectSetup.AndroidVersionCode;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;

                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = signing.storeFile;
                PlayerSettings.Android.keystorePass = signing.storePassword;
                PlayerSettings.Android.keyaliasName = signing.keyAlias;
                PlayerSettings.Android.keyaliasPass = signing.keyPassword;

                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene.Path },
                    locationPathName = Output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new System.Exception("Build Android échoué : " + report.summary.result);
                Debug.Log($"[BuildAndroidRelease] {Output} ({new FileInfo(Output).Length / 1048576f:0.0} Mo, " +
                          $"{ProjectSetup.AndroidId} {ProjectSetup.Version} ({ProjectSetup.AndroidVersionCode}))");
            }
            finally
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.development = previousDev;
                AssetDatabase.SaveAssets();
            }
        }

        struct Signing { public string storeFile, storePassword, keyAlias, keyPassword; }

        /// Lit keystore.properties (clé=valeur) ; le chemin du keystore est
        /// résolu depuis le dossier `../android` s'il est relatif.
        static Signing ReadSigning()
        {
            string androidDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "android"));
            string file = Path.GetFullPath(KeystoreProperties);
            if (!File.Exists(file)) throw new System.Exception("keystore.properties introuvable dans " + androidDir);
            var values = new Dictionary<string, string>();
            foreach (var raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            foreach (var key in new[] { "storeFile", "storePassword", "keyAlias", "keyPassword" })
                if (!values.ContainsKey(key) || values[key].Length == 0)
                    throw new System.Exception("keystore.properties : " + key + " manquant");
            string store = values["storeFile"];
            if (!Path.IsPathRooted(store)) store = Path.GetFullPath(Path.Combine(androidDir, store));
            if (!File.Exists(store)) throw new System.Exception("Keystore introuvable (storeFile)");
            return new Signing
            {
                storeFile = store,
                storePassword = values["storePassword"],
                keyAlias = values["keyAlias"],
                keyPassword = values["keyPassword"],
            };
        }
    }
}
