using System;

namespace TenGo.Audio
{
    /// Moteur de synthèse temps réel (son 4.0, render.py variante B) :
    /// 12 voix → passe-bas 5000 Hz → réverbération à peignes 22 % (délais ×1,3)
    /// → sortie à 62 % → limiteur doux. render.py sort à 70 %, mais l'extrait
    /// validé (son_B) a été ramené au niveau de l'ancien son (−21,6 dBFS RMS
    /// sur la séquence type) : 62 % redonne ce niveau. 12 voix et non plus 8 : la victoire
    /// superpose 7 notes de kalimba et 3 cloches.
    /// Pur C# : rendu par `OnAudioFilterRead` en jeu (`SoundManager`) et hors
    /// ligne pour l'export WAV et les tests. Les notes différées (accord égrené,
    /// victoire) sont datées à l'échantillon près. Les appels du fil principal
    /// et le rendu du fil audio sont sérialisés par un verrou ; aucune
    /// allocation dans le rendu.
    public sealed class SynthEngine
    {
        public const int VoiceCount = 12;
        public const float MasterGain = 0.62f;
        public const double LowpassHz = 5000.0;
        public const float ReverbWet = 0.22f;
        public const double ReverbSize = 1.3;
        const int MaxScheduled = 64;

        struct Pending
        {
            public bool Active;
            public long Due;
            public double Frequency;
            public float Velocity;
            public VoiceKind Kind;
        }

        readonly object _lock = new();
        readonly SynthVoice[] _voices;
        readonly Lowpass _lowpass;
        readonly CombReverb _reverb;
        readonly Pending[] _pending = new Pending[MaxScheduled];
        long _clock;          // échantillons rendus
        long _order;          // ordre de déclenchement des voix
        long _nextDue = long.MaxValue;
        volatile bool _muted;

        public int SampleRate { get; }

        /// Son coupé : une note différée ne part pas (`guard !isMuted` de `after`).
        public bool Muted { get => _muted; set => _muted = value; }

        /// Temps rendu, en secondes.
        public double Time { get { lock (_lock) return _clock / (double)SampleRate; } }

        /// Déclenchements (fréquence, vélocité, timbre, instant en s), pour les tests et l'export.
        public Action<double, float, VoiceKind, double> OnTrigger;

        public SynthEngine(int sampleRate)
        {
            SampleRate = sampleRate;
            _voices = new SynthVoice[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) _voices[i] = new SynthVoice(sampleRate);
            _lowpass = new Lowpass(LowpassHz, sampleRate);
            _reverb = new CombReverb(sampleRate, ReverbWet, ReverbSize);
        }

        public int ActiveVoices
        {
            get { lock (_lock) { int n = 0; foreach (var v in _voices) if (v.IsActive) n++; return n; } }
        }

        /// Note immédiate : voix libre en priorité, sinon vol de la plus ancienne.
        public void Trigger(double frequency, float velocity = 1f, bool isShort = false) =>
            Trigger(isShort ? VoiceKind.KalimbaShort : VoiceKind.KalimbaLong, frequency, velocity);

        public void Trigger(VoiceKind kind, double frequency, float velocity)
        {
            lock (_lock) TriggerLocked(kind, frequency, velocity);
        }

        void TriggerLocked(VoiceKind kind, double frequency, float velocity)
        {
            OnTrigger?.Invoke(frequency, velocity, kind, _clock / (double)SampleRate);
            SynthVoice target = null;
            foreach (var v in _voices) if (!v.IsActive) { target = v; break; }
            if (target == null)
            {
                target = _voices[0];
                foreach (var v in _voices) if (v.StartTime < target.StartTime) target = v;
            }
            target.NoteOn(kind, frequency, ++_order, velocity);
        }

        /// Note différée de `delaySeconds` (`DispatchQueue.main.asyncAfter`).
        public void Schedule(double delaySeconds, double frequency, float velocity, bool isShort = false) =>
            Schedule(delaySeconds, isShort ? VoiceKind.KalimbaShort : VoiceKind.KalimbaLong, frequency, velocity);

        public void Schedule(double delaySeconds, VoiceKind kind, double frequency, float velocity)
        {
            if (delaySeconds <= 0) { Trigger(kind, frequency, velocity); return; }
            lock (_lock)
            {
                long due = _clock + (long)Math.Round(delaySeconds * SampleRate);
                for (int i = 0; i < MaxScheduled; i++)
                {
                    if (_pending[i].Active) continue;
                    _pending[i] = new Pending { Active = true, Due = due, Frequency = frequency, Velocity = velocity, Kind = kind };
                    if (due < _nextDue) _nextDue = due;
                    return;
                }
            }
        }

        /// Oublie les notes différées (changement d'écran, coupure du son).
        public void CancelScheduled()
        {
            lock (_lock)
            {
                for (int i = 0; i < MaxScheduled; i++) _pending[i].Active = false;
                _nextDue = long.MaxValue;
            }
        }

        void FireDue()
        {
            long next = long.MaxValue;
            for (int i = 0; i < MaxScheduled; i++)
            {
                ref var p = ref _pending[i];
                if (!p.Active) continue;
                if (p.Due <= _clock)
                {
                    p.Active = false;
                    if (!_muted) TriggerLocked(p.Kind, p.Frequency, p.Velocity);
                }
                else if (p.Due < next) next = p.Due;
            }
            _nextDue = next;
        }

        /// Rend `frames` images dans `buffer` (entrelacé sur `channels` canaux, écrasé).
        public void Render(float[] buffer, int channels, int frames = -1)
        {
            if (frames < 0) frames = buffer.Length / Math.Max(1, channels);
            lock (_lock)
            {
                int k = 0;
                for (int f = 0; f < frames; f++)
                {
                    if (_clock >= _nextDue) FireDue();
                    float mix = 0f;
                    for (int v = 0; v < _voices.Length; v++) mix += _voices[v].NextSample();
                    mix = SoftLimiter.Process(_reverb.Process(_lowpass.Process(mix)) * MasterGain);
                    for (int c = 0; c < channels; c++) buffer[k++] = mix;
                    _clock++;
                }
            }
        }

        /// Rendu hors ligne de `seconds` secondes en mono (export, tests).
        public float[] RenderMono(double seconds)
        {
            var o = new float[(int)(seconds * SampleRate)];
            Render(o, 1, o.Length);
            return o;
        }
    }
}
