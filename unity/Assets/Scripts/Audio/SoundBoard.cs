using System;
using System.Collections.Generic;
using TenGo.Core;

namespace TenGo.Audio
{
    /// Interface sonore du jeu (API publique de SoundManager.swift), sans Unity.
    /// Son 4.0 (render.py, variante B validée le 9 nov. 2026) : kalimba, gamme
    /// et tonalité tournantes d'une partie à l'autre (`BeginGame`, règle dans
    /// `Scales`), notes humanisées (±8 cents, vélocité ×[0,85 ; 1,05]). Dans
    /// une partie, chaque chiffre garde sa note ; la tension ≥ 0,8 la monte
    /// d'une octave ; une chaîne validée se résout en arpège montant vers la
    /// tonique, avec une cloche à partir de 6 bulles. Branchée sur un `SynthEngine`.
    public sealed class SoundBoard
    {
        /// Anti-saturation : au plus une note sous le doigt toutes les 70 ms.
        public const double ImmediateThrottle = 0.070;
        /// Humanisation : désaccord maximal en cents.
        public const double DetuneCents = 8.0;
        public const float VelocityJitterMin = 0.85f, VelocityJitterMax = 1.05f;
        /// Première bulle du tracé : 80 % de la vélocité de base.
        public const float FirstBubbleFactor = 0.8f;
        /// Arpège de chaîne : pas de 45 ms, 60 ms à partir de 5 bulles.
        public const double ArpeggioStep = 0.045, ArpeggioStepLong = 0.060;
        /// Degrés de l'arpège de chaîne (indices relatifs dans la gamme).
        static readonly int[] ArpeggioSteps = { 0, 2, 4, 5, 7, 9, 10 };
        /// Indice de la tonique de résolution (octave du milieu).
        public const int Tonic = 5;

        readonly SynthEngine _engine;
        readonly Func<double> _clock;
        readonly List<double> _pathNotes = new();
        readonly Random _rng;
        double _lastImmediateAt = double.NegativeInfinity;
        int _palette = 0;
        int _previousPalette = -1;
        double[] _notes = Scales.NotesFor(0);

        /// Réglage « son coupé » (`SettingsRepository.SoundMuted`), lu à chaque appel.
        public Func<bool> IsMutedSource = () => false;

        /// Rappel d'haptique de `playLose` (une vibration légère accompagne la défaite).
        public Action LoseHaptic;

        /// Humanisation des notes (désactivable pour les tests de fréquence exacte).
        public bool Humanize = true;

        public SynthEngine Engine => _engine;
        public IReadOnlyList<double> PathNotes => _pathNotes;
        /// Palette de la partie en cours (index dans `Scales.Palettes`).
        public int Palette => _palette;
        /// Notes de la palette en cours (15 notes sur trois octaves).
        public IReadOnlyList<double> Notes => _notes;

