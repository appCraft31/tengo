using System;

namespace TenGo.Audio
{
    /// Moteur de synthèse temps réel (SoundManager.swift, SynthEngine.kt) :
    /// 8 voix → passe-bas 1000 Hz → réverbération 30 % → sortie à 50 %.
    /// Pur C# : rendu par `OnAudioFilterRead` en jeu (`SoundManager`) et hors
    /// ligne pour l'export WAV et les tests. Les notes différées (accord égrené,
    /// victoire) sont datées à l'échantillon près. Les appels du fil principal
    /// et le rendu du fil audio sont sérialisés par un verrou ; aucune
    /// allocation dans le rendu.
    public sealed class SynthEngine
    {
        public const int VoiceCount = 8;
        public const float MasterGain = 0.5f;
        const int MaxScheduled = 64;

        struct Pending
        {
            public bool Active;
            public long Due;
            public double Frequency;
            public float Velocity;
            public bool Short;
        }

        readonly object _lock = new();
        readonly SynthVoice[] _voices;
        readonly Lowpass _lowpass;
        readonly SchroederReverb _reverb;
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

        /// Déclenchements (fréquence, vélocité, court, instant en s), pour les tests et l'export.
        public Action<double, float, bool, double> OnTrigger;

        public SynthEngine(int sampleRate)
        {
            SampleRate = sampleRate;
            _voices = new SynthVoice[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) _voices[i] = new SynthVoice(sampleRate);
            _lowpass = new Lowpass(1000.0, sampleRate);
            _reverb = new SchroederReverb(sampleRate, 0.30f);
        }

        public int ActiveVoices
        {
            get { lock (_lock) { int n = 0; foreach (var v in _voices) if (v.IsActive) n++; return n; } }
        }

        /// Note immédiate : voix libre en priorité, sinon vol de la plus ancienne.
        public void Trigger(double frequency, float velocity = 1f, bool isShort = false)
        {
            lock (_lock) TriggerLocked(frequency, velocity, isShort);
        }

        void TriggerLocked(double frequency, float velocity, bool isShort)
        {
            OnTrigger?.Invoke(frequency, velocity, isShort, _clock / (double)SampleRate);
            SynthVoice target = null;
            foreach (var v in _voices) if (!v.IsActive) { target = v; break; }
            if (target == null)
            {
                target = _voices[0];
                foreach (var v in _voices) if (v.StartTime < target.StartTime) target = v;
            }
            target.NoteOn(frequency, ++_order, velocity, isShort);
        }

        /// Note différée de `delaySeconds` (`DispatchQueue.main.asyncAfter`).
        public void Schedule(double delaySeconds, double frequency, float velocity, bool isShort = false)
        {
            if (delaySeconds <= 0) { Trigger(frequency, velocity, isShort); return; }
            lock (_lock)
            {
                long due = _clock + (long)Math.Round(delaySeconds * SampleRate);
                for (int i = 0; i < MaxScheduled; i++)
                {
                    if (_pending[i].Active) continue;
                    _pending[i] = new Pending { Active = true, Due = due, Frequency = frequency, Velocity = velocity, Short = isShort };
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
                    if (!_muted) TriggerLocked(p.Frequency, p.Velocity, p.Short);
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
                    mix = _reverb.Process(_lowpass.Process(mix)) * MasterGain;
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
