using System;

namespace TenGo.Audio
{
    /// Voix polyphonique (classe `Voice` de SoundManager.swift, Voice.kt) :
    /// oscillateur 85 % sinus + 15 % triangle, enveloppe ADSR. Deux profils :
    /// long (accords, victoire) 0,05 / 0,20 / maintien 0,25 / 2,00 s ; court
    /// (notes du tracé) 0,03 / 0,10 / 0,03 / 0,35 s. Palier 0,3, gain 0,6.
    /// Aucune allocation par échantillon.
    public sealed class SynthVoice
    {
        enum Stage { Idle, Attack, Decay, Sustain, Release }

        public const float SustainLevel = 0.3f;
        public const float VoiceGain = 0.6f;

        readonly double _sampleRate;
        readonly int[] _long, _short;
        int _attack, _decay, _hold, _release;
        Stage _stage = Stage.Idle;
        double _phase, _phaseIncrement;
        int _sampleIndex;
        float _currentEnv, _attackStartEnv, _velocity = 1f;

        public bool IsActive { get; private set; }
        /// Ordre de déclenchement (vol de la plus ancienne voix).
        public long StartTime { get; private set; }
        public double Frequency { get; private set; }
        public float Velocity => _velocity;
        public bool IsShort { get; private set; }

        public SynthVoice(double sampleRate)
        {
            _sampleRate = sampleRate;
            _long = new[] { S(0.05), S(0.20), S(0.25), S(2.00) };
            _short = new[] { S(0.03), S(0.10), S(0.03), S(0.35) };
            (_attack, _decay, _hold, _release) = (_long[0], _long[1], _long[2], _long[3]);
        }

        int S(double seconds) => Math.Max(1, (int)(seconds * _sampleRate));

        /// Déclenche une note. Phase conservée et attaque partant du niveau
        /// courant : un vol de voix ne claque pas.
        public void NoteOn(double frequency, long now, float velocity = 1f, bool isShort = false)
        {
            var p = isShort ? _short : _long;
            (_attack, _decay, _hold, _release) = (p[0], p[1], p[2], p[3]);
            _velocity = Math.Max(0f, Math.Min(1f, velocity));
            _phaseIncrement = frequency / _sampleRate;
            _attackStartEnv = _currentEnv;
            _sampleIndex = 0;
            _stage = Stage.Attack;
            StartTime = now;
            Frequency = frequency;
            IsShort = isShort;
            IsActive = true;
        }

        public float NextSample()
        {
            if (_stage == Stage.Idle) return 0f;
            float env;
            switch (_stage)
            {
                case Stage.Attack:
                    env = _attackStartEnv + (float)_sampleIndex / _attack * (1f - _attackStartEnv);
                    if (++_sampleIndex >= _attack) { _stage = Stage.Decay; _sampleIndex = 0; }
                    break;
                case Stage.Decay:
                    env = 1f - (float)_sampleIndex / _decay * (1f - SustainLevel);
                    if (++_sampleIndex >= _decay) { _stage = Stage.Sustain; _sampleIndex = 0; }
                    break;
                case Stage.Sustain:
                    env = SustainLevel;
                    if (++_sampleIndex >= _hold) { _stage = Stage.Release; _sampleIndex = 0; }
                    break;
                default:
                    env = SustainLevel * (1f - (float)_sampleIndex / _release);
                    if (++_sampleIndex >= _release)
                    {
                        _stage = Stage.Idle;
                        IsActive = false;
                        _currentEnv = 0f;
                        return 0f;
                    }
                    break;
            }
            _currentEnv = env;
            float sine = (float)Math.Sin(_phase * 2.0 * Math.PI);
            float tri = (float)(4.0 * Math.Abs(_phase - 0.5) - 1.0);
            float mixed = sine * 0.85f + tri * 0.15f;
            _phase += _phaseIncrement;
            if (_phase >= 1.0) _phase -= 1.0;
            return mixed * env * VoiceGain * _velocity;
        }
    }
}
