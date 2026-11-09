using System;
using System.Collections.Generic;
using TenGo.Core;

namespace TenGo.Audio
{
    /// Interface sonore du jeu (API publique de SoundManager.swift), sans Unity :
    /// chaque bulle joue sa note dans la gamme fixe, la tension du tracé fait
    /// monter d'une octave les deux dernières unités, une chaîne validée se
    /// résout en accord égrené qui s'étoffe avec la longueur, la victoire tient
    /// en trois notes et un accord. Branchée sur un `SynthEngine`.
    public sealed class SoundBoard
    {
        /// Anti-saturation des 8 voix sur un swipe très rapide.
        public const double ImmediateThrottle = 0.040;
        /// Décalage entre les notes d'un accord « gratté ».
        public const double StrumInterval = 0.025;

        readonly SynthEngine _engine;
        readonly Func<double> _clock;
        readonly List<double> _pathNotes = new();
        double _lastImmediateAt = double.NegativeInfinity;

        /// Réglage « son coupé » (`SettingsRepository.SoundMuted`), lu à chaque appel.
        public Func<bool> IsMutedSource = () => false;

        /// Rappel d'haptique de `playLose` (une vibration légère accompagne la note grave).
        public Action LoseHaptic;

        public SynthEngine Engine => _engine;
        public IReadOnlyList<double> PathNotes => _pathNotes;

        /// `clock` : horloge monotone en secondes (`CACurrentMediaTime`).
        public SoundBoard(SynthEngine engine, Func<double> clock)
        {
            _engine = engine;
            _clock = clock;
        }

        bool Muted
        {
            get
            {
                bool m;
                try { m = IsMutedSource?.Invoke() ?? false; } catch { m = false; }
                _engine.Muted = m;
                return m;
            }
        }

        /// Première bulle : la valeur détermine la note (toujours la même).
        public void PlaySelect(int bubbleValue)
        {
            if (Muted) return;
            double f = Scales.FrequencyFor(bubbleValue);
            _pathNotes.Clear();
            _pathNotes.Add(f);
            PlayImmediate(f, 0.45f);
        }

        /// Bulle suivante : `tension` (somme / 10) nuance le volume, et à 0,8 et
        /// plus la note monte d'une octave.
        public void PlayConnect(int bubbleValue, double tension = 0)
        {
            if (Muted) return;
            double f = Scales.FrequencyFor(bubbleValue);
            _pathNotes.Add(f);
            if (tension >= 0.8) f *= 2;
            PlayImmediate(f, (float)(0.45 + 0.2 * Math.Min(1, Math.Max(0, tension))));
        }

        /// Bulle refusée (elle ferait dépasser 10) : note grave et discrète.
        public void PlayRejected()
        {
            if (Muted) return;
            PlayImmediate(110.0, 0.30f);
        }

        public void PlayBacktrack()
        {
            if (_pathNotes.Count > 0) _pathNotes.RemoveAt(_pathNotes.Count - 1);
        }

        /// Chaîne validée : accord bref sur la dernière note tracée, quinte, puis
        /// octave (4+), puis tierce aiguë (6+), égrené de 25 ms.
        public void PlayCombo(int length = 0)
        {
            double b = _pathNotes.Count > 0 ? _pathNotes[_pathNotes.Count - 1] : Scales.Notes[0];
            _pathNotes.Clear();
            if (Muted) return;
            var chord = new List<double> { b, b * 1.498 };
            if (length >= 4) chord.Add(b * 2);
            if (length >= 6) chord.Add(b * 2.52);
            for (int i = 0; i < chord.Count; i++) _engine.Schedule(i * StrumInterval, chord[i], 0.55f);
        }

        public void CancelPath() => _pathNotes.Clear();

        /// Victoire : trois notes montantes, puis l'accord de la tonique à 0,40 s.
        public void PlayWin()
        {
            if (Muted) return;
            var n = Scales.Notes;
            double[] steps = { n[4], n[7], n[9] };
            for (int i = 0; i < steps.Length; i++) _engine.Schedule(i * 0.11, steps[i], 0.8f);
            double root = n[5];
            foreach (var f in new[] { root, root * 1.498, root * 2, root * 2.52 }) _engine.Schedule(0.40, f, 0.45f);
        }

        /// Défaite : une note grave, avec une vibration légère.
        public void PlayLose()
        {
            if (Muted) return;
            LoseHaptic?.Invoke();
            _engine.Trigger(98.0, 1f);
        }

        /// Notes simultanées.
        public void PlayChord(IEnumerable<double> frequencies, float velocity)
        {
            if (Muted) return;
            foreach (var f in frequencies) _engine.Trigger(f, velocity);
        }

        /// Note courte immédiate, sous le doigt (au plus une toutes les 40 ms).
        void PlayImmediate(double frequency, float velocity)
        {
            double now = _clock();
            if (now - _lastImmediateAt < ImmediateThrottle) return;
            _lastImmediateAt = now;
            _engine.Trigger(frequency, velocity, isShort: true);
        }
    }
}
