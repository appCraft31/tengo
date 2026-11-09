using TenGo.Haptics;
using UnityEngine;

namespace TenGo.Audio
{
    /// Sortie audio du synthé (`SoundManager.shared`) : objet caché portant une
    /// `AudioSource` sans clip dont `OnAudioFilterRead` rend le `SynthEngine`
    /// à la fréquence de sortie du système. Créé à la demande en jeu ; en
    /// éditeur hors jeu, `Shared` reste utilisable (moteur sans sortie).
    [AddComponentMenu("")]
    public sealed class SoundManager : MonoBehaviour
    {
        static SoundBoard _shared;
        static SoundManager _instance;
        SynthEngine _engine;

        /// Interface sonore partagée (créée au premier appel).
        public static SoundBoard Shared
        {
            get
            {
                if (_shared != null) return _shared;
                int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
                var engine = new SynthEngine(rate);
                _shared = new SoundBoard(engine, () => Time.realtimeSinceStartupAsDouble)
                {
                    LoseHaptic = HapticManager.Light,
                };
                if (Application.isPlaying) EnsureOutput(engine);
                return _shared;
            }
        }

        /// Branche le réglage « son coupé ».
        public static void Bind(Core.SettingsRepository settings) => Shared.IsMutedSource = () => settings.SoundMuted;

        static void EnsureOutput(SynthEngine engine)
        {
            if (_instance != null) return;
            var go = new GameObject("[Sound]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;
            _instance = go.AddComponent<SoundManager>();
            _instance._engine = engine;
            src.Play();   // sans clip : seul le filtre produit le son
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            var e = _engine;
            if (e == null) return;
            e.Render(data, channels);
        }

        void OnDestroy()
        {
            if (_instance == this) { _instance = null; _shared = null; }
        }

        /// Oublie l'interface partagée (tests).
        public static void ResetShared()
        {
            if (_instance != null) DestroyObject(_instance.gameObject);
            _instance = null;
            _shared = null;
        }

        static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
