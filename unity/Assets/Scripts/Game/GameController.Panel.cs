using System.Collections.Generic;
using System.Globalization;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Services;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Fiche de fin de partie, offre « sans pub »
    //
    // Refonte 4.0 (`design/refonte/03_fin_de_partie`) : une fiche crème monte
    // du bas sur la grille voilée. Le score d'abord, en très grand (pastille
    // or « Nouveau record » le cas échéant), puis les gains (pièces, XP et
    // sa barre), les faits marquants en liste sobre (chaîne max, miroir,
    // graine), et une seule action principale : la seconde chance tant
    // qu'elle est proposée, sinon Rejouer (ou l'action du mode), Accueil en lien.
    public sealed partial class GameController
    {
        const int OverlayZ = 380;
        const int PanelZ = 400;

        static readonly Color RecordFill = Design.Hex(0xFBEBC4);
        static readonly Color RecordInk = Design.Hex(0x7A5A12);

        Transform _noAdsOffer;
        double _noAdsOfferShownAt = double.NegativeInfinity;
        const double NoAdsOfferTapGuard = 0.7;
        Vector2 _noAdsCardSize = new(500, 100);
        Rect _noAdsDeclineRect = new(-110, -102, 220, 50);
        Rect NoAdsDeclineRect => _noAdsDeclineRect;

        /// Haut de la fiche (unités de scène) : l'offre « sans pub » se pose au-dessus.
        float _sheetTopY;
        /// Emplacement de l'action principale (bouton de partage du duel, posé plus tard).
        Vector2 _primarySlot;
        float _primaryWidth;

        static float D(float pt) => Design.Pt(pt);

        string EncouragementText(bool isNewRecord, bool nearMiss)
        {
            if (_isWinState) return Loc.T("game_over.encourage_perfect");
            if (isNewRecord) return Loc.T("game_over.encourage_record");
            if (nearMiss) return Loc.T("game_over.encourage_close");
            int seed = _lastResult?.TotalChainsMade ?? _s.Stats.TotalChainsMade;
            return Loc.T(seed % 2 == 0 ? "game_over.encourage_progress" : "game_over.encourage_again");
        }

        /// Une ligne des faits marquants.
        sealed class FactRow
        {
            public VectorIconName Icon;
            public Color Tint;
            public float TintAlpha = 0.45f;
            public Color IconColor = Design.InkHex;
            public string Title;
            public string Subtitle;
            public string Value;
            public IReadOnlyList<int> Minis;
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
            var g = _geometry;

            // Voile crème sur la grille.
            _gameOverOverlay = RenderShared.Node("GameOverOverlay", Root);
            ShapeView.Rect("Dim", _gameOverOverlay, Vector2.zero, SceneSize * 2, 0, Design.Bg.WithAlpha(0.72f), default, 0, OverlayZ);
            FxAlpha.Of(_gameOverOverlay.gameObject).Alpha = 0;
            Tw.Run(_gameOverOverlay.gameObject, Tw.Fade(_gameOverOverlay.gameObject, 1, 0.3f));

            var panel = RenderShared.Node("GameOverPanel", Root);
            _gameOverPanel = panel;
            var content = RenderShared.Node("Content", panel);
            float sheetW = g.VisibleWidth + 4;
            float cw = Mathf.Min(g.VisibleWidth, D(480)) - 2 * D(20);
            float left = -cw / 2, right = cw / 2;
            int z = PanelZ + 2;
            float cursor = -D(12);

            // Poignée.
            ShapeView.Rect("Grab", content, P(0, cursor - D(2.5f)), new Vector2(D(40), D(5)), D(2.5f), Design.Line, default, 0, z);
            cursor -= D(5 + 18);

            bool isNewRecord = result?.IsNewRecord ?? false;
            int? nearMiss = result?.NearMissGap;
            bool puzzleStars = Kind == GameModeKind.Puzzle || _demoPuzzle != null;
            bool showRecord = isNewRecord && !puzzleStars && Kind != GameModeKind.Duel;

            // En-tête : pastille « Nouveau record », ou le titre du mode en surtitre.
            if (showRecord)
            {
                var pill = RenderShared.Node("Record", content, P(0, cursor - D(15)));
                var text = Design.Text(pill, Loc.T("ui.end.new_record"), NunitoWeight.Black, 14, RecordInk, Vector2.zero, z + 3,
                    HorizontalAlignmentOptions.Left);
                float w = D(16 + 6) + text.WidthPt();
                ShapeView.Rect("Bg", pill, Vector2.zero, new Vector2(w + D(24), D(30)), D(15), RecordFill, default, 0, z + 1);
                VectorIcon.Create(pill, VectorIconName.LineTrophy, P(-w / 2 + D(8), 0), D(16), RecordInk, z + 2);
                text.transform.localPosition = P(-w / 2 + D(22), 0);
                pill.localScale = Vector3.one * 0.6f;
                FxAlpha.Of(pill.gameObject).Alpha = 0;
                Tw.Run(pill.gameObject, Tw.Seq(Tw.Wait(0.45f), Tw.Par(Tw.FadeIn(pill.gameObject, 0.2f), Tw.Scale(pill, 1f, 0.28f))));
                PopEffects.MakeBurst(content, P(0, cursor - D(15)), Design.Gold, PopTier.Medium);
                cursor -= D(30 + 6);
            }
            else
            {
                string kicker = Kind switch
                {
                    GameModeKind.Duel => Loc.T("duel.finished_title"),
                    GameModeKind.Puzzle => Loc.T(_isWinState ? "puzzle.solved_title" : "puzzle.stuck_title"),
                    GameModeKind.Rush => Loc.T("rush.time_up_title"),
                    _ => _isWinState ? Loc.T("game_over.perfect_title") : Loc.T("puzzle.stuck_title"),
                };
                Design.Text(content, kicker, NunitoWeight.ExtraBold, 15, Design.Ink2, new Vector2(0, cursor - D(10)), z + 3).ShrinkToFit(cw);
                cursor -= D(22);
            }

            var scoreDisplay = Design.Text(content, "0", NunitoWeight.Black, 64, Design.Ink, new Vector2(0, cursor - D(34)), z + 3);
            scoreDisplay.Tmp.characterSpacing = -3f;
            AnimateScoreCountUp(scoreDisplay, _score, cw);
            cursor -= D(70);

            // Sous le score : étoiles du puzzle, statut du duel, ancien record, écart ou meilleur score.
            if (puzzleStars)
            {
                int stars = result?.PuzzleStars ?? 0;
                for (int i = 0; i < 3; i++)
                    Glyphs.Star(content, P((i - 1) * D(30), cursor - D(14)), D(26), i < stars ? Design.Gold : Design.Line, true, z + 3);
                cursor -= D(30);
            }
            else
            {
                string sub = null;
                if (Kind == GameModeKind.Duel) sub = Loc.T("duel.publishing");
                else if (isNewRecord)
                {
                    var board = Kind == GameModeKind.Rush ? _s.Games.RushHighScores : _s.Games.HighScores;
                    sub = board.Count > 1 && board[1] < _score ? Loc.F("ui.end.previous_record", Design.Number(board[1]))
                        : EncouragementText(true, false);
                }
                else if (nearMiss is int gap) sub = Loc.F("game_over.near_miss", gap);
                else if (BestFor(Kind) is int best) sub = Loc.F("ui.end.best", Design.Number(best));
                else sub = EncouragementText(false, false);
                var subLabel = Design.Text(content, sub, NunitoWeight.Bold, 14, nearMiss != null ? Design.Flame : Design.Ink2,
                    new Vector2(0, cursor - D(10)), z + 3);
                subLabel.ShrinkToFit(cw);
                if (Kind == GameModeKind.Duel) _duelStatusLabel = subLabel;
                cursor -= D(20);
            }

            // Gains : pièces, XP (niveau et barre).
            int coins = result?.CoinsEarned ?? 0;
            int xp = result?.Xp?.XpGained ?? 0;
            if (coins > 0 || xp > 0)
            {
                cursor -= D(18);
                float gainH = D(64);
                int n = (coins > 0 ? 1 : 0) + (xp > 0 ? 1 : 0);
                float gw = (cw - D(10) * (n - 1)) / n;
                float gx = left + gw / 2;
                float gy = cursor - gainH / 2;
                if (coins > 0)
                {
                    var card = RenderShared.Node("Coins", content, P(gx, gy));
                    Design.Surface("Bg", card, Vector2.zero, new Vector2(gw, gainH), D(Design.CardRadius), z);
                    float x = -gw / 2 + D(14);
                    Design.Coin(card, new Vector2(x + D(14), 0), 28, z + 2);
                    Design.Text(card, "+" + coins, NunitoWeight.Black, 18, Design.Ink, new Vector2(x + D(38), D(8)), z + 3, HorizontalAlignmentOptions.Left);
                    Design.Text(card, Loc.T("ui.end.coins"), NunitoWeight.Bold, 12, Design.Ink2, new Vector2(x + D(38), -D(11)), z + 3,
                        HorizontalAlignmentOptions.Left).ShrinkToFit(gw - D(66));
                    gx += gw + D(10);
                }
                if (xp > 0)
                {
                    var card = RenderShared.Node("Xp", content, P(gx, gy));
                    Design.Surface("Bg", card, Vector2.zero, new Vector2(gw, gainH), D(Design.CardRadius), z);
                    var level = _s.Level.Breakdown;
                    float x = -gw / 2 + D(14);
                    var tint = Look.BubbleColor(8);
                    var disc = ShapeView.Circle("Level", card, P(x + D(14), 0), D(14), Design.Tint(tint, 0.5f), default, 0, z + 1);
                    Design.Text(card, level.Level.ToString(), NunitoWeight.Black, 11, Design.Ink, new Vector2(x + D(14), 0), z + 3);
                    Design.Text(card, Loc.F("game_over.xp_gained", xp), NunitoWeight.Black, 18, Design.Ink, new Vector2(x + D(38), D(9)), z + 3,
                        HorizontalAlignmentOptions.Left).ShrinkToFit(gw - D(66));
                    float ratio = level.XpForNext > 0 ? level.XpIntoLevel / (float)level.XpForNext : 1f;
                    Design.Bar(card, new Vector2(x + D(38), -D(11)), Mathf.Min(100, (gw - D(66)) / Design.Scale), 6, ratio, tint, z + 2);
                    if (result.Xp is GainResult gr && gr.LeveledUp)
                    {
                        PopEffects.MakeBurst(card, P(x + D(14), 0), tint, PopTier.Small);
                        Tw.Run(disc.gameObject, Tw.Seq(Tw.Wait(0.5f), Tw.Scale(disc.transform, 1.25f, 0.15f), Tw.Scale(disc.transform, 1f, 0.2f)));
                    }
                }
                cursor -= gainH;
            }

            // Faits marquants.
            var rows = new List<FactRow>
            {
                new()
                {
                    Icon = VectorIconName.LineBolt, Tint = Look.BubbleColor(3), Title = Loc.T("ui.end.longest_chain"),
                    Subtitle = Loc.F("game_over.combos_created", _combosCreated), Value = Loc.F("mirror.notebook.group", _longestChain),
                },
            };
            if (MirrorRow() is FactRow mr) rows.Add(mr);
            if (SeedRow() is FactRow sr) rows.Add(sr);
            cursor -= D(10);
            float rowH = D(60);
            var list = RenderShared.Node("Facts", content, P(0, cursor - rows.Count * rowH / 2));
            Design.Surface("Bg", list, Vector2.zero, new Vector2(cw, rows.Count * rowH), D(Design.CardRadius), z);
            for (int i = 0; i < rows.Count; i++)
                AddFactRow(list, rows[i], rows.Count * rowH / 2 - rowH * (i + 0.5f), cw, rowH, i > 0, z);
            cursor -= rows.Count * rowH;

            // Actions.
            cursor -= D(16);
            PuzzleLevel nextPuzzle = null;
            if (Kind == GameModeKind.Puzzle && _isWinState && _demoPuzzle == null && _puzzleLevel != null)
                nextPuzzle = _s.Catalog.Level(_puzzleLevel.World, _puzzleLevel.Index + 1);
            bool offerContinue = Kind == GameModeKind.Normal && _secondChanceGrid != null;
            float primaryH = offerContinue ? 62 : 56;
            _primarySlot = new Vector2(0, cursor - D(primaryH / 2));
            _primaryWidth = cw;
            if (offerContinue)
                PanelButton(content, "continueBtn", _primarySlot, cw, Loc.T("ui.end.second_chance"), true, VectorIconName.LineVideo,
                    primaryH, Loc.T("game_over.continue_hint"));
            else if (Kind == GameModeKind.Normal || Kind == GameModeKind.Rush || Kind == GameModeKind.Puzzle || _demoPuzzle != null)
            {
                if (nextPuzzle != null)
                    PanelButton(content, "nextPuzzleBtn", _primarySlot, cw, Loc.F("puzzle.level_label", nextPuzzle.Index), true, VectorIconName.LinePlay);
                else
                    PanelButton(content, "replayBtn", _primarySlot, cw, Loc.T(nearMiss != null ? "game_over.beat_record" : "game_over.replay"),
                        true, VectorIconName.LineRestart);
            }
            else if (Kind == GameModeKind.Duel)
            {
                // Le bouton de partage n'apparaît qu'une fois le code connu (UpdateDuelStatus).
            }
            else
                PanelButton(content, "dailyLeaderboardBtn", _primarySlot, cw, Loc.T("game_over.daily_leaderboard"), true, VectorIconName.LineTrophy);
            cursor -= D(primaryH + 10);

            // Ligne du bas : Accueil (lien discret, ou bouton à côté de la relance contextuelle).
            (string name, string title, VectorIconName icon)? nextUp = null;
            if (offerContinue) nextUp = ("replayBtn", Loc.T("game_over.replay"), VectorIconName.LineRestart);
            else if (result != null && Kind != GameModeKind.Demo)
            {
                if (result.Suggestion == NextSuggestion.NextDaily) nextUp = ("nextDailyBtn", Loc.T("menu.daily"), VectorIconName.LineCalendar);
                else if (result.Suggestion == NextSuggestion.NextRush) nextUp = ("nextRushBtn", Loc.T("menu.rush"), VectorIconName.LineBolt);
            }
            if (nextUp is { } nu)
            {
                float half = (cw - D(10)) / 2;
                PanelButton(content, "homePanelBtn", new Vector2(left + half / 2, cursor - D(28)), half, Loc.T("game_over.home"), false, null);
                PanelButton(content, nu.name, new Vector2(right - half / 2, cursor - D(28)), half, nu.title, false, nu.icon);
                cursor -= D(56);
            }
            else
            {
                var home = RenderShared.Node("homePanelBtn", content, P(0, cursor - D(16)));
                FxAlpha.Of(home.gameObject);
                Design.Text(home, Loc.T("game_over.home"), NunitoWeight.ExtraBold, 15, Design.Ink2, Vector2.zero, z + 3).ShrinkToFit(cw);
                AddZone("homePanelBtn", home, new Vector2(D(200), D(40)));
                cursor -= D(32);
            }
            cursor -= Mathf.Max(D(20), g.BottomInset + D(4));

            // La fiche est ancrée en bas de l'écran ; trop haute, elle se tasse.
            float height = -cursor;
            float maxH = g.VisibleHeight - g.TopInset - D(8);
            float k = Mathf.Min(1f, maxH / height);
            content.localScale = Vector3.one * k;
            _sheetTopY = g.VisibleBottom + height * k;
            content.localPosition = P(0, _sheetTopY);
            float extra = D(60);
            ShapeView.Rect("SheetHair", panel, P(0, _sheetTopY - height * k / 2 - extra / 2 + D(1)), new Vector2(sheetW, height * k + extra),
                D(30), Design.Hair, default, 0, PanelZ - 1);
            ShapeView.Rect("Sheet", panel, P(0, _sheetTopY - height * k / 2 - extra / 2), new Vector2(sheetW, height * k + extra), D(30),
                Design.Bg, default, 0, PanelZ);

            // Montée depuis le bas.
            panel.localPosition = P(0, -height * k - D(20));
            Tw.Run(panel.gameObject, Tw.Seq(Tw.MoveTo(panel, Vector2.zero, 0.32f)), "appear");

            // Le réseau a pu répondre avant l'ouverture du panneau.
            if (Kind == GameModeKind.Duel && _pendingDuel != null) { var d = _pendingDuel; _pendingDuel = null; UpdateDuelStatus(d); }

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

        /// Bouton de la fiche (principal ou secondaire), zone tactile nommée.
        Transform PanelButton(Transform parent, string name, Vector2 pos, float width, string label, bool primary, VectorIconName? icon,
            float heightPt = 56, string subtitle = null)
        {
            var node = Design.Button(name, parent, pos, width, label, primary, PanelZ + 2, icon, heightPt, subtitle);
            AddZone(name, node, new Vector2(width, D(heightPt)));
            return node;
        }

        void AddFactRow(Transform list, FactRow row, float y, float cw, float rowH, bool separator, int z)
        {
            var node = RenderShared.Node("Fact", list, P(0, y));
            if (separator) ShapeView.Rect("Sep", node, P(0, rowH / 2), new Vector2(cw, D(1)), 0, Design.Line, default, 0, z + 1);
            float x = -cw / 2 + D(14);
            Design.IconChip(node, new Vector2(x + D(18), 0), 36, 12, row.Tint, row.Icon, z + 1, row.IconColor, 20, row.TintAlpha);
            float textX = x + D(36 + 12);
            float valueW = 0;
            float rightX = cw / 2 - D(14);
            if (row.Minis != null && row.Minis.Count > 0)
            {
                int n = row.Minis.Count;
                float d = Mathf.Min(18, (130f + 3) / n - 3);
                valueW = D(n * (d + 3) - 3);
                float mx = rightX - valueW + D(d / 2);
                foreach (var v in row.Minis) { Design.MiniBubble(node, new Vector2(mx, 0), d, v, z + 2); mx += D(d + 3); }
            }
            else if (!string.IsNullOrEmpty(row.Value))
            {
                var val = Design.Text(node, row.Value, NunitoWeight.Black, 14, Design.Ink, new Vector2(rightX, 0), z + 3, HorizontalAlignmentOptions.Right);
                valueW = val.WidthPt();
            }
            float textW = rightX - (valueW > 0 ? valueW + D(10) : 0) - textX;
            if (string.IsNullOrEmpty(row.Subtitle))
            {
                var t = Design.Text(node, row.Title, NunitoWeight.ExtraBold, 14, Design.Ink, new Vector2(textX, 0), z + 3, HorizontalAlignmentOptions.Left);
                if (t.WidthPt() > textW) { t.SizePt = D(13); t.Wrap(textW); }
                return;
            }
            Design.Text(node, row.Title, NunitoWeight.ExtraBold, 14, Design.Ink, new Vector2(textX, D(9)), z + 3, HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            Design.Text(node, row.Subtitle, NunitoWeight.Bold, 12, Design.Ink2, new Vector2(textX, -D(10)), z + 3, HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
        }

        /// Ce que le miroir a remarqué pendant la partie (`null` : rien à dire).
        FactRow MirrorRow()
        {
            if (_lastResult?.MirrorInsight is not MirrorInsight ins) return null;
            var row = new FactRow { Icon = VectorIconName.LineBrain, Tint = Look.BubbleColor(8) };
            string notebook = Loc.F("ui.end.notebook", _s.Mirror.RepertoireCount, Partition.Total);
            switch (ins.Kind)
            {
                case MirrorInsightKind.Discovered:
                    row.Title = Loc.T("ui.end.new_ten");
                    row.Subtitle = Loc.F("ui.end.notebook", ins.Total, Partition.Total);
                    var vals = new List<int>();
                    foreach (var part in ins.Key.Split('+')) if (int.TryParse(part, out int pv)) vals.Add(pv);
                    row.Minis = vals;
                    break;
                case MirrorInsightKind.NextTarget:
                    row.Title = Loc.F("mirror.end.to_find", ins.Length);
                    row.Subtitle = notebook;
                    row.Minis = new int[ins.Length];
                    break;
                // « ↑ » d'iOS n'existe pas dans Nunito : la tendance est dite sans flèche.
                case MirrorInsightKind.Faster:
                    row.Title = Loc.T("mirror.skill.look");
                    row.Subtitle = Loc.F("mirror.trend.faster", Dec(ins.Gain));
                    row.Value = Loc.F("mirror.unit.seconds", Dec(ins.Seconds));
                    break;
                case MirrorInsightKind.Longer:
                    row.Title = Loc.T("mirror.skill.vision");
                    row.Subtitle = Loc.F("mirror.trend.more", Dec(ins.Average - ins.Usual));
                    row.Value = Dec(ins.Average);
                    break;
                case MirrorInsightKind.Anticipation:
                    row.Title = Loc.T("mirror.skill.anticipation");
                    row.Subtitle = Loc.F("mirror.trend.points_up", ins.Percent - ins.UsualPercent);
                    row.Value = ins.Percent + " %";
                    break;
                default: return null;
            }
            return row;
        }

        /// Sort de la graine de dix (`null` : aucune).
        FactRow SeedRow()
        {
            if (_lastResult?.SeedOutcome is not SeedOutcome o) return null;
            var row = new FactRow { Icon = VectorIconName.LineSeed, Tint = Look.BubbleColor(4), TintAlpha = 0.5f, IconColor = Design.SeedGreen };
            switch (o.Kind)
            {
                case SeedOutcomeKind.Planted:
                    row.Title = Loc.T("ui.end.seed_planted");
                    row.Subtitle = Loc.F("ui.end.blooms_in", Remaining(o.BloomAtMs));
                    row.Value = "×" + o.Power;
                    break;
                case SeedOutcomeKind.Strengthened:
                    row.Title = Loc.T("ui.end.seed_stronger");
                    row.Subtitle = Loc.F("ui.end.blooms_in", Remaining(o.BloomAtMs));
                    row.Value = "×" + o.Power;
                    break;
                case SeedOutcomeKind.StillGrowing:
                    row.Title = Loc.T("ui.end.seed_growing");
                    row.Subtitle = Loc.F("ui.end.blooms_in", Remaining(o.BloomAtMs));
                    if (_lastResult.SeedPower > 0) row.Value = "×" + _lastResult.SeedPower;
                    break;
                case SeedOutcomeKind.FlowerWaiting:
                    row.Title = Loc.F("game.flower_ready", o.Power);
                    row.Value = "×" + o.Power;
                    break;
                default:
                    row.Title = Loc.F("seed.too_short", o.Minimum);
                    row.IconColor = Design.Ink3;
                    break;
            }
            return row;
        }

        void AnimateScoreCountUp(TenGoText label, int target, float maxW)
        {
            const int steps = 24;
            const float duration = 0.55f;
            var seq = new List<Tween>();
            for (int i = 1; i <= steps; i++)
            {
                int v = (int)((double)target * i / steps);
                seq.Add(Tw.Call(() => { if (label != null) label.Text = Design.Number(v); }));
                seq.Add(Tw.Wait(duration / steps));
            }
            seq.Add(Tw.Call(() => { if (label != null) { label.Text = Design.Number(target); label.ShrinkToFit(maxW); } }));
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

            // Carte blanche posée au-dessus de la fiche, « Non merci » en lien dessous.
            float w = Mathf.Min(_geometry.VisibleWidth, D(480)) - 2 * D(20), h = D(72);
            var size = new Vector2(w, h);
            _noAdsCardSize = size;
            _noAdsDeclineRect = new Rect(-D(80), -h / 2 - D(40), D(160), D(36));
            int z = PanelZ + 10;
            var offer = RenderShared.Node("noAdsOffer", _gameOverPanel, P(0, _sheetTopY + D(14) + D(40) + h / 2));
            FxAlpha.Of(offer.gameObject).Alpha = 0;
            Design.Surface("Bg", offer, Vector2.zero, size, D(Design.CardRadius), z);
            float x = -w / 2 + D(14);
            Design.IconChip(offer, new Vector2(x + D(22), 0), 44, 14, Design.Sunk, VectorIconName.LineNoads, z + 1, Design.Ink, 24, 1f);

            var priceLabel = Design.Text(offer, price, NunitoWeight.Black, 16, Design.OnAccent, Vector2.zero, z + 5);
            float pillW = Mathf.Max(D(84), priceLabel.WidthPt() + D(28));
            var pillPos = P(w / 2 - D(14) - pillW / 2, 0);
            ShapeView.Rect("Pill", offer, pillPos, new Vector2(pillW, D(40)), D(20), Design.Accent, default, 0, z + 2);
            priceLabel.transform.localPosition = pillPos;

            float titleX = x + D(44 + 12);
            float titleMaxW = w / 2 - D(14) - pillW - D(10) - titleX;
            Design.Text(offer, Loc.F("offer.noads_title", StoreCatalog.AdFreeBonusCoins), NunitoWeight.Black, 16, Design.Ink,
                new Vector2(titleX, D(9)), z + 5, HorizontalAlignmentOptions.Left).ShrinkToFit(titleMaxW);
            Design.Text(offer, Loc.F("offer.noads_ads_today", adsToday), NunitoWeight.Bold, 12, Design.Ink2,
                new Vector2(titleX, -D(10)), z + 5, HorizontalAlignmentOptions.Left).ShrinkToFit(titleMaxW);

            var r = _noAdsDeclineRect;
            Design.Text(offer, Loc.T("offer.noads_decline"), NunitoWeight.ExtraBold, 15, Design.Ink2, new Vector2(r.center.x, r.center.y), z + 5)
                .ShrinkToFit(r.width);

            _noAdsOffer = offer;
            Tw.Run(offer.gameObject, Tw.FadeIn(offer.gameObject, 0.25f), "appear");
            return true;
        }

        bool HandleNoAdsOfferTouch(Vector2 point)
        {
            if (_noAdsOffer == null) return false;
            var local = point - WorldPt(_noAdsOffer);
            var card = new Rect(-_noAdsCardSize.x / 2, -_noAdsCardSize.y / 2, _noAdsCardSize.x, _noAdsCardSize.y);
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
            var toast = RenderShared.Node("purchaseToast", Root, P(0, _geometry.VisibleTop - _geometry.TopInset - D(60)));
            FxAlpha.Of(toast.gameObject).Alpha = 0;
            float maxW = _geometry.VisibleWidth - 2 * D(32);
            var label = Design.Text(toast, text, NunitoWeight.ExtraBold, 15, Design.Bg, Vector2.zero, 602).Wrap(maxW);
            ShapeView.Rect("Bg", toast, Vector2.zero, new Vector2(Mathf.Min(maxW, label.WidthPt()) + D(32), label.HeightPt() + D(22)), D(16), Design.Ink, default, 0, 600);
            Tw.Run(toast.gameObject, Tw.Seq(Tw.FadeIn(toast.gameObject, 0.2f), Tw.Wait(2.6f), Tw.FadeOut(toast.gameObject, 0.4f), Tw.Remove(toast.gameObject)));
        }
    }
}
