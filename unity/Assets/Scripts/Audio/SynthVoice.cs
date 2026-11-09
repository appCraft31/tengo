using System;

namespace TenGo.Audio
{
    /// Timbres du synthé (son 4.0, variante kalimba validée le 9 nov. 2026).
    public enum VoiceKind
    {
        /// Kalimba courte (notes sous le doigt) : corps en exp −5,5/s, 1,2 s.
        KalimbaShort,
        /// Kalimba longue (arpèges, victoire, défaite) : corps en exp −2,6/s, 2,4 s.
        KalimbaLong,
        /// Cloche : partiels 1 / 2,76 / 5,4 / 8,9, 3 s.
        Bell,
        /// « Tok » mat de la bulle refusée : sinus 150 Hz en exp −40/s, 120 ms.
        Tok,
    }

    /// Voix polyphonique à partiels sinusoïdaux décroissants (render.py, variante B) :
    /// - kalimba : corps 1 × f, « lame » 5,4 × f (0,35, exp −28/s), clic 9,1 × f
    ///   (0,25, exp −90/s), attaque 3 ms, gain 0,55 ;
    /// - cloche : (1, 1, −1,2/s), (0,5, 2,76, −2,5/s), (0,3, 5,4, −4/s), (0,15, 8,9, −7/s),
    ///   attaque 2 ms, gain 0,3 ;
    /// - tok : 150 Hz, exp −40/s, gain 0,5.
    /// Chaque partiel est un phaseur tourné à chaque échantillon et une
    /// enveloppe multipliée par un facteur constant : aucun sinus ni aucune
    /// exponentielle calculés par échantillon, aucune allocation. Un partiel
    /// au-delà de 0,45 × la fréquence d'échantillonnage est omis (repliement).
    /// Un vol de voix ne claque pas : l'écart avec la dernière valeur sortie
    /// s'éteint en ~2 ms.
    public sealed class SynthVoice
    {
        public const int MaxPartials = 4;
        public const float KalimbaGain = 0.55f;
        public const float BellGain = 0.30f;
        public const float TokGain = 0.50f;
        /// Fondu de fin (la référence coupe net à la durée du timbre).
        const double EndFade = 0.005;

        readonly double _sampleRate;
        readonly double[] _re = new double[MaxPartials], _im = new double[MaxPartials];
        readonly double[] _cos = new double[MaxPartials], _sin = new double[MaxPartials];
        readonly double[] _amp = new double[MaxPartials], _decay = new double[MaxPartials];
        readonly double _declickMul;
        int _partials;
        int _sampleIndex, _length, _attack, _fadeStart;
        double _gain, _declick;
        float _last;

        public bool IsActive { get; private set; }
        /// Ordre de déclenchement (vol de la plus ancienne voix).
        public long StartTime { get; private set; }
        public double Frequency { get; private set; }
        public float Velocity { get; private set; }
        public VoiceKind Kind { get; private set; }
        public bool IsShort => Kind == VoiceKind.KalimbaShort || Kind == VoiceKind.Tok;

        public SynthVoice(double sampleRate)
        {
            _sampleRate = sampleRate;
            _declickMul = Math.Exp(-1.0 / (0.002 * sampleRate));
        }

        /// Durée du timbre en secondes (comme les tampons de render.py).
        public static double DurationOf(VoiceKind kind) => kind switch
        {
            VoiceKind.KalimbaShort => 1.2,
            VoiceKind.KalimbaLong => 2.4,
            VoiceKind.Bell => 3.0,
            _ => 0.12,
        };

        public void NoteOn(double frequency, long now, float velocity = 1f, bool isShort = false) =>
            NoteOn(isShort ? VoiceKind.KalimbaShort : VoiceKind.KalimbaLong, frequency, now, velocity);

        public void NoteOn(VoiceKind kind, double frequency, long now, float velocity)
        {
            // Vol de voix : on part de la dernière valeur sortie pour éviter le claquement.
            _declick = _last;
            Kind = kind;
            Frequency = frequency;
            Velocity = Math.Max(0f, Math.Min(1f, velocity));
            StartTime = now;
            _partials = 0;
            switch (kind)
            {
                case VoiceKind.KalimbaShort:
                case VoiceKind.KalimbaLong:
                    AddPartial(frequency, 1.0, kind == VoiceKind.KalimbaShort ? 5.5 : 2.6);
                    AddPartial(frequency * 5.4, 0.35, 28);
                    AddPartial(frequency * 9.1, 0.25, 90);
                    _gain = KalimbaGain;
                    _attack = Samples(0.003);
                    break;
                case VoiceKind.Bell:
                    AddPartial(frequency, 1.0, 1.2);
                    AddPartial(frequency * 2.76, 0.5, 2.5);
                    AddPartial(frequency * 5.4, 0.3, 4);
                    AddPartial(frequency * 8.9, 0.15, 7);
                    _gain = BellGain;
                    _attack = Samples(0.002);
                    break;
                default:
                    AddPartial(frequency, 1.0, 40);
                    _gain = TokGain;
                    _attack = 1;
                    break;
            }
            _gain *= Velocity;
            _length = Samples(DurationOf(kind));
            _fadeStart = _length - Samples(EndFade);
            _sampleIndex = 0;
            IsActive = true;
        }

        int Samples(double seconds) => Math.Max(1, (int)(seconds * _sampleRate));

        void AddPartial(double freq, double amp, double decayPerSecond)
        {
            if (freq <= 0 || freq > 0.45 * _sampleRate) return;
            int i = _partials++;
            double w = 2.0 * Math.PI * freq / _sampleRate;
            _cos[i] = Math.Cos(w);
            _sin[i] = Math.Sin(w);
            _re[i] = 1.0;   // phaseur (cos, sin) : départ à sin 0
            _im[i] = 0.0;
            _amp[i] = amp;
            _decay[i] = Math.Exp(-decayPerSecond / _sampleRate);
        }

        public float NextSample()
        {
            if (!IsActive)
            {
                if (_declick == 0.0) return _last = 0f;
                _declick *= _declickMul;
                if (Math.Abs(_declick) < 1e-6) _declick = 0.0;
                return _last = (float)_declick;
            }
            double s = 0.0;
            for (int i = 0; i < _partials; i++)
            {
                s += _amp[i] * _im[i];
                double re = _re[i] * _cos[i] - _im[i] * _sin[i];
                _im[i] = _re[i] * _sin[i] + _im[i] * _cos[i];
                _re[i] = re;
                _amp[i] *= _decay[i];
            }
            // Renormalisation périodique des phaseurs (dérive numérique).
            if ((_sampleIndex & 1023) == 1023)
                for (int i = 0; i < _partials; i++)
                {
                    double n = 1.0 / Math.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]);
                    _re[i] *= n; _im[i] *= n;
                }
            double env = _sampleIndex < _attack ? _sampleIndex / (double)_attack : 1.0;
            if (_sampleIndex >= _fadeStart) env *= Math.Max(0.0, (_length - _sampleIndex) / (double)(_length - _fadeStart));
            double o = s * env * _gain + _declick;
            _declick *= _declickMul;
            if (++_sampleIndex >= _length) IsActive = false;
            return _last = (float)o;
        }
    }
}
