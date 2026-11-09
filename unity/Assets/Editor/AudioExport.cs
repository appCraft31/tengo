using System.IO;
using System.Text;
using TenGo.Audio;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Editor
{
    /// Rendu hors ligne des sons du jeu en WAV (`Tools/unity.sh TenGo.Editor.AudioExport.All`),
    /// par le même moteur que le temps réel, pour les écouter et les comparer
    /// à l'app iOS. Sortie : Logs/audio/*.wav et Logs/audio/report.txt (crête en dBFS).
    public static class AudioExport
    {
        public const string Dir = "Logs/audio";
        const int Rate = 44100;

        public static void All()
        {
            Directory.CreateDirectory(Dir);
            var report = new StringBuilder("Son\tDurée (s)\tCrête (dBFS)\n");

            void Export(string name, double seconds, System.Action<SoundBoard, System.Action<double>> play)
            {
                var engine = new SynthEngine(Rate);
                double now = 100;
                var board = new SoundBoard(engine, () => now);
                var pcm = new System.Collections.Generic.List<float>();
                // `advance(s)` : rend s secondes et fait avancer l'horloge du throttle.
                void Advance(double s) { pcm.AddRange(engine.RenderMono(s)); now += s; }
                play(board, Advance);
                double left = seconds - pcm.Count / (double)Rate;
                if (left > 0) Advance(left);
                var data = pcm.ToArray();
                var path = Path.GetFullPath(Path.Combine(Dir, name + ".wav"));
                WavWriter.Write(path, data, Rate);
                double peak = WavWriter.PeakDbfs(data);
                report.Append($"{name}\t{seconds:0.0}\t{peak:0.0}\n");
                Debug.Log($"[Audio] {path} ({peak:0.0} dBFS)");
            }

            for (int v = 1; v <= 9; v++)
            {
                int value = v;
                Export($"select_{value}", 1.0, (b, adv) => b.PlaySelect(value));
            }
            // Chemin 2 + 3 + 4 + 1, une bulle toutes les 120 ms, puis validation.
            Export("path_2341_combo", 3.0, (b, adv) =>
            {
                int[] path = { 2, 3, 4, 1 };
                int sum = 0;
                for (int i = 0; i < path.Length; i++)
                {
                    sum += path[i];
                    if (i == 0) b.PlaySelect(path[i]); else b.PlayConnect(path[i], sum / 10.0);
                    adv(0.12);
                }
                b.PlayCombo(path.Length);
            });
            foreach (var len in new[] { 2, 4, 6 })
            {
                int l = len;
                Export($"combo_{l}", 2.5, (b, adv) => { b.PlaySelect(5); adv(0.3); b.PlayCombo(l); });
            }
            Export("rejected", 1.0, (b, adv) => b.PlayRejected());
            Export("win", 3.5, (b, adv) => b.PlayWin());
            Export("lose", 3.0, (b, adv) => b.PlayLose());
            // Huit notes à la fois : saturation des voix et du mélange.
            Export("eight_voices", 3.0, (b, adv) => b.PlayChord(Scales.Notes[..8], 1f));

            var reportPath = Path.GetFullPath(Path.Combine(Dir, "report.txt"));
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log($"[Audio] {reportPath}");
        }
    }
}
