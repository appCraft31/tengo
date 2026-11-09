using System.IO;
using TenGo.Rendering;
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
            EnsureGlowMaterial();
            EnsureFont();
            EnsureDemoScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Réglages appliqués.");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "AppCraft31";
            PlayerSettings.productName = "tenGO";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosId);

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

        /// Nunito en polices SDF dynamiques (une par graisse) ; Bold par défaut dans TextMeshPro.
        static void EnsureFont()
        {
            TMP_FontAsset? fallbackDefault = null;
            foreach (var weight in FontWeights)
            {
                var asset = EnsureFontAsset(weight);
                if (weight == DefaultWeight) fallbackDefault = asset;
            }
            if (fallbackDefault == null) return;
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null)
            {
                Debug.LogError("[ProjectSetup] « TMP Settings » absent : voir Assets/TextMesh Pro.");
                return;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = fallbackDefault;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log($"[ProjectSetup] Police TMP par défaut : {fallbackDefault.name}");
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

        /// Scène de démonstration : les neuf bulles du thème par défaut.
        public static void EnsureDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Units.ReferenceOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            // Une rangée de bulles pastel 1 à 9 sur le fond crème (rendu provisoire).
            var root = new GameObject("Demo").transform;
            for (var v = 1; v <= 9; v++)
            {
                var x = ((v - 1) % 3 - 1) * 1.9f;
                var y = (1 - (v - 1) / 3) * 1.9f;
                GlowCircle.Create($"Bulle{v}", root, new Vector2(x, y), Palette.Bubbles[v - 1], 0.75f,
                    thickness: 0.04f, glowIntensity: 0.4f, glowRange: 0.15f, fill: 1f);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DemoScene)!);
            EditorSceneManager.SaveScene(scene, DemoScene);
            if (File.Exists("Assets/Scenes/SampleScene.unity"))
                AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoScene, true) };
        }
    }
}
