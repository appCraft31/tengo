using TenGo.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TenGo.Editor
{
    /// Scène unique du jeu, reconstruite de façon reproductible :
    /// Tools/unity.sh TenGo.Editor.MainScene.Ensure
    /// Une caméra et un `AppRoot` qui compose tout au démarrage.
    public static class MainScene
    {
        public const string Path = "Assets/Scenes/Main.unity";

        [MenuItem("TenGo/Reconstruire la scène principale")]
        public static void Ensure()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("AppRoot", typeof(AppRoot));
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0, 0, -10);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = TenGo.Units.ReferenceOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.98f, 0.96f, 0.93f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Path);

            // Main en premier (seule scène livrée) ; la vitrine de l'étape 4 reste ouvrable en éditeur.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Path, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MainScene] " + Path + " enregistrée et seule scène du build.");
        }
    }
}
