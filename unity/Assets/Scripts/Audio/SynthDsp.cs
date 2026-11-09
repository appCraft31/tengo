using System;

namespace TenGo.Audio
{
    /// Passe-bas à un pôle (Dsp.kt, render.py) : y += a·(x − y), a = 1 − exp(−2π·fc/fs).
    public sealed class Lowpass
    {
        readonly float _a;
        float _y;
        public Lowpass(double cutoffHz, double sampleRate) => _a = (float)(1.0 - Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate));
        public float Process(float x) { _y += _a * (x - _y); return _y; }
    }

    /// Réverbération du son 4.0, identique à `reverb()` de render.py : quatre
    /// peignes à rétroaction 0,78 en parallèle (29,7 / 37,1 / 41,1 / 43,7 ms,
    /// multipliés par `size`), moyennés ; sortie = sec·(1 − wet) + peignes·wet.
    /// Le signal sec est inclus dans chaque peigne, comme dans la référence.
    /// Délais en secondes, donc indépendants de la fréquence de sortie.
    public sealed class CombReverb
    {
        static readonly double[] Delays = { 0.0297, 0.0371, 0.0411, 0.0437 };
        public const float Feedback = 0.78f;

        readonly float[][] _buf;
        readonly int[] _idx;
        readonly float _wet;

        public CombReverb(double sampleRate, float wetMix, double size)
        {
            _wet = wetMix;
            _buf = new float[Delays.Length][];
            _idx = new int[Delays.Length];
            for (int i = 0; i < Delays.Length; i++) _buf[i] = new float[Math.Max(1, (int)(sampleRate * Delays[i] * size))];
        }

        public float Process(float input)
        {
            float acc = 0f;
            for (int i = 0; i < _buf.Length; i++)
            {
                var b = _buf[i];
                int k = _idx[i];
                float y = input + b[k] * Feedback;   // y[n] = x[n] + 0,78·y[n − D]
                b[k] = y;
                _idx[i] = k + 1 == b.Length ? 0 : k + 1;
                acc += y;
            }
            return input * (1f - _wet) + acc / _buf.Length * _wet;
        }
    }

    /// Limiteur doux sans état : transparent sous `Threshold`, puis courbe en
    /// tangente hyperbolique qui tend vers `Ceiling` (−0,18 dBFS) sans l'atteindre.
    public static class SoftLimiter
    {
        public const float Threshold = 0.8f;
        public const float Ceiling = 0.98f;

        public static float Process(float x)
        {
            float a = Math.Abs(x);
            if (a <= Threshold) return x;
            float k = Ceiling - Threshold;
            float y = Threshold + k * (float)Math.Tanh((a - Threshold) / k);
            return x < 0 ? -y : y;
        }
    }
}
