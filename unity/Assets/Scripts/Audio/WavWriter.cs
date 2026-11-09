using System;
using System.IO;

namespace TenGo.Audio
{
    /// Écriture d'un WAV PCM 16 bits mono (export des sons pour vérification).
    public static class WavWriter
    {
        public static void Write(string path, float[] samples, int sampleRate)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(sampleRate);
            w.Write(sampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples)
            {
                float c = Math.Max(-1f, Math.Min(1f, s));
                w.Write((short)Math.Round(c * 32767f));
            }
        }

        /// Crête en dBFS (−∞ pour un silence).
        public static double PeakDbfs(float[] pcm)
        {
            float peak = 0f;
            foreach (var s in pcm) peak = Math.Max(peak, Math.Abs(s));
            return peak > 0 ? 20 * Math.Log10(peak) : double.NegativeInfinity;
        }
    }
}
