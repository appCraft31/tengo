using System;

namespace TenGo.Audio
{
    /// Passe-bas à un pôle, équivalent de l'EQ passe-bas 1000 Hz d'iOS (Dsp.kt) :
    /// y += a·(x − y), a = 1 − exp(−2π·fc/fs).
    public sealed class Lowpass
    {
        readonly float _a;
        float _y;
        public Lowpass(double cutoffHz, double sampleRate) => _a = (float)(1.0 - Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate));
        public float Process(float x) { _y += _a * (x - _y); return _y; }
    }

    /// Réverbération Schroeder (4 peignes en parallèle, 2 passe-tout en série),
    /// équivalent de `AVAudioUnitReverb` largeRoom à 30 % (Dsp.kt). Délais
    /// réglés pour 44 100 Hz, mis à l'échelle de la fréquence de sortie.
    public sealed class SchroederReverb
    {
        static readonly int[] CombTunings = { 1116, 1188, 1277, 1356 };
        static readonly int[] AllpassTunings = { 556, 441 };
        const float CombFeedback = 0.84f;
        const float AllpassFeedback = 0.5f;

        readonly float[][] _comb;
        readonly int[] _combIdx;
        readonly float[][] _allpass;
        readonly int[] _allpassIdx;
        readonly float _wet;

        public SchroederReverb(double sampleRate, float wetMix = 0.30f)
        {
            _wet = wetMix;
            double k = sampleRate / 44100.0;
            _comb = new float[CombTunings.Length][];
            _combIdx = new int[CombTunings.Length];
            for (int i = 0; i < CombTunings.Length; i++) _comb[i] = new float[Math.Max(1, (int)(CombTunings[i] * k))];
            _allpass = new float[AllpassTunings.Length][];
            _allpassIdx = new int[AllpassTunings.Length];
            for (int i = 0; i < AllpassTunings.Length; i++) _allpass[i] = new float[Math.Max(1, (int)(AllpassTunings[i] * k))];
        }

        public float Process(float input)
        {
            float o = 0f;
            for (int i = 0; i < _comb.Length; i++)
            {
                var buf = _comb[i];
                int idx = _combIdx[i];
                float y = buf[idx];
                buf[idx] = input + y * CombFeedback;
                _combIdx[i] = idx + 1 == buf.Length ? 0 : idx + 1;
                o += y;
            }
            o /= _comb.Length;
            for (int i = 0; i < _allpass.Length; i++)
            {
                var buf = _allpass[i];
                int idx = _allpassIdx[i];
                float bo = buf[idx];
                float y = -o + bo;
                buf[idx] = o + bo * AllpassFeedback;
                _allpassIdx[i] = idx + 1 == buf.Length ? 0 : idx + 1;
                o = y;
            }
            return input * (1f - _wet) + o * _wet;
        }
    }
}
