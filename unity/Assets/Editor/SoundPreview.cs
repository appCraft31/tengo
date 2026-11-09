using System;
using System.IO;
using TenGo.Audio;
using UnityEngine;

namespace TenGo.Editor
{
    /// Rendu de contrôle du son 4.0 (`Tools/unity.sh TenGo.Editor.SoundPreview.Render`) :
    /// la séquence de la maquette render.py (variante B) jouée par le vrai moteur,
    /// écrite dans Logs/sound/preview.wav, avec crête et RMS dans
    /// Logs/sound/preview.txt pour comparer à son_B.wav.
    public static class SoundPreview
    {
        public const string Dir = "Logs/sound";
        const int Rate = 44100;

        public static void Render() => Write("preview", true);

        /// Même séquence sans humanisation (Logs/sound/preview_dry.wav), pour une
        /// comparaison échantillon par échantillon avec render.py sans humanisation.
        public static void RenderDry() => Write("preview_dry", false);

        static void Write(string name, bool humanize)
        {
            Directory.CreateDirectory(Dir);
            var pcm = SoundSequence.Render(Rate, humanize: humanize);
            var path = Path.GetFullPath(Path.Combine(Dir, name + ".wav"));
            WavWriter.Write(path, pcm, Rate);

            double sum = 0; float peak = 0;
            foreach (var s in pcm) { sum += s * (double)s; peak = Math.Max(peak, Math.Abs(s)); }
            double rms = Math.Sqrt(sum / pcm.Length);
            string report = $"durée {pcm.Length / (double)Rate:0.00} s\ncrête {20 * Math.Log10(peak):0.00} dBFS\nRMS {20 * Math.Log10(rms):0.0} dBFS\n";
            File.WriteAllText(Path.Combine(Dir, name + ".txt"), report);
            Debug.Log($"[SoundPreview] {path}\n{report}");
        }
    }
}
