using System.IO;
using System.Collections.Generic;
using TenGo.Rendering;
using TenGo.Showcase;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

#nullable enable

namespace TenGo.Editor
{
    /// Réglages du Player, polices TMP, matériau des cercles et scène de
    /// démonstration, appliqués de façon reproductible :
    /// Tools/unity.sh TenGo.Editor.ProjectSetup.Apply
    public static class ProjectSetup
    {
        /// Identifiants des apps en ligne : la version Unity les remplace.
        public const string AndroidId = "com.appcraft31.tengo";
        public const string IosId = "AppCraft31.tenGO";
        public const string Version = "4.0.0";
        /// Dernières versions natives : Android 16 (3.3.0), iOS build 282.
        public const int AndroidVersionCode = 17;
        public const int IosBuild = 283;
        public const string DemoScene = "Assets/Scenes/GlowDemo.unity";
        /// Graisses de Nunito (instances statiques de la police variable d'Android).
        public static readonly string[] FontWeights = { "Regular", "SemiBold", "Bold", "ExtraBold", "Black" };
        /// Police par défaut de TextMeshPro.
        const string DefaultWeight = "Bold";
        const string MaterialPath = "Assets/Resources/" + GlowCircle.MaterialResource + ".mat";

        [MenuItem("TenGo/Appliquer les réglages du projet")]
        public static void Apply()
        {
            ApplyPlayerSettings();
            NativeSetup.Apply();
            EnsureGlowMaterial();
            EnsureMaterials();
            EnsureFont();
            EnsureDemoScene();
            ClearDynamicFontData();
            Debug.Log("[ProjectSetup] Réglages appliqués.");
        }

        /// Vide les atlas des polices dynamiques (Nunito, replis CJK) et enregistre :
        /// les glyphes rendus en éditeur (captures, tests) n'ont pas à finir dans le dépôt.
        public static void ClearDynamicFontData()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
                asset.ClearFontAssetData(true);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "AppCraft31";
            PlayerSettings.productName = "tenGO";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosId);

            // Espace Gamma : les couleurs sRGB se mélangent comme dans SpriteKit
            // (voiles, ombres et tracés translucides sur fond crème, TenGoCommon.hlsl).
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // Portrait seulement.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Pas d'écran de démarrage Unity (permis en Personal depuis Unity 6).
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Palette.Background;

            // Android : IL2CPP ARM64, AAB, API 26 minimum (24 demandé, plancher d'Unity 6.6), cible = la plus récente installée.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; // Unity 6.6 refuse moins de 26 (Android 8.0)
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
            EditorUserBuildSettings.buildAppBundle = true;

            // iOS : IL2CPP (seul possible), ARM64, iOS 15 minimum, iPhone et iPad.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // 1 = ARM64
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = IosBuild.ToString();
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;

