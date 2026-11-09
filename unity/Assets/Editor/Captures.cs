using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Showcase;
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

        // MARK: - Étape 4 : briques de rendu

        /// Taille des captures : un iPhone 17 Pro Max (1320 × 2868) à demi-résolution,
        /// même rapport que les captures du simulateur (scratchpad, marketing/screenshots).
        public const int W = 660, H = 1434;

        static readonly string[] ThemeIds = { "default", "forest", "ocean", "desert", "candy", "space", "night" };

        /// Scène vide, caméra du jeu (aspectFill de 750 × 1334), horloge à zéro.
        static (Camera cam, Transform root) Fresh(string themeId, string bubble = "classic", string trail = "classic")
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.ForceFull = true;
            Look.Apply(themeId, bubble, trail);
            var camGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Units.ReferenceOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            return (cam, new GameObject("Root").transform);
        }

        static void Log(string path) => Debug.Log($"[Capture] {path}");

        /// Plateau 9 × 7 du thème par défaut avec un chemin tracé, après 1 s d'animation.
        public static void Grid()
        {
            var (cam, root) = Fresh("default");
            Vitrine.Build(Vitrine.Page.Grid, root, cam);
            FxTime.Advance(1f);
            Log(Shot(cam, "step4_grid.png", 1320, 2868));
        }

        /// Le plateau dans chacun des 7 thèmes (fonds animés à t = 3 s).
        public static void Themes()
        {
            foreach (var id in ThemeIds)
            {
                var (cam, root) = Fresh(id);
                Vitrine.Build(Vitrine.Page.Grid, root, cam);
                FxTime.Advance(3f);
                Log(Shot(cam, $"step4_theme_{id}.png", W, H));
            }
        }

        /// Matières de bulles, tracés et états.
        public static void Materials()
        {
            var (cam, root) = Fresh("default");
            Vitrine.Build(Vitrine.Page.Materials, root, cam);
            FxTime.Advance(0.5f);
            Log(Shot(cam, "step4_materials.png", W, H));
        }

        /// Effets à plusieurs instants (éclatements, anneaux, flash, confettis).
        public static void Effects()
        {
            var (cam, root) = Fresh("default");
            Vitrine.Build(Vitrine.Page.Effects, root, cam);
            float t = 0f;
            foreach (var at in new[] { 0.05f, 0.12f, 0.3f, 0.6f, 1.2f })
            {
                FxTime.Advance(at - t);
                t = at;
                Log(Shot(cam, $"step4_effects_{Mathf.RoundToInt(at * 1000):0000}ms.png", W, H));
            }
            JuiceSettings.Reset();
        }

        /// Icônes, boutons, pièce, textes (Nunito et replis CJK).
        public static void Ui()
        {
            var (cam, root) = Fresh("default");
            Vitrine.Build(Vitrine.Page.Ui, root, cam);
            FxTime.Advance(0.2f);
            Log(Shot(cam, "step4_ui.png", W, H));
        }

        /// Toutes les captures de l'étape 4.
        public static void Step4()
        {
            Grid();
            Themes();
            Materials();
            Effects();
            Ui();
            JuiceSettings.Reset();
            Look.Reset();
            FxTime.Reset();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ProjectSetup.ClearDynamicFontData();
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
