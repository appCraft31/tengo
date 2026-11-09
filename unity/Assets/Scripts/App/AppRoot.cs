using System;
using System.Collections.Generic;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Platform;
using TenGo.Platform.Legacy;
using TenGo.Rendering;
using TenGo.Screens;
using TenGo.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TenGo.App
{
    /// Racine de l'application (`GameViewController` + `SKView` d'iOS) : compose
    /// la sauvegarde, l'horloge, les dépôts, le son, l'haptique et le thème,
    /// porte la caméra (cadrage aspectFill 750 × 1334), lit l'entrée et route
    /// les écrans avec un fondu.
    ///
    /// Dans la scène `Main.unity`, `Start` démarre tout seul sur le fichier de
    /// sauvegarde de l'appareil. Les tests et le pilote automatique appellent
    /// `Boot(options)` sur une sauvegarde en mémoire et pilotent `Tick` à la main.
    public sealed class AppRoot : MonoBehaviour, IScreenRouter
    {
        public sealed class Options
        {
            /// Sauvegarde ; `null` = fichier de l'appareil + reprise des données natives.
            public ISaveStore Store;
            public IClock Clock;
            /// Écran simulé (pilote, tests) ; `null` = écran réel, relu à chaque image.
            public SceneGeometry? Geometry;
            /// Vrai : `Update` avance l'application (jeu). Faux : `Tick` appelé à la main.
            public bool AutoTick = true;
            /// Faux : ignore l'écran tactile et la souris (seuls les événements injectés comptent).
            public bool ReadDevices = true;
            public AppServices.Overrides Services;
            /// Langue imposée (sinon celle de l'appareil).
            public string Language;
            /// Faux : pas d'écran d'accueil au démarrage (tests).
            public bool ShowMenuOnBoot = true;
            /// Faux : pas de fondu entre écrans (tests, captures).
            public bool Fades = true;
            /// Lien du lancement à froid (tests) ; `null` = `Application.absoluteURL`.
            public string LaunchUrl;
            /// Faux : ni consentement ni Game Center au démarrage (captures).
            public bool StartServices = true;
        }

        public static AppRoot Instance { get; private set; }

        public AppServices Services { get; private set; }
        public SceneGeometry Geometry { get; private set; }
        public Camera Camera { get; private set; }
        public ShakeCamera Shake { get; private set; }
        public PointerInput Input { get; } = new();
        public IAppScreen Current { get; private set; }
        /// Fabrique des écrans hors partie.
        public IScreenFactory Screens { get; set; } = new ScreenFactory();
        public IOverlayFactory Overlays { get; set; } = new OverlayFactory();
        /// Liens entrants en attente (lancement à froid ou écran en transition).
        public DeepLinkInbox Links { get; } = new();
        /// Consentement recueilli et pubs démarrées (`isAdsSessionStarted`).
        public bool AdsSessionStarted { get; private set; }
        /// Le recueil du consentement a commencé (une seule fois par lancement).
        public bool ConsentRequested { get; private set; }
        public IScreenRouter Router => this;
        public bool Booted { get; private set; }

        Options _options;
        JsonFileSaveStore _fileStore;
        readonly List<PointerInput.Event> _events = new();
        bool _transitioning;

        // MARK: - Démarrage

        void Start()
        {
            if (!Booted) Boot(new Options());
        }

        /// Crée un `AppRoot` avec sa caméra (tests, pilote automatique).
        public static AppRoot Create(Options options)
        {
            var go = new GameObject("AppRoot");
            var app = go.AddComponent<AppRoot>();
            app.Boot(options);
            return app;
        }

        public void Boot(Options options)
        {
            if (Booted) return;
            Booted = true;
            Instance = this;
            _options = options ?? new Options();

            // 1. Sauvegarde, puis reprise des données natives AVANT tout dépôt qui écrit un défaut.
            ISaveStore store = _options.Store;
            if (store == null)
            {
                _fileStore = JsonFileSaveStore.OpenDefault();
                try { LegacyBootstrap.Run(_fileStore); }
                catch (Exception e) { Debug.LogException(e); }
                store = _fileStore;
            }
            var clock = _options.Clock ?? SystemClock.Instance;

            // 2. Textes, dépôts, services.
            AppServices.LoadStrings(_options.Language ?? DeviceLanguage.Current());
            Services = new AppServices(store, clock, _options.Services);
            Services.FirstRun.Bootstrap();
            SoundManager.Bind(Services.Settings);
            HapticManager.Bind(Services.Settings);
            JuiceSettings.Bind(Services.Settings);
            Services.ApplyLook();

            // 3. Caméra et entrée.
            SetupCamera();
            Input.ReadDevices = _options.ReadDevices;
            if (_options.ReadDevices) PointerInput.EnableTouch();
            Application.targetFrameRate = 60;

            // 4. Premier écran (GameViewController.viewDidLoad) : tout premier
            // lancement → tutoriel court qui mène droit en partie ; sinon l'accueil.
            if (_options.ShowMenuOnBoot)
            {
                bool firstRun = Services.FirstRun.IsPending && !Services.Settings.HasSeenTutorial;
                Show(firstRun ? Screens.Tutorial(this, true) : Screens.Menu(this), fade: false);
            }

            // 5. Liens entrants : celui du lancement à froid, puis ceux qui arrivent app ouverte.
            string launchUrl = _options.LaunchUrl ?? (_options.Store == null ? Application.absoluteURL : null);
            if (!string.IsNullOrEmpty(launchUrl)) Links.Receive(launchUrl);
            if (_options.Store == null) Application.deepLinkActivated += OnDeepLinkActivated;
            if (_options.ShowMenuOnBoot) ProcessPendingLinks();

            // 6. Consentement, Game Center, pubs (viewDidAppear).
            if (_options.StartServices) StartConsentFlow();
            FlushSave();
        }

        // MARK: - Liens profonds

        void OnDeepLinkActivated(string url) => HandleDeepLink(url);

        /// Lien reçu app ouverte (`DeepLink.handle` + notification) ; vrai si reconnu.
        public bool HandleDeepLink(string url)
        {
            if (!Links.Receive(url)) { Debug.Log("[DeepLink] lien ignoré"); return false; }
            ProcessPendingLinks();
            return true;
        }

        /// Ouvre la destination des liens en attente (`viewDidAppear` à froid).
        public void ProcessPendingLinks()
        {
            if (Links.ConsumeDaily()) Show(CreateGame(GameLaunch.ForDaily()));
            if (Links.ConsumeProgress()) Show(Screens.Profile(this));
            var code = Links.ConsumeDuelCode();
            if (code != null) Show(Screens.Duel(this, code));
        }

        // MARK: - Consentement et services

        /// `viewDidAppear` : au tout premier lancement, ni consentement ni Game
        /// Center avant la fin de la première partie (puis 1,6 s, le temps que le
        /// panneau de fin s'affiche, Game Center après la réponse). Sinon, 0,5 s
        /// après l'affichage, Game Center puis consentement.
        void StartConsentFlow()
        {
            if (ConsentRequested) return;
            if (Services.FirstRun.IsPending)
            {
                void OnFirst()
                {
                    Services.FirstRun.FirstGameCompleted -= OnFirst;
                    GatherConsent(1.6f, gameCenterAfterConsent: true);
                }
                Services.FirstRun.FirstGameCompleted += OnFirst;
                return;
            }
            GatherConsent(0.5f, gameCenterAfterConsent: false);
        }

        void GatherConsent(float delay, bool gameCenterAfterConsent)
        {
            ConsentRequested = true;
            if (!gameCenterAfterConsent) Services.Play.Authenticate();
            FxTime.AfterRealtime(delay, () =>
            {
                if (this == null) return;
                Services.Consent.Gather(personalized =>
                {
                    if (this == null) return;
                    if (gameCenterAfterConsent) Services.Play.Authenticate();
                    Debug.Log("[AdMob] Démarrage pubs — mode " + (personalized ? "personnalisé" : "non personnalisé") + " (simulé)");
                    AdsSessionStarted = true;
                    Services.Rewarded.PreloadIfNeeded();
                });
            });
        }

        void SetupCamera()
        {
            Camera = GetComponentInChildren<Camera>();
            if (Camera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camGo.transform.SetParent(transform, false);
                Camera = camGo.AddComponent<Camera>();
            }
            if (FindAnyObjectByType<AudioListener>() == null) Camera.gameObject.AddComponent<AudioListener>();
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            Camera.transform.localPosition = new Vector3(0, 0, -10);
            Shake = ShakeCamera.Attach(Camera);
            ApplyGeometry(_options.Geometry ?? SceneGeometry.Current(), force: true);
        }

        void ApplyGeometry(SceneGeometry g, bool force = false)
        {
            if (!force && g.SameAs(Geometry)) return;
            Geometry = g;
            Camera.orthographicSize = g.OrthoSize;
            Shake.SetRest(new Vector3(0, 0, -10), g.OrthoSize);
            Current?.GeometryChanged(g);
        }

        public void SetBackground(Color c)
        {
            if (Camera != null) Camera.backgroundColor = c;
        }

        // MARK: - Boucle

        void Update()
        {
            if (!Booted || !_options.AutoTick) return;
            // FxDriver avance déjà FxTime en jeu : ici, seulement l'entrée et l'écran.
            Tick(Time.unscaledDeltaTime);
        }

        /// Un pas : géométrie, entrée, écran. Le pilote appelle `FxTime.Step(dt)` avant.
        public void Tick(float dt)
        {
            if (_options.Geometry == null) ApplyGeometry(SceneGeometry.Current());
            Input.Poll(Geometry, _events);
            if (_options.ReadDevices && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Current?.Back();
            foreach (var e in _events)
            {
                if (Current == null || _transitioning) break;
                switch (e.Type)
                {
                    case PointerInput.Kind.Down: Current.PointerDown(e.Pt); break;
                    case PointerInput.Kind.Move: Current.PointerMove(e.Pt); break;
                    case PointerInput.Kind.Up: Current.PointerUp(e.Pt); break;
                    case PointerInput.Kind.Cancel: Current.PointerCancel(); break;
                }
            }
            Current?.Tick(dt);
        }

        // MARK: - Navigation

        /// Remplace l'écran courant, avec le fondu de 0,3 s d'iOS (`SceneTransition.fade`).
        public void Show(IAppScreen screen, bool fade = true)
        {
            if (screen == null) return;
            if (!fade || !_options.Fades || Current == null)
            {
                Swap(screen);
                return;
            }
            _transitioning = true;
            Input.Clear();
            screen.Root.gameObject.SetActive(false);
            var cover = new Vector2(Geometry.VisibleWidth, Geometry.VisibleHeight);
            SceneFader.Fade(transform, cover, () =>
            {
                screen.Root.gameObject.SetActive(true);
                Swap(screen);
                _transitioning = false;
            });
        }

        void Swap(IAppScreen screen)
        {
            var old = Current;
            Current = screen;
            screen.Root.SetParent(transform, false);
            screen.Root.localPosition = Vector3.zero;
            if (old != null)
            {
                old.Dispose();
                if (old.Root != null)
                {
                    if (Application.isPlaying) Destroy(old.Root.gameObject); else DestroyImmediate(old.Root.gameObject);
                }
            }
            Shake.ResetView();
        }

        public void ShowMenu() => Show(Screens.Menu(this));
        public void ShowPuzzleLevels(int world) => Show(Screens.PuzzleLevels(this, world));
        public void ShowDuel() => Show(Screens.Duel(this));
        public void ShowDuel(string incomingCode) => Show(Screens.Duel(this, incomingCode));
        public void ShowShop(bool returnToGame) => Show(Screens.Shop(this, returnToGame));
        public void ShowShop(bool returnToGame, bool guided) => Show(Screens.Shop(this, returnToGame, guided));
        /// Joueur qui n'a encore rien terminé : même tutoriel court que le premier lancement.
        public void ShowTutorial() => Show(Screens.Tutorial(this, Services.FirstRun.IsPending));
        public void ShowProfile() => Show(Screens.Profile(this));
        public void ShowMissions() => Show(Screens.Missions(this));
        public void ShowAchievements() => Show(Screens.Achievements(this));
        public void ShowMirror() => Show(Screens.Mirror(this));
        public void ShowLeaderboard() => Show(Screens.Leaderboard(this));

        /// Onglet de premier niveau, sans transition (`TabBar.present`) : les
        /// quatre écrans forment un même espace, pas une succession de pages.
        public void ShowTab(Tab tab)
        {
            IAppScreen screen = tab switch
            {
                Tab.Progress => Screens.Profile(this),
                Tab.Social => Screens.Duel(this),
                Tab.Shop => Screens.Shop(this, false),
                _ => Screens.Menu(this),
            };
            Show(screen, fade: false);
        }
        public void StartGame(GameLaunch launch) => Show(CreateGame(launch));

        /// Écran de partie (sans l'afficher) : le pilote et les tests s'en servent aussi.
        public GameController CreateGame(GameLaunch launch)
        {
            Services.ApplyLook();
            return new GameController(this, launch);
        }

        /// Partie courante, s'il y en a une.
        public GameController Game => Current as GameController;

        // MARK: - Sauvegarde

        /// Écrit la sauvegarde sur disque si elle a changé (fin de partie, pause, fermeture).
        public void FlushSave()
        {
            if (_fileStore == null || !_fileStore.Dirty) return;
            try { _fileStore.Flush(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || !Booted) return;
            Current?.AppPaused();
            FlushSave();
        }

        void OnApplicationQuit()
        {
            if (!Booted) return;
            Current?.AppPaused();
            FlushSave();
        }

        void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
            if (Booted) FlushSave();
            Current?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
