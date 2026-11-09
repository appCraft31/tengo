using System;
using System.Collections.Generic;
using TenGo.App;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Platform;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    /// La partie : portage de GameScene.swift (iOS 3.3.0, qui fait foi). Six
    /// modes (normal, défi du jour, démo, Rush, puzzle, duel), tracé des
    /// chemins, gravité, twists, fleurs, boosters et coach-marks, vidéos
    /// récompensées, fin de partie et relances.
    ///
    /// Le code est réparti en fichiers partiels qui suivent les `MARK` du Swift :
    /// mise en page (Layout), toucher (Input), tracé et validation (Path),
    /// boosters (Boosters), vidéos (Rewarded), fin de partie et Rush (End),
    /// panneau de fin (Panel), relance, démo et navigation (Flow).
    ///
    /// Toutes les séquences passent par `Tw` sur la racine (les `SKAction` de la
    /// scène) : `FxTime` les avance, image par image en jeu, à la main dans le
    /// pilote automatique et les tests. Le chrono du Rush et les délais
    /// d'inactivité courent en temps réel (`FxTime.RealNow`), comme iOS.
    public sealed partial class GameController : IAppScreen
    {
        // MARK: - Constantes de mise en page

        public const float CellSize = 84f;
        public static readonly Vector2 SceneSize = new(750, 1334);

        static float GridOriginX => -(GameRules.Cols * CellSize) / 2f + CellSize / 2f;
        static float GridOriginY => -(GameRules.Rows * CellSize) / 2f + CellSize / 2f;
        static float GridTop => GridOriginY + (GameRules.Rows - 1) * CellSize;
        static float GridBottom => GridOriginY;

        static Vector2 P(float x, float y) => Units.P(x, y);

        // MARK: - Composition

        readonly AppRoot _app;
        readonly AppServices _s;
        readonly GameLaunch _launch;
        readonly bool _resuming;

        public GameMode Mode { get; }
        GameModeKind Kind => Mode.Kind;
        public Transform Root { get; }
        SceneGeometry _geometry;

        DailyToday _dailyToday;
        PuzzleLevel _puzzleLevel;
        readonly long _duelSeed;
        string _duelCode;
        readonly List<Transform> _obstacleNodes = new();

        // Démo
        readonly ulong _demoSeed;
        readonly double _demoSpeed = 1.0;
        int _demoRound;
        readonly int _demoMaxLen = 5;
        readonly PuzzleLevel _demoPuzzle;
        readonly List<List<Cell>> _demoScript;
        int _demoScriptIndex;

        // MARK: - État

        GridModel _grid = GridModel.Empty();
        readonly BubbleView[,] _nodes = new BubbleView[GameRules.Rows, GameRules.Cols];
        readonly List<Cell> _path = new();
        Transform _trailContainer;
        Transform _strokeNode;
        ShapeView _trailComet;

        bool _isAnimating;
        /// Une animation bloque l'entrée ; à sa fin, la main revient au joueur et
        /// un doigt resté posé reprend son geste (`isAnimating.didSet`).
        bool IsAnimating
        {
            get => _isAnimating;
            set
            {
                bool old = _isAnimating;
                _isAnimating = value;
                if (old && !value)
                {
                    _lastPlayerActionAt = FxTime.RealNow;
                    _boardReadyAt = FxTime.RealNow;
                    ResumePendingTouchIfNeeded();
                }
            }
        }

        Vector2? _pendingTouchPoint;

        // Score
        int _score;
        int _displayedScore;
        Transform _gameOverPanel;
        Transform _gameOverOverlay;
        bool _noAdsOfferResumesReplay;
        bool _isPurchasingNoAds;

        // Statistiques de la partie
        int _combosCreated;
        int _longestChain;
        bool _isWinState;
        TenGoText _duelStatusLabel;
        string _duelShareCode;
        /// Bilan de fin de partie (coordinateur) : pièces, XP, étoiles, miroir, graine.
        GameEndResult _lastResult;
        /// Le bilan a déjà été crédité pour cette fin de partie (garde-fou).
        int _gameEndsRecorded;

        // Rush
        public const float RushDuration = 60f;
        public const int RushBonusChainLength = 7;
        public const float RushBonusSeconds = 2f;
        double _rushTimeRemaining = RushDuration;
        TenGoText _rushTimerLabel;
        bool _rushEnded;
        bool _rushCountdownActive;
        bool _rushTimerRunning;
        bool _rushAwaitingBoard;
        readonly IRng _rushRng;

        // Boosters
        public const float BoosterRadius = 42f;
        static readonly Booster[] BoosterOrder = { Booster.Hint, Booster.Shuffle, Booster.Hammer };
        Transform _boosterBar;
        readonly List<(Booster booster, Transform node)> _boosterButtons = new();
        readonly Dictionary<Booster, TenGoText> _boosterCountLabels = new();
        bool _hammerArmed;
        float _boosterHitRadius = 48f;
        readonly List<Booster> _boosterCoachQueue = new();
        CoachMarkOverlay _boosterCoach;
        Transform _boosterShopPrompt;
        bool _hasPendingShopSnapshot;
        Transform _shopPillNode;

        // Vidéos récompensées (TEN-3)
        GridModel _secondChanceGrid;
        bool _secondChanceUsed;
        Transform _hintOfferNode;
        int _hintVideosThisGame;
        public const int MaxHintVideosPerGame = 2;
        public const double HintOfferIdleDelay = 20;
        double _lastPlayerActionAt;
        bool _rewardedInFlight;

        // Miroir cognitif et graines de dix
        MirrorTally _mirrorTally = new();
        double _boardReadyAt;
        double? _pathStartedAt;
        readonly HashSet<int> _lastFallen = new();
        bool _lastMoveHadFalls;

        // Juice
        Transform _fxLayer;
        JuiceDirector _juice;
        int _celebratedChain;

        float PathTension => Mathf.Min(1f, _grid.PathSum(_path) / 10f);

        // Interface
        TenGoText _pathGainLabel;
        TenGoText _scoreLabel;
        TenGoText _scoreCaption;
        TenGoText _messageLabel;
        Transform _homeBubbleNode;
        Transform _scoreBubbleNode;
        Transform _restartBubbleNode;
        Transform _settingsBubbleNode;
        Transform _homeGlyph;
        IGameOverlay _settingsOverlay;
        TenGoText _countdownLabel;
        bool _disposed;

        // MARK: - Accès pour le pilote automatique et les tests

        public GridModel Grid => _grid;
        public int Score => _score;
        public int DisplayedScore => _displayedScore;
        public bool Animating => _isAnimating;
        public bool IsGameOver => _gameOverPanel != null;
        public bool IsWin => _isWinState;
        public int Combos => _combosCreated;
        public int LongestChain => _longestChain;
        public IReadOnlyList<Cell> CurrentPath => _path;
        public bool RushCountdownActive => _rushCountdownActive;
        public double RushTimeRemaining => _rushTimeRemaining;
        public GameEndResult LastResult => _lastResult;
        public int GameEndsRecorded => _gameEndsRecorded;
        public bool HammerArmed => _hammerArmed;
        public bool CoachVisible => _boosterCoach != null;
        public bool HintOfferVisible => _hintOfferNode != null;
        public bool OffersContinue => _gameOverPanel != null && FindNamed(_gameOverPanel, "continueBtn") != null;
        public bool BoosterPromptVisible => _boosterShopPrompt != null;
        public BubbleView NodeAt(int row, int col) => _nodes[row, col];
        public string MessageText => _messageLabel != null && _messageLabel.gameObject.activeSelf ? _messageLabel.Text : null;
        public IReadOnlyList<Booster> VisibleBoosters
        {
            get { var l = new List<Booster>(); foreach (var b in _boosterButtons) l.Add(b.booster); return l; }
        }

        // MARK: - Construction (`init` + `sceneDidLoad` + `didMove(to:)`)

        public GameController(AppRoot app, GameLaunch launch)
        {
            _app = app;
            _s = app.Services;
            _launch = launch;
            Mode = launch.Mode;
            _resuming = launch.Resuming;
            _geometry = app.Geometry;
            Root = new GameObject("Game " + Mode.Encode()).transform;
            _rushRng = launch.RushSeed.HasValue ? new SeededGenerator(launch.RushSeed.Value) : SystemRng.Create();

            switch (Kind)
            {
                case GameModeKind.Daily:
                    _dailyToday = launch.Daily ?? DailyChallenge.Make(_s.Clock);
                    break;
                case GameModeKind.Puzzle:
                    _puzzleLevel = _s.Catalog.Level(Mode.World, Mode.Index);
                    break;
                case GameModeKind.Duel:
                    _duelSeed = Mode.Seed;
                    _duelCode = Mode.Code;
                    break;
                case GameModeKind.Demo:
                    _demoSeed = launch.DemoSeed;
                    _demoSpeed = Math.Max(0.25, launch.DemoSpeed);
                    _demoMaxLen = Math.Min(9, Math.Max(2, launch.DemoMaxLen));
                    _demoPuzzle = launch.DemoPuzzle;
                    _demoScript = launch.DemoScript;
                    break;
            }
            SceneDidLoad();
            DidMove();
        }

        void SceneDidLoad()
        {
            _app.SetBackground(Look.Background);
            // En capture marketing, le juice reste complet quelle que soit la configuration.
            if (Kind == GameModeKind.Demo) JuiceSettings.ForceFull = true;
            SetupJuice();
            SetupBackground();
            SetupUI();

            if (Kind == GameModeKind.Daily && _dailyToday != null)
            {
                _grid = _dailyToday.Grid;
            }
            else if (Kind == GameModeKind.Demo)
            {
                _grid = _demoPuzzle != null ? _demoPuzzle.MakeGrid() : GridModel.Generate(new SeededGenerator(_demoSeed));
            }
            else if (Kind == GameModeKind.Duel)
            {
                _grid = GridModel.Generate(new SeededGenerator(unchecked((ulong)_duelSeed)));
            }
            else if (Kind == GameModeKind.Puzzle && _puzzleLevel != null)
            {
                _grid = _puzzleLevel.MakeGrid();
            }
            else if (Kind == GameModeKind.Rush)
            {
                _grid = GridModel.Generate(_rushRng);
            }
            else if (_launch.Saved != null)
            {
                _grid = _launch.Saved.Grid;
                _score = _launch.Saved.Score;
                _displayedScore = _score;
                _scoreLabel.Text = _score.ToString();
            }
            else
            {
                _grid = GridModel.Generate(SystemRng.Create());
                PlantBloomedFlowerIfAny();
            }
            SetupGrid();

            // En démo : pas d'effets de bord, boutons parasites masqués, auto-player.
            if (Kind == GameModeKind.Demo)
            {
                _homeBubbleNode.gameObject.SetActive(false);
                _restartBubbleNode.gameObject.SetActive(false);
                StartDemo();
                return;
            }

            // Effets de bord de DÉBUT DE SESSION : une fois par partie (pas au
            // retour de la boutique).
            if (!_resuming)
            {
                GameStartInfo start = _s.Coordinator.OnGameStarted(Mode);
                if (start.StreakProtected) FlashMessage(Loc.T("streak.protected"), 2.4f);
                _s.Play.RegisterGameAndMaybeRequestNotifications();
            }
            SetupBoosters();

            if (Kind == GameModeKind.Rush) StartRushCountdown();
        }

        /// Couche d'effets + caméra de secousse (`setupJuice`).
        void SetupJuice()
        {
            _fxLayer = RenderShared.Node("FxLayer", Root);
            _juice = new JuiceDirector(_fxLayer, _app.Shake, SceneSize)
            {
                // Pas de secousse quand un panneau est ouvert : il bougerait avec la caméra.
                AllowsScreenEffects = () => _gameOverPanel == null && (_settingsOverlay == null || !_settingsOverlay.IsOpen),
            };
        }

        void SetupBackground() => ThemeBackground.Make(Root, Look.Theme, SceneSize);

        void DidMove()
        {
            _didMove = true;
            RepositionBottomRow();
            // La barre est maintenant à sa place : les coach-marks peuvent viser juste.
            QueueBoosterCoachMarks();
            SetupSettingsButton();
            HapticManager.Prepare();
            _s.Rewarded.PreloadIfNeeded();
            _lastPlayerActionAt = FxTime.RealNow;
            _boardReadyAt = FxTime.RealNow;
            ApplyQaHooks();
        }

        // MARK: - Boucle (`update(_:)`)

        public void Tick(float dt)
        {
            if (_disposed) return;
            if (Kind == GameModeKind.Normal) MaybeOfferHintVideo();
            UpdateRush(dt);
        }

        // MARK: - Outils

        /// `run(SKAction.sequence([wait, run]))` sur la scène.
        void After(float seconds, Action action, string key = null) =>
            Tw.Run(Root.gameObject, Tw.Seq(Tw.Wait(seconds), Tw.Call(() => { if (!_disposed) action(); })), key);

        static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
        }

        static void Kill(Transform t) { if (t != null) Kill(t.gameObject); }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform c in root) if (c.name == name) return c;
            return null;
        }

        /// `SKAction.sequence([scale 0.88, scale 1])` des boutons.
        static void Press(Transform t, float to = 0.88f, float down = 0.08f, float up = 0.12f)
        {
            if (t == null) return;
            Tw.Run(t.gameObject, Tw.Seq(Tw.Scale(t, to, down), Tw.Scale(t, 1f, up)), "press");
        }

        TenGoText Label(Transform parent, string text, NunitoWeight weight, float size, Color color, Vector2 pos, int order,
            TMPro.HorizontalAlignmentOptions align = TMPro.HorizontalAlignmentOptions.Center) =>
            TenGoText.Create(parent, text, weight, size, color, pos, order, horizontal: align);

        // MARK: - IAppScreen

        public void Back()
        {
            if (Kind == GameModeKind.Demo) return;
            if (_settingsOverlay != null && _settingsOverlay.IsOpen) { _settingsOverlay.Close(); return; }
            HapticManager.Light();
            GoBackToMenu();
        }

        /// Arrière-plan : la partie normale en cours est sauvegardée (comme
        /// Android ; iOS ne la sauvegardait qu'au retour à l'accueil).
        public void AppPaused()
        {
            if (Kind == GameModeKind.Normal && _gameOverPanel == null && !_grid.IsGridEmpty() && !_isAnimating)
                _s.Games.Save(_grid, _score);
        }

        public void GeometryChanged(SceneGeometry g)
        {
            _geometry = g;
            RepositionBottomRow();
            PlaceSettingsButton();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _boosterCoach?.Dismiss();
            _juice?.ResetTimeScale();
            SoundManager.Shared.CancelPath();
            JuiceSettings.ForceFull = false;
            Tw.Kill(Root.gameObject);
        }
    }
}
