using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TenGo.Editor
{
    /// APK de développement, à installer sur l'émulateur UNIQUEMENT :
    /// Tools/unity.sh TenGo.Editor.BuildAndroidDev.Run -buildTarget Android
    /// Identifiant distinct de la production (`com.appcraft31.tengo.unitydev`),
    /// rétabli à la fin : la version Play ne peut pas être écrasée par erreur.
    public static class BuildAndroidDev
    {
        public const string DevId = "com.appcraft31.tengo.unitydev";
        public const string Output = "Builds/Android/tengo-unitydev.apk";

        public static void Run()
        {
            string previousId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousName = PlayerSettings.productName;
            bool previousAab = EditorUserBuildSettings.buildAppBundle;
            try
            {
                MainScene.Ensure();
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DevId);
                PlayerSettings.productName = "tenGO dev";
                EditorUserBuildSettings.buildAppBundle = false;
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainScene.Path },
                    locationPathName = Output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new System.Exception("Build Android échoué : " + report.summary.result);
                Debug.Log($"[BuildAndroidDev] {Output} ({report.summary.totalSize / 1048576f:0.0} Mo, {DevId})");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousId);
                PlayerSettings.productName = previousName;
                EditorUserBuildSettings.buildAppBundle = previousAab;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
