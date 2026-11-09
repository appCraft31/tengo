using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

#nullable enable

namespace TenGo.Editor
{
    /// Icônes de l'app, reprises telles quelles des apps natives (appelé par
    /// `ProjectSetup.Apply`) :
    /// - Android : icône adaptative de `../android/app/src/main/res`
    ///   (`ic_launcher_background` dégradé corail → rose, `ic_launcher_foreground`
    ///   « 10 » blanc + 2 cercles), rendue en 432×432 (108 dp, zone sûre 66 %).
    ///   Unity 6.6 n'a plus que le type Adaptive (Round et Legacy obsolètes) : les
    ///   icônes classiques `Legacy-<densité>.png` (zone visible de 72 dp, coins
    ///   arrondis) sont posées par `AndroidIconPostprocess` ;
    /// - icône par défaut (toutes plateformes) : `AppIcon-1024.png` d'iOS
    ///   (l'appiconset natif est de toute façon recopié par `IosPostprocess`).
    public static class IconSetup
    {
        public const string Folder = "Assets/Art/Icons/";
        public const string Background = Folder + "AdaptiveBackground.png";
        public const string Foreground = Folder + "AdaptiveForeground.png";
        public const string Default = Folder + "AppIcon-1024.png";

        public static void Apply()
        {
            var background = Load(Background);
            var foreground = Load(Foreground);
            var fallback = Load(Default);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { fallback }, IconKind.Any);

            var target = NamedBuildTarget.Android;
            // Couches des icônes adaptatives : 0 = arrière-plan, 1 = premier plan.
            Set(target, AndroidPlatformIconKind.Adaptive, background, foreground);
            Debug.Log("[IconSetup] Icône adaptative Android et icône par défaut appliquées.");
        }

        static void Set(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        /// Texture d'icône, importée sans compression ni mipmaps (Unity la redimensionne par densité).
        static Texture2D Load(string path)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                bool dirty = importer.textureType != TextureImporterType.Default
                             || importer.mipmapEnabled
                             || importer.textureCompression != TextureImporterCompression.Uncompressed
                             || importer.npotScale != TextureImporterNPOTScale.None
                             || !importer.alphaIsTransparency
                             || importer.maxTextureSize < 2048;
                if (dirty)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                   ?? throw new System.InvalidOperationException("Icône introuvable : " + path);
        }
    }

    /// Icônes classiques (non adaptatives) du module `launcher` du projet Gradle :
    /// - `app_icon.png` : Unity 6.6 n'y met que son logo (mdpi), faute de type Legacy ;
    /// - `ic_launcher.png` : le module Unity de Liftoff embarque le robot Android
    ///   sous ce nom ; les ressources du module application l'emportent sur celles
    ///   des bibliothèques, densité par densité.
    /// Le manifeste pointe sur `@mipmap/app_icon`, dont la version anydpi-v26
    /// (icône adaptative) sert sur tous les appareils (minSdk 26) ; `roundIcon`
    /// y est ajouté (même ressource), comme `ic_launcher_round` de l'app native.
    public sealed class AndroidIconPostprocess : IPostGenerateGradleAndroidProject
    {
        static readonly string[] Densities = { "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi" };
        public int callbackOrder => 20;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var res = Path.Combine(path, "..", "launcher", "src", "main", "res");
            if (!Directory.Exists(res))
                throw new BuildFailedException("Ressources du module launcher introuvables : " + res);
            foreach (var density in Densities)
            {
                var source = Path.GetFullPath($"{IconSetup.Folder}Legacy-{density}.png");
                if (!File.Exists(source)) throw new BuildFailedException("Icône introuvable : " + source);
                var dir = Path.Combine(res, "mipmap-" + density);
                Directory.CreateDirectory(dir);
                File.Copy(source, Path.Combine(dir, "app_icon.png"), true);
                File.Copy(source, Path.Combine(dir, "ic_launcher.png"), true);
            }

            const string ns = "http://schemas.android.com/apk/res/android";
            var manifestPath = Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            if (doc.SelectSingleNode("/manifest/application") is XmlElement app)
            {
                app.SetAttribute("roundIcon", ns, app.GetAttribute("icon", ns));
                doc.Save(manifestPath);
            }
        }
    }
}
