using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace TenGo.Editor
{
    /// Projet Xcode exporté : réglages de l'Info.plist et des entitlements de
    /// l'app iOS 3.3.0 (`../ios/tenGO/Info.plist`, `tenGO.entitlements`).
    /// - Info.plist : ATT (texte localisé dans 10 langues, InfoPlist.strings),
    ///   50 SKAdNetworkItems (`SKAdNetworkIds.txt`), GADApplicationIdentifier,
    ///   consent mode Firebase par défaut, schéma `tengo`, ATS, langues,
    ///   orientations, région de développement fr ;
    /// - entitlements : Game Center et domaines associés (liens universels) ;
    /// - frameworks : GameKit, AppTrackingTransparency ;
    /// - UnityFramework : version et build alignés sur l'app ;
    /// - icône : AppIcon.appiconset de l'app native.
    public sealed class IosPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100; // après le plugin Google Mobile Ads
        const string EditorDir = "Assets/Editor";
        public static readonly string[] Languages = { "fr", "en", "es", "de", "it", "pt-BR", "ja", "zh-Hans", "ko", "nl" };
        public static readonly string[] AssociatedDomains = { "applinks:appcraft31.app", "applinks:appcraft31.vercel.app" };

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string root = report.summary.outputPath;
            UpdatePlist(root);
            UpdateProject(root);
            AlignFrameworkVersion(root);
            CopyAppIcon(root);
        }

        const string NativeAppIcon = "../ios/tenGO/Assets.xcassets/AppIcon.appiconset";

        /// Icône de l'app iOS 3.3.0 (1024 × 1024 unique, clair, sombre et teinté) à
        /// la place des icônes par défaut d'Unity : sans elle, App Store Connect
        /// refuse le build (erreur 91111, « Missing app icon »).
        static void CopyAppIcon(string root)
        {
            if (!Directory.Exists(NativeAppIcon))
                throw new BuildFailedException("Icône introuvable : " + NativeAppIcon);
            string target = Path.Combine(root, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset");
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(NativeAppIcon))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        /// UnityFramework embarqué : même version et même build que l'app
        /// (Unity y laisse 1.0 et CURRENT_PROJECT_VERSION 1.0).
        static void AlignFrameworkVersion(string root)
        {
            var path = Path.Combine(root, "UnityFramework", "Info.plist");
            if (!File.Exists(path)) return;
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            plist.root.SetString("CFBundleShortVersionString", PlayerSettings.bundleVersion);
            plist.root.SetString("CFBundleVersion", PlayerSettings.iOS.buildNumber);
            plist.WriteToFile(path);

            var projPath = PBXProject.GetPBXProjectPath(root);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            foreach (var target in new[] { proj.GetUnityMainTargetGuid(), proj.GetUnityFrameworkTargetGuid() })
            {
                proj.SetBuildProperty(target, "MARKETING_VERSION", PlayerSettings.bundleVersion);
                proj.SetBuildProperty(target, "CURRENT_PROJECT_VERSION", PlayerSettings.iOS.buildNumber);
            }
            proj.WriteToFile(projPath);
        }

        public static Dictionary<string, string> ReadTracking() =>
            File.ReadAllLines(Path.Combine(EditorDir, "TrackingUsage.txt"))
                .Where(l => l.Contains('='))
                .ToDictionary(l => l.Substring(0, l.IndexOf('=')), l => l.Substring(l.IndexOf('=') + 1));

        public static IEnumerable<string> SkAdNetworkIds() =>
            File.ReadAllLines(Path.Combine(EditorDir, "SKAdNetworkIds.txt")).Select(l => l.Trim()).Where(l => l.Length > 0);

        static void UpdatePlist(string root)
        {
            var path = Path.Combine(root, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var dict = plist.root;
            dict.SetString("NSUserTrackingUsageDescription", ReadTracking()["fr"]);
            dict.SetString("GADApplicationIdentifier", Services.AdUnits.IosAppId);
            dict.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            dict.SetString("CFBundleDevelopmentRegion", "fr");
            dict.SetBoolean("UIRequiresFullScreen", true);
            dict.SetBoolean("UIStatusBarHidden", true);

            // Consent mode : signaux publicitaires refusés tant qu'ATT ne les autorise pas.
            dict.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE", true);
            dict.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE", false);
            dict.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA", false);
            dict.SetBoolean("GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS", false);

            var phone = dict.CreateArray("UISupportedInterfaceOrientations");
            phone.AddString("UIInterfaceOrientationPortrait");
            var pad = dict.CreateArray("UISupportedInterfaceOrientations~ipad");
            pad.AddString("UIInterfaceOrientationPortrait");
            pad.AddString("UIInterfaceOrientationPortraitUpsideDown");

            var langs = dict.CreateArray("CFBundleLocalizations");
            foreach (var l in Languages) langs.AddString(l);

            var ats = dict.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
            ats.SetBoolean("NSAllowsArbitraryLoadsForMedia", true);
            ats.SetBoolean("NSAllowsArbitraryLoadsInWebContent", true);

            // Schéma tengo:// (liens de duel), sans doublon si Unity en a déjà posé.
            var types = dict["CFBundleURLTypes"] as PlistElementArray ?? dict.CreateArray("CFBundleURLTypes");
            bool hasScheme = types.values.OfType<PlistElementDict>()
                .Any(t => (t["CFBundleURLSchemes"] as PlistElementArray)?.values.Any(v => v.AsString() == "tengo") == true);
            if (!hasScheme)
            {
                var type = types.AddDict();
                type.SetString("CFBundleURLName", "AppCraft31.tenGO");
                type.CreateArray("CFBundleURLSchemes").AddString("tengo");
            }

            var items = dict["SKAdNetworkItems"] as PlistElementArray ?? dict.CreateArray("SKAdNetworkItems");
            var present = new HashSet<string>(items.values.OfType<PlistElementDict>()
                .Select(d => d["SKAdNetworkIdentifier"]?.AsString()?.ToLowerInvariant()).Where(v => v != null));
            foreach (var id in SkAdNetworkIds())
                if (present.Add(id.ToLowerInvariant())) items.AddDict().SetString("SKAdNetworkIdentifier", id);
            plist.WriteToFile(path);
        }

        static void UpdateProject(string root)
        {
            var projPath = PBXProject.GetPBXProjectPath(root);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            string framework = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(framework, "AppTrackingTransparency.framework", true);
            proj.AddFrameworkToProject(framework, "GameKit.framework", false);

            // Texte ATT localisé : <langue>.lproj/InfoPlist.strings.
            foreach (var kv in ReadTracking())
            {
                string folder = kv.Key + ".lproj";
                Directory.CreateDirectory(Path.Combine(root, folder));
                string text = "\"NSUserTrackingUsageDescription\" = \"" + kv.Value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\";\n";
                File.WriteAllText(Path.Combine(root, folder, "InfoPlist.strings"), text);
                if (proj.FindFileGuidByProjectPath(folder) == null)
                    proj.AddFileToBuild(main, proj.AddFolderReference(folder, folder));
            }
            proj.WriteToFile(projPath);

            // Entitlements : Game Center + domaines associés (posés aussi dans CODE_SIGN_ENTITLEMENTS).
            var capabilities = new ProjectCapabilityManager(projPath, "tenGO.entitlements", null, main);
            capabilities.AddGameCenter();
            capabilities.AddAssociatedDomains(AssociatedDomains);
            capabilities.WriteToFile();
        }
    }
}
