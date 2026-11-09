using System.Collections.Generic;
using System.Globalization;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Services;
using TMPro;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Panneau de fin de partie, offre « sans pub »
    public sealed partial class GameController
    {
        const int OverlayZ = 380;
        const int PanelZ = 400;
        const float PanelW = 500, PanelH = 680;

        static readonly Color ReplayGreen = new(0.82f, 0.95f, 0.88f, 1);
        static readonly Color DailyLavender = new(0.86f, 0.82f, 0.97f, 1);
        static readonly Color RushPeach = new(1.0f, 0.89f, 0.78f, 1);
        static readonly Color HomeBeige = new(0.94f, 0.91f, 0.88f, 1);
        static readonly Color BtnStroke = Colors.White(0.68f, 0.30f);

        Transform _noAdsOffer;
        double _noAdsOfferShownAt = double.NegativeInfinity;
        const double NoAdsOfferTapGuard = 0.7;
        static readonly Vector2 NoAdsCardSize = new(500, 76);
        static readonly Rect NoAdsDeclineRect = new(-110, -102, 220, 50);

        string EncouragementText(bool isNewRecord, bool nearMiss)
        {
            if (_isWinState) return Loc.T("game_over.encourage_perfect");
            if (isNewRecord) return Loc.T("game_over.encourage_record");
            if (nearMiss) return Loc.T("game_over.encourage_close");
            int seed = _lastResult?.TotalChainsMade ?? _s.Stats.TotalChainsMade;
            return Loc.T(seed % 2 == 0 ? "game_over.encourage_progress" : "game_over.encourage_again");
        }

        void ShowGameOverPanel()
        {
            if (_disposed || _gameOverPanel != null) return;
            SyncDisplayedScore();
            _boosterCoach?.Dismiss();
            _boosterCoach = null;
            _hammerArmed = false;
            _messageLabel.gameObject.SetActive(false);
            if (_boosterBar != null) _boosterBar.gameObject.SetActive(false);
            DismissHintOffer();
            var result = _lastResult;

            // Voile plein écran.
            _gameOverOverlay = RenderShared.Node("GameOverOverlay", Root);
            var dim = ShapeView.Rect("Dim", _gameOverOverlay, Vector2.zero, SceneSize * 2, 0, Colors.White(0, 0.32f), default, 0, OverlayZ);
            FxAlpha.Of(_gameOverOverlay.gameObject).Alpha = 0;
            Tw.Run(_gameOverOverlay.gameObject, Tw.Fade(_gameOverOverlay.gameObject, 1, 0.3f));

            var panel = RenderShared.Node("GameOverPanel", Root);
            FxAlpha.Of(panel.gameObject).Alpha = 0;
            _gameOverPanel = panel;

            ShapeView.Rect("Shadow", panel, P(3, -8), new Vector2(PanelW + 6, PanelH + 6), 36, Colors.White(0, 0.14f), default, 0, PanelZ - 1);
            ShapeView.Rect("Bg", panel, Vector2.zero, new Vector2(PanelW, PanelH), 36, new Color(0.97f, 0.95f, 0.92f, 0.99f), Colors.White(0.72f, 0.30f), 1, PanelZ);

            string titleText = Kind switch
            {
                GameModeKind.Duel => Loc.T("duel.finished_title"),
                GameModeKind.Puzzle => Loc.T(_isWinState ? "puzzle.solved_title" : "puzzle.stuck_title"),
                GameModeKind.Rush => Loc.T("rush.time_up_title"),
                _ => Loc.T(_isWinState ? "game_over.perfect_title" : "game_over.game_over_title"),
            };
            Label(panel, titleText, Fonts.AvenirHeavy, 44, Colors.White(0.24f, 1), P(0, 278), PanelZ + 3).ShrinkToFit(PanelW - 40);

            var scoreDisplay = Label(panel, "0 " + Loc.T("game.points_label"), Fonts.AvenirHeavy, 64, Colors.White(0.26f, 1), P(0, 130), PanelZ + 3);
            AnimateScoreCountUp(scoreDisplay, _score);

            // Pièces et XP gagnées, sur une ligne.
            int coins = result?.CoinsEarned ?? 0;
            int xp = result?.Xp?.XpGained ?? 0;
            if (coins > 0 || xp > 0)
            {
                var line = RenderShared.Node("Rewards", panel, P(0, 198));
                var pieces = new List<(Transform node, float width)>();
                if (coins > 0)
                {
                    var g = RenderShared.Node("Coins", line);
                    var l = Label(g, "+" + coins, Fonts.AvenirBold, 19, new Color(0.45f, 0.34f, 0.10f, 1), Vector2.zero, PanelZ + 5, HorizontalAlignmentOptions.Left);
                    const float gap = 8;
                    float content = 22 + gap + l.WidthPt();
                    float width = content + 32;
                    ShapeView.Rect("Chip", g, Vector2.zero, new Vector2(width, 40), 20, new Color(0.99f, 0.95f, 0.80f, 1), default, 0, PanelZ + 2);
                    CoinIcon.Make(g, P(-content / 2 + 11, 0), 11, PanelZ + 3);
                    l.transform.localPosition = P(-content / 2 + 22 + gap, 0);
                    pieces.Add((g, width));
                }
                if (xp > 0)
                {
                    var g = RenderShared.Node("Xp", line);
                    var l = Label(g, Loc.F("game_over.xp_gained", xp), Fonts.AvenirBold, 19, new Color(0.20f, 0.34f, 0.68f, 1), Vector2.zero, PanelZ + 5);
                    float width = l.WidthPt() + 32;
                    ShapeView.Rect("Chip", g, Vector2.zero, new Vector2(width, 40), 20, new Color(0.89f, 0.92f, 1.00f, 1), default, 0, PanelZ + 2);
                    pieces.Add((g, width));
                }
                const float interGap = 12;
                float total = interGap * Mathf.Max(0, pieces.Count - 1);
                foreach (var p in pieces) total += p.width;
                float cursor = -total / 2;
                foreach (var p in pieces)
                {
                    p.node.localPosition = P(cursor + p.width / 2, 0);
                    cursor += p.width + interGap;
                }
            }

            // Passage de niveau.
            bool leveledUp = result?.Xp is GainResult gain && gain.LeveledUp;
            if (leveledUp)
            {
                var gr = result.Xp.Value;
                PopEffects.MakeBurst(panel, P(0, 240), Look.Accent, PopTier.Small);
                string text = Loc.T("game_over.level_up") + " " + Loc.F("game_over.new_level", gr.NewLevel, Loc.T(LevelCurve.TitleKey(gr.NewLevel)));
                Label(panel, text, Fonts.AvenirBold, 16, new Color(0.31f, 0.52f, 0.85f, 1), P(0, 240), PanelZ + 6).ShrinkToFit(PanelW - 40);
            }

            if (Kind == GameModeKind.Duel)
            {
                _duelStatusLabel = Label(panel, Loc.T("duel.publishing"), Fonts.AvenirDemiBold, 17, Colors.White(0.32f, 1), P(0, 86), PanelZ + 3);
                // Le réseau a pu répondre avant l'ouverture du panneau.
                if (_pendingDuel != null) { var d = _pendingDuel; _pendingDuel = null; UpdateDuelStatus(d); }
            }

            bool puzzleStars = Kind == GameModeKind.Puzzle || _demoPuzzle != null;
            if (puzzleStars)
            {
                int stars = result?.PuzzleStars ?? 0;
                var color = _isWinState ? new Color(0.95f, 0.72f, 0.20f, 1) : Colors.White(0.60f, 1);
                for (int i = 0; i < 3; i++)
                    Glyphs.Star(panel, P((i - 1) * 36, 90), 34, color, i < stars, PanelZ + 3);
            }

            bool isNewRecord = result?.IsNewRecord ?? false;
            int? nearMiss = result?.NearMissGap;
            if (puzzleStars || Kind == GameModeKind.Duel) { }
            else if (isNewRecord)
            {
                PopEffects.MakeBurst(panel, P(0, 84), new Color(0.96f, 0.78f, 0.28f, 1), PopTier.Medium);
                var record = Label(panel, Loc.T("game_over.new_record"), Fonts.AvenirHeavy, 27, new Color(0.88f, 0.60f, 0.14f, 1), P(0, 84), PanelZ + 6);
                record.ShrinkToFit(PanelW - 40);
                float s = record.transform.localScale.x;
                record.transform.localScale = Vector3.one * 0.6f * s;
                FxAlpha.Of(record.gameObject).Alpha = 0;
                Tw.Run(record.gameObject, Tw.Seq(Tw.Wait(0.45f), Tw.Par(Tw.FadeIn(record.gameObject, 0.2f), Tw.Scale(record.transform, s, 0.28f))));
            }
            else if (nearMiss is int gap)
            {
                Label(panel, Loc.F("game_over.near_miss", gap), Fonts.AvenirBold, 21, new Color(0.88f, 0.45f, 0.24f, 1), P(0, 84), PanelZ + 3).ShrinkToFit(PanelW - 40);
            }
            else if (BestFor(Kind) is int best)
            {
                Label(panel, Loc.F("game_over.best_score", best), Fonts.AvenirMedium, 19, Colors.White(0.38f, 1), P(0, 84), PanelZ + 3);
            }

            if (!leveledUp)
                Label(panel, EncouragementText(isNewRecord, nearMiss != null), Fonts.AvenirMedium, 18, Colors.White(0.44f, 1), P(0, 240), PanelZ + 3).Wrap(PanelW - 70);

            // Tuiles de statistiques.
            var stats = new[]
            {
                (Loc.F("game_over.longest_chain", _longestChain), Look.BubbleColor(6)),
                (Loc.F("game_over.combos_created", _combosCreated), Look.BubbleColor(4)),
            };
            const float statW = 216, statH = 56;
            for (int i = 0; i < stats.Length; i++)
            {
                var pos = P((i == 0 ? -1 : 1) * (statW / 2 + 6), 10);
                ShapeView.Rect("Stat", panel, pos, new Vector2(statW, statH), 20, stats[i].Item2.MixedWithWhite(0.62f), default, 0, PanelZ + 2);
                Label(panel, stats[i].Item1, Fonts.AvenirDemiBold, 15, Colors.White(0.26f, 1), pos, PanelZ + 3).ShrinkToFit(statW - 20);
            }

            AddMirrorCard(panel, PanelW - 56, -84);

            PuzzleLevel nextPuzzle = null;
            if (Kind == GameModeKind.Puzzle && _isWinState && _demoPuzzle == null && _puzzleLevel != null)
                nextPuzzle = _s.Catalog.Level(_puzzleLevel.World, _puzzleLevel.Index + 1);
            bool offerContinue = Kind == GameModeKind.Normal && _secondChanceGrid != null;

            if (offerContinue)
            {
                var btn = PanelButton(panel, "continueBtn", P(0, -184), new Vector2(320, 68), Look.Accent, 6);
                var ink = Look.Accent.ReadableInk();
                var title = Label(btn, Loc.T("game_over.continue"), Fonts.AvenirBold, 22, ink, Vector2.zero, PanelZ + 5, HorizontalAlignmentOptions.Left);
                float rowW = 22 + 8 + title.WidthPt();
                VectorIcon.Create(btn, VectorIconName.IconPlay, P(-rowW / 2 + 11, 9), 22, ink, PanelZ + 5);
                title.transform.localPosition = P(-rowW / 2 + 30, 9);
                Label(btn, Loc.T("game_over.continue_hint"), Fonts.AvenirDemiBold, 12, ink.WithAlpha(0.75f), P(0, -16), PanelZ + 5).ShrinkToFit(290);
            }
            else if (Kind == GameModeKind.Normal || Kind == GameModeKind.Rush || Kind == GameModeKind.Puzzle || _demoPuzzle != null)
            {
                var btn = PanelButton(panel, nextPuzzle == null ? "replayBtn" : "nextPuzzleBtn", P(0, -184), new Vector2(320, 68), ReplayGreen, 6);
                string text = nextPuzzle != null ? Loc.T("tutorial.next_button") : Loc.T(nearMiss != null ? "game_over.beat_record" : "game_over.replay");
                var ink = Colors.White(0.22f, 1);
                var label = Label(btn, text, Fonts.AvenirBold, 24, ink, Vector2.zero, PanelZ + 5, HorizontalAlignmentOptions.Left);
                float iconW = nextPuzzle == null ? 20 : 0, iconGap = nextPuzzle == null ? 12 : 0;
                float contentW = iconW + iconGap + label.WidthPt();
                if (contentW > 290) { label.ShrinkToFit(290 - iconW - iconGap); contentW = 290; }
                if (nextPuzzle == null) VectorIcon.Create(btn, VectorIconName.LineRestart, P(-contentW / 2 + iconW / 2, 1), 28, ink, PanelZ + 5);
                label.transform.localPosition = P(-contentW / 2 + iconW + iconGap, 0);
            }
            else if (Kind == GameModeKind.Duel)
            {
                // Le bouton de partage n'apparaît qu'une fois le code connu (UpdateDuelStatus).
            }
            else
            {
                var btn = PanelButton(panel, "dailyLeaderboardBtn", P(0, -184), new Vector2(320, 68), DailyLavender, 6);
                Label(btn, Loc.T("game_over.daily_leaderboard"), Fonts.AvenirMedium, 22, Colors.White(0.26f, 1), Vector2.zero, PanelZ + 5).ShrinkToFit(290);
            }

            // Ligne du bas : Accueil, et la relance contextuelle.
            (string name, string title, Color color, bool replayGlyph)? nextUp = null;
            if (offerContinue) nextUp = ("replayBtn", Loc.T("game_over.replay"), ReplayGreen, true);
            else if (result != null && Kind != GameModeKind.Demo)
            {
                if (result.Suggestion == NextSuggestion.NextDaily) nextUp = ("nextDailyBtn", Loc.T("menu.daily"), DailyLavender, false);
                else if (result.Suggestion == NextSuggestion.NextRush) nextUp = ("nextRushBtn", Loc.T("menu.rush"), RushPeach, false);
            }
            float homeW = nextUp == null ? 200 : 152;
            float homeX = nextUp == null ? 0 : -84;
            var home = PanelButton(panel, "homePanelBtn", P(homeX, -262), new Vector2(homeW, 52), HomeBeige, 4);
            Label(home, Loc.T("game_over.home"), Fonts.AvenirMedium, 19, Colors.White(0.42f, 1), Vector2.zero, PanelZ + 5).ShrinkToFit(homeW - 20);
            if (nextUp is { } nu)
            {
                var next = PanelButton(panel, nu.name, P(84, -262), new Vector2(152, 52), nu.color, 4);
                var ink = Colors.White(0.26f, 1);
                var l = Label(next, nu.title, Fonts.AvenirDemiBold, 17, ink, Vector2.zero, PanelZ + 5, nu.replayGlyph ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Center);
                if (nu.replayGlyph)
                {
                    l.ShrinkToFit(100);
                    float w = 16 + 10 + l.WidthPt() * l.transform.localScale.x;
                    VectorIcon.Create(next, VectorIconName.LineRestart, P(-w / 2 + 8, 0), 20, ink, PanelZ + 5);
                    l.transform.localPosition = P(-w / 2 + 26, 0);
                }
                else l.ShrinkToFit(136);
            }

            panel.localScale = Vector3.one * 0.88f;
            Tw.Run(panel.gameObject, Tw.Par(Tw.FadeIn(panel.gameObject, 0.38f), Tw.Scale(panel, 1f, 0.38f)), "appear");

            // Interstitielle automatique 1,5 s après le panneau.
            if (Kind != GameModeKind.Demo)
                After(1.5f, () =>
                {
                    if (_gameOverPanel == null) return;
                    _s.Interstitials.MaybeShow(AdTrigger.GameOverAuto, shown => { if (shown && !_disposed) PresentNoAdsOffer(false); });
                });

            if (Kind != GameModeKind.Demo && !_s.AdFree.IsPurchased && _s.AdFreeStore.DisplayPrice == null)
                _s.AdFreeStore.LoadProducts();
        }

        int? BestFor(GameModeKind kind)
        {
            var board = kind == GameModeKind.Rush ? _s.Games.RushHighScores : _s.Games.HighScores;
            return board.Count > 0 ? board[0] : (int?)null;
        }

        /// Bouton du panneau : capsule en relief, zone tactile nommée.
        Transform PanelButton(Transform panel, string name, Vector2 pos, Vector2 size, Color fill, float depth)
        {
            var node = RenderShared.Node(name, panel, pos);
            FxAlpha.Of(node.gameObject);
            Relief.Raise(ShapeView.Rect("Bg", node, Vector2.zero, size, size.y / 2, fill, BtnStroke, 1, PanelZ + 2), depth);
            AddZone(name, node, size);
            return node;
        }

        void AnimateScoreCountUp(TenGoText label, int target)
        {
            string pts = Loc.T("game.points_label");
            const int steps = 24;
            const float duration = 0.55f;
            var seq = new List<Tween>();
            for (int i = 1; i <= steps; i++)
            {
                int v = (int)((double)target * i / steps);
                seq.Add(Tw.Call(() => { if (label != null) label.Text = v + " " + pts; }));
                seq.Add(Tw.Wait(duration / steps));
            }
            seq.Add(Tw.Call(() => { if (label != null) { label.Text = target + " " + pts; label.ShrinkToFit(PanelW - 30); } }));
            Tw.Run(label.gameObject, Tw.Seq(seq.ToArray()), "countUp");
        }

        void HandlePanelTouch(Vector2 point)
        {
            string name = ZoneAt(point, n => n.EndsWith("Btn"));
            if (name == null) return;
            HapticManager.Light();
            var node = FindNamed(_gameOverPanel, name);
            switch (name)
            {
                case "replayBtn":
                    Press(node, 0.93f, 0.07f, 0.1f);
                    After(0.12f, () => _s.Interstitials.MaybeShow(AdTrigger.Replay, shown =>
                    {
                        if (_disposed) return;
                        // Une pub vient de passer : on propose de s'en passer, le panneau reste.
                        if (shown && PresentNoAdsOffer(true)) return;
                        ResetGame();
                    }));
                    break;
                case "continueBtn":
                    Press(node, 0.93f, 0.07f, 0.1f);
                    WatchSecondChanceVideo();
                    break;
                case "nextPuzzleBtn":
                    Press(node, 0.93f, 0.07f, 0.1f);
                    After(0.12f, () =>
                    {
                        if (_puzzleLevel == null || _s.Catalog.Level(_puzzleLevel.World, _puzzleLevel.Index + 1) is not PuzzleLevel next) return;
                        _app.Router.StartGame(App.GameLaunch.Puzzle(next.World, next.Index));
                    });
                    break;
                case "dailyLeaderboardBtn":
                    Press(node, 0.93f, 0.07f, 0.1f);
                    _s.Play.ShowLeaderboard(Leaderboard.Daily);
                    break;
                case "homePanelBtn":
                    After(0.08f, GoBackToMenu);
                    break;
                case "duelShareBtn":
                    if (_duelShareCode == null) return;
                    Press(node, 0.93f, 0.07f, 0.1f);
                    _s.Play.ShareDuel(_duelShareCode);
                    break;
                case "nextDailyBtn":
                case "nextRushBtn":
                    Press(node, 0.93f, 0.07f, 0.1f);
                    bool toDaily = name == "nextDailyBtn";
                    After(0.12f, () => _app.Router.StartGame(toDaily ? App.GameLaunch.ForDaily() : App.GameLaunch.Rush()));
                    break;
            }
        }

        // MARK: - Carte « miroir » et graine

        void AddMirrorCard(Transform panel, float width, float centerY)
        {
            var insight = _lastResult?.MirrorInsight;
            var outcome = _lastResult?.SeedOutcome;
            if (insight == null && outcome == null) return;
            const float h = 108, gap = 10;
            bool both = insight != null && outcome != null;
            float mirrorW = both ? (width - gap) * 0.59f : width;
            float seedW = both ? width - gap - mirrorW : width;
            var ink = new Color(0.2f, 0.19f, 0.26f, 1);
            int z = PanelZ + 2;

            Transform Tile(float w, float x, Color color)
            {
                var node = RenderShared.Node("Tile", panel, P(x, centerY));
                ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(w, h), 20, color, default, 0, z);
                return node;
            }
            TenGoText Text(Transform parent, string s, float size, NunitoWeight weight, Color color, float maxW, Vector2 pos,
                HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Center)
            {
                var l = Label(parent, s, weight, size, color, pos, z + 3, align);
                return l.ShrinkToFit(maxW);
            }

            if (insight is MirrorInsight ins)
            {
                float x = both ? -width / 2 + mirrorW / 2 : 0;
                var t = Tile(mirrorW, x, new Color(0.94f, 0.92f, 1, 1));
                float inner = mirrorW - 24;
                void Caption(string s) => Text(t, s, 13, Fonts.AvenirDemiBold, ink.WithAlpha(0.55f), inner, P(0, 38));
                void Equation(IReadOnlyList<int?> values)
                {
                    float d = values.Count <= 4 ? 30 : values.Count <= 6 ? 24 : 18;
                    var row = RenderShared.Node("Equation", t, P(0, 4));
                    var eq = Label(row, "= 10", Fonts.AvenirHeavy, d * 0.55f, ink, Vector2.zero, z + 3, HorizontalAlignmentOptions.Left);
                    float total = values.Count * (d + 3) + eq.WidthPt() + 3;
                    row.localScale = Vector3.one * Mathf.Min(1, inner / total);
                    float cx = -total / 2 + d / 2;
                    foreach (var v in values)
                    {
                        ShapeView.Circle("B", row, P(cx, 0), d / 2, v is int iv ? Look.BubbleColor(iv) : SeedArt.GaugeEmpty, default, 0, z + 1);
                        Label(row, v?.ToString() ?? "?", Fonts.AvenirBold, d * 0.5f, v == null ? ink.WithAlpha(0.35f) : Colors.White(0.25f, 1), P(cx, 0), z + 3);
                        cx += d + 3;
                    }
                    eq.transform.localPosition = P(cx - d / 2 + 3, 0);
                }
                void Gauge(int found, int total)
                {
                    var count = Text(t, found + "/" + total, 13, Fonts.AvenirHeavy, ink.WithAlpha(0.7f), 60, P(inner / 2, -34), HorizontalAlignmentOptions.Right);
                    float barW = inner - count.WidthPt() - 8;
                    float step = barW / Mathf.Max(1, total);
                    for (int i = 0; i < total; i++)
                        ShapeView.Rect("Seg", t, P(-inner / 2 + step * (i + 0.5f), -34), new Vector2(step * 0.76f, 8), step * 0.38f,
                            i < found ? Look.BubbleColor(i % 9 + 1) : SeedArt.GaugeEmpty, default, 0, z + 1);
                }
                void Metric(string title, string value, string delta)
                {
                    Caption(title);
                    Text(t, value, 30, Fonts.AvenirHeavy, ink, inner, P(0, 4));
                    Text(t, delta, 14, Fonts.AvenirBold, new Color(0.24f, 0.58f, 0.34f, 1), inner, P(0, -34));
                }
                switch (ins.Kind)
                {
                    case MirrorInsightKind.Discovered:
                        Caption(Loc.T("mirror.end.new_way"));
                        var vals = new List<int?>();
                        foreach (var part in ins.Key.Split('+')) if (int.TryParse(part, out int pv)) vals.Add(pv);
                        Equation(vals);
                        Gauge(ins.Total, Partition.Total);
                        break;
                    case MirrorInsightKind.NextTarget:
                        Caption(Loc.F("mirror.end.to_find", ins.Length));
                        Equation(new int?[ins.Length]);
                        Gauge(ins.Found, ins.Total);
                        break;
                    // « ↑ » d'iOS n'existe pas dans Nunito : la tendance est dite sans flèche.
                    case MirrorInsightKind.Faster:
                        Metric(Loc.T("mirror.skill.look"), Loc.F("mirror.unit.seconds", Dec(ins.Seconds)), Loc.F("mirror.trend.faster", Dec(ins.Gain)));
                        break;
                    case MirrorInsightKind.Longer:
                        Metric(Loc.T("mirror.skill.vision"), Dec(ins.Average), Loc.F("mirror.trend.more", Dec(ins.Average - ins.Usual)));
                        break;
                    case MirrorInsightKind.Anticipation:
                        Metric(Loc.T("mirror.skill.anticipation"), ins.Percent + " %", Loc.F("mirror.trend.points_up", ins.Percent - ins.UsualPercent));
                        break;
                }
            }

            if (outcome is SeedOutcome o)
            {
                float x = both ? width / 2 - seedW / 2 : 0;
                var t = Tile(seedW, x, new Color(1, 0.96f, 0.86f, 1));
                int? power = null;
                bool bloomed = false, faded = false, pulse = false, showClock = false;
                string bottom = null;
                switch (o.Kind)
                {
                    case SeedOutcomeKind.Planted: power = o.Power; bottom = Remaining(o.BloomAtMs); showClock = true; break;
                    case SeedOutcomeKind.Strengthened: power = o.Power; pulse = true; bottom = Remaining(o.BloomAtMs); showClock = true; break;
                    case SeedOutcomeKind.StillGrowing: bottom = Remaining(o.BloomAtMs); showClock = true; break;
                    case SeedOutcomeKind.FlowerWaiting: power = o.Power; bloomed = true; pulse = true; break;
                    case SeedOutcomeKind.TooShort: faded = true; bottom = Loc.F("mirror.notebook.group", o.Minimum); break;
                }
                var art = RenderShared.Node("Art", t, P(0, bottom == null ? 0 : 14));
                if (bloomed) SeedArt.Flower(art, Vector2.zero, 58, z + 1);
                else SeedArt.Sprout(art, Vector2.zero, 56, z + 1, faded ? 0.3f : 1f);
                if (power is int pw) SeedArt.PowerBadge(art, P(30, 24), pw, z + 4);
                if (pulse) SeedArt.Pulse(art);
                if (bottom != null)
                {
                    var l = Text(t, bottom, 15, Fonts.AvenirHeavy, ink.WithAlpha(showClock ? 0.75f : 0.6f), seedW - 40, P(0, -36),
                        showClock ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Center);
                    if (showClock)
                    {
                        float total = 14 + 5 + l.WidthPt() * l.transform.localScale.x;
                        SeedArt.Clock(t, P(-total / 2 + 7, -36), 14, ink.WithAlpha(0.55f), z + 3);
                        l.transform.localPosition = P(-total / 2 + 19, -36);
                    }
                }
            }
        }

        /// `CognitiveMirror.formatDecimal` : une décimale au plus, séparateur de la langue.
        static string Dec(double v)
        {
            CultureInfo ci;
            try { ci = CultureInfo.GetCultureInfo(Loc.Language ?? "en"); } catch { ci = CultureInfo.InvariantCulture; }
            return v.ToString("0.#", ci);
        }

        /// `SeedGarden.remainingText` (DateComponentsFormatter abrégé) : « 3h 20min », ou « 45min » sous l'heure (au moins 1 min).
        string Remaining(long bloomAtMs)
        {
            double seconds = System.Math.Max(60, (bloomAtMs - _s.Clock.Now.ToUnixTimeMilliseconds()) / 1000.0);
            int totalMin = (int)System.Math.Floor(seconds / 60);
            if (seconds < 3600) return totalMin + "min";
            int hours = totalMin / 60, minutes = totalMin % 60;
            return minutes == 0 ? hours + "h" : hours + "h " + minutes + "min";
        }

        // MARK: - Offre « sans pub » après une interstitielle

        bool PresentNoAdsOffer(bool resumesReplay)
        {
            var policy = _s.Interstitials.Policy;
            string price = _s.AdFreeStore.DisplayPrice;
            if (_gameOverPanel == null || _noAdsOffer != null || !policy.CanOfferAdFree(_s.AdFree.IsPurchased, price != null)) return false;
            int adsSeen = policy.ShownSinceNoAdsOffer;
            int adsToday = Mathf.Max(policy.ShownToday, 1);
            policy.MarkAdFreeOfferShown();
            _noAdsOfferResumesReplay = resumesReplay;
            _noAdsOfferShownAt = FxTime.RealNow;
            _s.Analytics.NoAdsOfferView("game_over", adsSeen, adsToday);

            var accent = Look.Accent;
            var ink = accent.ReadableInk();
            var size = NoAdsCardSize;
            int z = PanelZ + 10;
            var offer = RenderShared.Node("noAdsOffer", _gameOverPanel, P(0, -394));
            FxAlpha.Of(offer.gameObject).Alpha = 0;
            ShapeView.Rect("Shadow", offer, P(2, -5), size, 26, Colors.White(0, 0.14f), default, 0, z - 3);
            Relief.Raise(ShapeView.Rect("Bg", offer, Vector2.zero, size, 26, accent, Look.Logo.WithAlpha(0.25f), 1.5f, z), 6);

            // Pictogramme « écran publicitaire barré ».
            var picto = RenderShared.Node("Picto", offer, P(-size.x / 2 + 44, 0));
            ShapeView.Rect("Screen", picto, Vector2.zero, new Vector2(34, 24), 5, Color.clear, ink.WithAlpha(0.85f), 2.5f, z + 2);
            StrokeView.Create("Slash", picto, new[] { P(-19, -14), P(19, 14) }, ink.WithAlpha(0.85f), 3, z + 3);

            var priceLabel = Label(offer, price, Fonts.AvenirBold, 18, Look.Logo, Vector2.zero, z + 5);
            float pillW = Mathf.Max(104, priceLabel.WidthPt() + 30);
            var pillPos = P(size.x / 2 - 16 - pillW / 2, 0);
            Relief.Raise(ShapeView.Rect("Pill", offer, pillPos, new Vector2(pillW, 42), 21, Look.Background, default, 0, z + 2), 3);
            priceLabel.transform.localPosition = pillPos;

            float titleX = -size.x / 2 + 76;
            float titleMaxW = size.x / 2 - 16 - pillW - 12 - titleX;
            Label(offer, Loc.F("offer.noads_title", StoreCatalog.AdFreeBonusCoins), Fonts.AvenirBold, 22, ink, P(titleX, 11), z + 5, HorizontalAlignmentOptions.Left).ShrinkToFit(titleMaxW);
            Label(offer, Loc.F("offer.noads_ads_today", adsToday), Fonts.AvenirDemiBold, 14, ink.WithAlpha(0.78f), P(titleX, -16), z + 5, HorizontalAlignmentOptions.Left).ShrinkToFit(titleMaxW);

            var r = NoAdsDeclineRect;
            Relief.Raise(ShapeView.Rect("DeclineBg", offer, P(r.center.x, r.center.y), r.size, r.height / 2, HomeBeige, BtnStroke, 1, z + 2), 4);
            Label(offer, Loc.T("offer.noads_decline"), Fonts.AvenirMedium, 19, Colors.White(0.32f, 1), P(r.center.x, r.center.y), z + 5).ShrinkToFit(r.width - 16);

            _noAdsOffer = offer;
            Tw.Run(offer.gameObject, Tw.FadeIn(offer.gameObject, 0.25f), "appear");
            return true;
        }

        bool HandleNoAdsOfferTouch(Vector2 point)
        {
            if (_noAdsOffer == null) return false;
            var local = point - WorldPt(_noAdsOffer);
            var card = new Rect(-NoAdsCardSize.x / 2, -NoAdsCardSize.y / 2, NoAdsCardSize.x, NoAdsCardSize.y);
            if (card.Contains(local))
            {
                // Appui trop tôt : consommé, sans ouvrir l'achat.
                if (FxTime.RealNow - _noAdsOfferShownAt >= NoAdsOfferTapGuard) PurchaseNoAdsFromOffer();
                return true;
            }
            if (NoAdsDeclineRect.Contains(local))
            {
                HapticManager.Light();
                _s.Analytics.NoAdsOfferDeclined("game_over");
                DismissNoAdsOffer();
                if (_noAdsOfferResumesReplay) ResetGame();
                return true;
            }
            return false;
        }

        void DismissNoAdsOffer()
        {
            if (_noAdsOffer == null) return;
            var offer = _noAdsOffer;
            _noAdsOffer = null;
            Tw.Run(offer.gameObject, Tw.Seq(Tw.FadeOut(offer.gameObject, 0.18f), Tw.Remove(offer.gameObject)), "appear");
        }

        void PurchaseNoAdsFromOffer()
        {
            if (_isPurchasingNoAds) return;
            _isPurchasingNoAds = true;
            HapticManager.Light();
            _s.AdFreeStore.PurchaseAdFree("game_over", (outcome, message) =>
            {
                _isPurchasingNoAds = false;
                if (_disposed) return;
                switch (outcome)
                {
                    case PurchaseOutcome.Success:
                        HapticManager.Medium();
                        DismissNoAdsOffer();
                        ShowPurchaseToast(Loc.F("shop.purchase_success_noads", StoreCatalog.AdFreeBonusCoins));
                        break;
                    case PurchaseOutcome.Pending:
                        DismissNoAdsOffer();
                        if (message != null) ShowPurchaseToast(message);
                        break;
                    default:
                        if (message != null) ShowPurchaseToast(message);
                        break;
                }
            });
        }

        void ShowPurchaseToast(string text)
        {
            Kill(FindNamed(Root, "purchaseToast"));
            var toast = RenderShared.Node("purchaseToast", Root, P(0, 340));
            FxAlpha.Of(toast.gameObject).Alpha = 0;
            var label = Label(toast, text, Fonts.AvenirDemiBold, 19, Color.white, Vector2.zero, 602).Wrap(440);
            ShapeView.Rect("Bg", toast, Vector2.zero, new Vector2(Mathf.Min(440, label.WidthPt()) + 48, label.HeightPt() + 30), 20, Colors.White(0.14f, 0.92f), default, 0, 600);
            Tw.Run(toast.gameObject, Tw.Seq(Tw.FadeIn(toast.gameObject, 0.2f), Tw.Wait(2.6f), Tw.FadeOut(toast.gameObject, 0.4f), Tw.Remove(toast.gameObject)));
        }
    }
}