            Debug.Log($"[ProjectSetup] Player : {PlayerSettings.productName} / {PlayerSettings.companyName} / {AndroidId} / {IosId} {Version} ({AndroidVersionCode}/{IosBuild}), " +
                      $"splash={PlayerSettings.SplashScreen.show}, logo={PlayerSettings.SplashScreen.showUnityLogo}, " +
                      $"Android min {PlayerSettings.Android.minSdkVersion} cible {PlayerSettings.Android.targetSdkVersion}, iOS {PlayerSettings.iOS.targetOSVersionString}");
        }

        /// Matériau partagé de GlowCircle, chargé par Resources (inclus dans les builds).
        static void EnsureGlowMaterial()
        {
            if (File.Exists(MaterialPath)) return;
            var shader = Shader.Find("TenGo/GlowCircle") ?? throw new System.InvalidOperationException("Shader TenGo/GlowCircle introuvable");
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)!);
            AssetDatabase.CreateAsset(new Material(shader) { name = "GlowCircle" }, MaterialPath);
        }

        /// Matériaux des shaders de rendu (Resources : inclus dans les builds).
        static readonly string[] RenderMaterials = { "Shape", "Trail", "Wave", "IconSdf", "Dots" };

        static void EnsureMaterials()
        {
            foreach (var name in RenderMaterials)
            {
                var path = $"Assets/Resources/Materials/{name}.mat";
                if (File.Exists(path)) continue;
                var shader = Shader.Find("TenGo/" + name) ?? throw new System.InvalidOperationException($"Shader TenGo/{name} introuvable");
                AssetDatabase.CreateAsset(new Material(shader) { name = name }, path);
            }
        }

        /// Replis CJK (Assets/Fonts/Fallback, voir LICENSES.md), dans l'ordre par défaut.
        static readonly string[] CjkFallbacks = { "NotoSansJP-Bold", "NotoSansKR-Bold", "NotoSansSC-Bold" };
        const string FontSetPath = "Assets/Fonts/Resources/" + Fonts.ResourcePath + ".asset";

        /// Nunito en polices SDF dynamiques (une par graisse), replis Noto CJK
        /// chaînés derrière chacune ; Bold par défaut dans TextMeshPro ; jeu de
        /// polices `TenGoFonts` (Resources) pour l'accès par graisse.
        static void EnsureFont()
        {
            var fallbacks = new List<TMP_FontAsset>();
            foreach (var f in CjkFallbacks)
            {
                var a = EnsureDynamicFont($"Assets/Fonts/Fallback/{f}.ttf", $"Assets/Fonts/Fallback/{f} SDF.asset", 64, 6);
                if (a != null) fallbacks.Add(a);
            }
            TMP_FontAsset? fallbackDefault = null;
            var byWeight = new Dictionary<string, TMP_FontAsset>();
            foreach (var weight in FontWeights)
            {
                var asset = EnsureFontAsset(weight);
                if (asset == null) continue;
                asset.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
                EditorUtility.SetDirty(asset);
                byWeight[weight] = asset;
                if (weight == DefaultWeight) fallbackDefault = asset;
            }
            var set = AssetDatabase.LoadAssetAtPath<TenGoFontSet>(FontSetPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FontSetPath)!);
                set = ScriptableObject.CreateInstance<TenGoFontSet>();
                AssetDatabase.CreateAsset(set, FontSetPath);
            }
            byWeight.TryGetValue("Regular", out set.regular);
            byWeight.TryGetValue("SemiBold", out set.semiBold);
            byWeight.TryGetValue("Bold", out set.bold);
            byWeight.TryGetValue("ExtraBold", out set.extraBold);
            byWeight.TryGetValue("Black", out set.black);
            set.japanese = fallbacks.Count > 0 ? fallbacks[0] : null;
            set.korean = fallbacks.Count > 1 ? fallbacks[1] : null;
            set.chineseSimplified = fallbacks.Count > 2 ? fallbacks[2] : null;
            EditorUtility.SetDirty(set);
            if (fallbackDefault == null) return;
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                Debug.LogError("[ProjectSetup] « TMP Settings » absent : voir Assets/TextMesh Pro.");
                return;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = fallbackDefault;
            var list = so.FindProperty("m_fallbackFontAssets");
            list.arraySize = fallbacks.Count;
            for (int i = 0; i < fallbacks.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = fallbacks[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log($"[ProjectSetup] Police TMP par défaut : {fallbackDefault.name}, {fallbacks.Count} replis CJK");
        }

        /// Police TMP SDF dynamique d'un fichier TTF (créée si absente), atlas vidé au build.
        static TMP_FontAsset? EnsureDynamicFont(string ttf, string assetPath, int sampling, int padding)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;
            AssetDatabase.ImportAsset(ttf);
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) { Debug.LogError($"[ProjectSetup] Police absente : {ttf}"); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, sampling, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            var so = new SerializedObject(asset);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static TMP_FontAsset? EnsureFontAsset(string weight)
        {
            var fontPath = $"Assets/Fonts/Nunito-{weight}.ttf";
            var assetPath = $"Assets/Fonts/Nunito-{weight} SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset != null) return asset;
            AssetDatabase.ImportAsset(fontPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError($"[ProjectSetup] Police absente : {fontPath}");
                return null;
            }
            asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = $"Nunito-{weight} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = $"Nunito-{weight} Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = $"Nunito-{weight} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// Scène de démonstration : la vitrine de l'étape 4 (plateau, chemin,
        /// états des bulles), construite au lancement par `VitrineRunner`.
        public static void EnsureDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Units.ReferenceOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Look.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            new GameObject("Vitrine", typeof(VitrineRunner));

            Directory.CreateDirectory(Path.GetDirectoryName(DemoScene)!);
            EditorSceneManager.SaveScene(scene, DemoScene);
            if (File.Exists("Assets/Scenes/SampleScene.unity"))
                AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoScene, true) };
        }
    }
}
