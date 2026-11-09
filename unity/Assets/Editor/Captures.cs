using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TenGo.Editor
{
    /// Captures d'écran en batchmode (`Tools/unity.sh TenGo.Editor.Captures.GlowDemo`) :
    /// la caméra de la scène est rendue dans une RenderTexture, enregistrée
    /// en PNG dans Logs/captures/.
    public static class Captures
    {
        public const string Dir = "Logs/captures";

        public static void GlowDemo()
        {
            EditorSceneManager.OpenScene(ProjectSetup.DemoScene, OpenSceneMode.Single);
            var path = Shot(Camera.main, "glow_demo.png", 780, 1688);
            Debug.Log($"[Capture] {path}");
        }

        /// Rend `cam` en `width` × `height` et écrit le PNG ; retourne son chemin.
        public static string Shot(Camera cam, string file, int width, int height)
        {
            if (cam == null) throw new System.InvalidOperationException("Pas de caméra à capturer");
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            var previous = cam.targetTexture;
            var active = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Dir);
                var path = Path.GetFullPath(Path.Combine(Dir, file));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return path;
            }
            finally
            {
                cam.targetTexture = previous;
                RenderTexture.active = active;
                Object.DestroyImmediate(rt);
            }
        }
    }
}