        /// `clock` : horloge monotone en secondes (`CACurrentMediaTime`).
        /// `seed` : graine de l'humanisation (aléatoire si absente).
        public SoundBoard(SynthEngine engine, Func<double> clock, int? seed = null)
        {
            _engine = engine;
            _clock = clock;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
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

        /// Nouvelle partie : choisit la palette d'après la grille de départ
        /// (`Scales.PaletteForGame`). `sharedGrid` : défi du jour, duel, puzzle, démo.
        public int BeginGame(ulong gridHash, bool sharedGrid)
        {
            SetPalette(Scales.PaletteForGame(gridHash, sharedGrid, _previousPalette));
            return _palette;
        }

        /// Impose une palette (rendu de contrôle, tests).
        public void SetPalette(int palette)
        {
            _palette = Scales.Wrap(palette);
            _previousPalette = _palette;
            _notes = Scales.NotesFor(_palette);
            _pathNotes.Clear();
            _lastImmediateAt = double.NegativeInfinity;
        }

        /// Fréquence (non humanisée) de la bulle `bubbleValue` dans la palette courante.
        public double FrequencyFor(int bubbleValue) => _notes[Scales.NoteIndex(bubbleValue)];

        double Detune() => Humanize ? Math.Pow(2.0, (_rng.NextDouble() * 2 - 1) * DetuneCents / 1200.0) : 1.0;
        float Jitter() => Humanize ? (float)(VelocityJitterMin + _rng.NextDouble() * (VelocityJitterMax - VelocityJitterMin)) : 1f;

        /// Première bulle : la valeur détermine la note (toujours la même dans la partie).
        public void PlaySelect(int bubbleValue)
        {
            if (Muted) return;
            double f = FrequencyFor(bubbleValue);
            _pathNotes.Clear();
            _pathNotes.Add(f);
            PlayImmediate(f, 0.45f * FirstBubbleFactor);
        }

        /// Bulle suivante : `tension` (somme / 10) nuance le volume, et à 0,8 et
        /// plus la note monte d'une octave.
        public void PlayConnect(int bubbleValue, double tension = 0)
        {
            if (Muted) return;
            double f = FrequencyFor(bubbleValue);
            _pathNotes.Add(f);
            if (tension >= 0.8) f *= 2;
            PlayImmediate(f, (float)(0.45 + 0.2 * Math.Min(1, Math.Max(0, tension))));
        }

        /// Bulle refusée (elle ferait dépasser 10) : « tok » mat, sans note grave.
        public void PlayRejected()
        {
            if (Muted) return;
            _engine.Trigger(VoiceKind.Tok, 150.0, 0.5f);
        }

        public void PlayBacktrack()
        {
            if (_pathNotes.Count > 0) _pathNotes.RemoveAt(_pathNotes.Count - 1);
        }

        /// Nombre de notes de l'arpège d'une chaîne de `length` bulles (2 à 7).
        public static int ArpeggioLength(int length) => Math.Max(2, Math.Min(ArpeggioSteps.Length, length));

        /// Chaîne validée : arpège montant de 2 à 7 notes selon la longueur (pas
        /// de 45 ms, 60 ms à partir de 5), résolu sur la tonique (une octave plus
        /// haut à partir de 6) ; à partir de 6 bulles, une cloche après l'arpège.
        public void PlayCombo(int length = 0)
        {
            int n = length > 0 ? length : _pathNotes.Count;
            _pathNotes.Clear();
            if (Muted) return;
            var N = _notes;
            int count = ArpeggioLength(n);
            int start = n < 5 ? 5 : 7;
            double step = n < 5 ? ArpeggioStep : ArpeggioStepLong;
            for (int i = 0; i < count; i++)
            {
                double f = i < count - 1
                    ? N[Math.Min(N.Length - 1, start + ArpeggioSteps[i])]
                    : N[Tonic] * (n >= 6 ? 2 : 1);
                _engine.Schedule(i * step, VoiceKind.KalimbaLong, f * Detune(), 0.5f * Jitter());
            }
            if (n >= 6) _engine.Schedule(count * ArpeggioStepLong, VoiceKind.Bell, N[10], 0.6f);
        }

        public void CancelPath() => _pathNotes.Clear();

        /// Victoire / record : arpège de 7 notes (70 ms) puis trois cloches montantes.
        public void PlayWin()
        {
            if (Muted) return;
            var N = _notes;
            int[] arp = { 5, 6, 7, 8, 9, 10, 12 };
            for (int i = 0; i < arp.Length; i++) _engine.Schedule(i * 0.07, VoiceKind.KalimbaLong, N[arp[i]], 0.55f);
            int[] bells = { 10, 12, 14 };
            for (int i = 0; i < bells.Length; i++) _engine.Schedule(0.55 + i * 0.12, VoiceKind.Bell, N[bells[i]], 0.7f);
        }

        /// Défaite : trois notes descendantes (220 ms), avec une vibration légère.
        public void PlayLose()
        {
            if (Muted) return;
            LoseHaptic?.Invoke();
            var N = _notes;
            int[] down = { 4, 2, 0 };
            for (int i = 0; i < down.Length; i++) _engine.Schedule(i * 0.22, VoiceKind.KalimbaLong, N[down[i]], 0.45f);
        }

        /// Notes simultanées.
        public void PlayChord(IEnumerable<double> frequencies, float velocity)
        {
            if (Muted) return;
            foreach (var f in frequencies) _engine.Trigger(VoiceKind.KalimbaLong, f, velocity);
        }

        /// Note courte immédiate, sous le doigt (au plus une toutes les 70 ms).
        void PlayImmediate(double frequency, float velocity)
        {
            double now = _clock();
            if (now - _lastImmediateAt < ImmediateThrottle) return;
            _lastImmediateAt = now;
            _engine.Trigger(VoiceKind.KalimbaShort, frequency * Detune(), velocity * Jitter());
        }
    }
}
