using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TenGo.Editor
{
    /// Export du projet Xcode (build de développement) dans Builds/iOS :
    /// Tools/unity.sh TenGo.Editor.BuildIos.Run -buildTarget iOS
    /// L'EDM écrit le Podfile (Firebase, Google Mobile Ads et adaptateurs) et
    /// lance `pod install` ; ouvrir ensuite Builds/iOS/Unity-iPhone.xcworkspace.
    /// Aucun envoi : la signature et l'archivage restent manuels.
    public static class BuildIos
    {
        public const string Output = "Builds/iOS";

        public static void Run()
        {
            MainScene.Ensure();
            NativeSetup.Apply();
            Directory.CreateDirectory(Output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene.Path },
                locationPathName = Output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Export iOS échoué : " + report.summary.result);
            bool pods = File.Exists(Path.Combine(Output, "Podfile"));
            bool workspace = Directory.Exists(Path.Combine(Output, "Unity-iPhone.xcworkspace"));
            Debug.Log($"[BuildIos] {Output} exporté — Podfile {(pods ? "présent" : "absent")}, workspace {(workspace ? "présent" : "absent")}");
        }
    }
}
