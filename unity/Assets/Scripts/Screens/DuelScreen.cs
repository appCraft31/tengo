using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Platform;
using TenGo.Rendering;
using TenGo.Services;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Point d'entrée du Duel (DuelScene.swift, onglet Social) : lancer un
    /// défi, répondre à un code reçu, retrouver ses duels passés.
    ///
    /// Cibles : « startDuel » (alias « newDuel »), « answerDuel » (alias
    /// « joinDuel », ouvre la saisie du code), « duelShare:&lt;CODE&gt; » (duel
    /// lancé et sans adversaire : partage du code).
    ///
    /// La liste relit les duels de l'historique local sur le serveur
    /// (`IDuelBackend.MyDuels`) ; répondre à un code lit le duel (`Fetch`),
    /// dit le score à battre, puis lance la partie sur la graine du serveur.
    public sealed class DuelScreen : ScreenBase
    {
        const int Z = 20;
        float _cardWidth = 340;
        float _listTopY;
        TenGoText _status;
        DuelCodeEntryOverlay _entry;
        bool _disposed;
        bool _joining;
        readonly List<Duel> _duels = new();

        /// Duels affichés dans la liste (après rechargement).
        public IReadOnlyList<Duel> Duels => _duels;

        /// Dernier message d'état affiché (chargement, erreur).
        public string StatusText => _status != null ? _status.Text : "";
        /// Saisie du code ouverte, s'il y en a une.
        public DuelCodeEntryOverlay CodeEntry => _entry != null && _entry.IsOpen ? _entry : null;

        public DuelScreen(AppRoot app, string incomingCode) : base(app, "DuelScreen")
        {
            TapOnRelease = true;
            SetupUI();
            ReloadDuels();
            // Code reçu par lien : on y répond une fois l'écran affiché (le
            // routeur est encore en train de présenter celui-ci).
            _pendingIncoming = incomingCode;
        }

        string _pendingIncoming;

        public override void Tick(float dt)
        {
            if (!string.IsNullOrEmpty(_pendingIncoming))
            {
                var code = _pendingIncoming;
                _pendingIncoming = null;
                JoinWithCode(code);
                return;
            }
            base.Tick(dt);
        }

        static Vector2 P(float x, float y) => Units.P(x, y);

        // MARK: - UI

        void SetupUI()
        {
            float usableW = UsableW;
            float halfH = HalfH;
            _cardWidth = Mathf.Min(usableW - 48, 600);

            float titleY = halfH - TopInset - 36;
            TenGoText.Create(Root, Loc.T("duel.title"), Fonts.AvenirHeavy, 36, Look.Logo, P(0, titleY), Z + 2).ShrinkToFit(_cardWidth);

            const float pitchH = 268;
            float pitchY = titleY - 48 - pitchH / 2;
            AddPitchCard(pitchY, pitchH);

            const float codeH = 92;
            float codeY = pitchY - pitchH / 2 - 22 - codeH / 2;
            AddCodeCard(codeY, codeH);

            _status = TenGoText.Create(Root, "", Fonts.AvenirMedium, 15, Look.Logo.WithAlpha(0.75f), P(0, codeY - codeH / 2 - 34), Z + 2);
            _status.Wrap(_cardWidth);
            _listTopY = codeY - codeH / 2 - 26;

            // Cet écran EST l'onglet Social : pas de retour, des onglets. Comme
            // DuelScene, la barre suit la marge basse brute (sans plancher de 20 pt).
            SelectedTab = Tab.Social;
            TabBarNode = TabBar.Make(Root, usableW, Tab.Social, -halfH + Geo.BottomInset + TabBar.Height / 2);
        }

        /// Carte d'appel : « moi contre ? », la règle en une phrase, l'action principale.
        void AddPitchCard(float y, float height)
        {
            var tint = Look.BubbleColor(1);
            var ink = tint.ReadableInk();
            float half = height / 2;
            var card = RenderShared.Node("PitchCard", Root, P(0, y));
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(_cardWidth, height), 30, tint, default, 0, Z);
            Relief.Raise(bg, 6);

            float avatarY = half - 68;
            // Initiale du pseudo Game Center / Play Games, sinon une silhouette.
            string me = S.Play.PlayerName?.Trim();
            AddAvatar(card, string.IsNullOrEmpty(me) ? null : me.Substring(0, 1).ToUpperInvariant(), 42, Look.BubbleColor(5), new Vector2(-96, avatarY), true);
            AddAvatar(card, "?", 42, Color.white, new Vector2(96, avatarY), false);

            ShapeView.Rect("Versus", card, P(0, avatarY), new Vector2(62, 36), 18, Color.white, default, 0, Z + 2);
            TenGoText.Create(card, "VS", Fonts.AvenirHeavy, 17, Colors.White(0.24f), P(0, avatarY), Z + 3);

            TenGoText.Create(card, Loc.T("duel.subtitle"), Fonts.AvenirMedium, 16, ink.WithAlpha(0.9f), P(0, avatarY - 78), Z + 2)
                .Wrap(_cardWidth - 56);

            var button = RenderShared.Node("startDuel", card, P(0, -half + 48));
            var buttonBg = ShapeView.Rect("Bg", button, Vector2.zero, new Vector2(_cardWidth - 48, 56), 28, Color.white, default, 0, Z + 2);
            Relief.Raise(buttonBg, 4, surface: true);
            TenGoText.Create(button, Loc.T("duel.start"), Fonts.AvenirBold, 19, Look.Logo, Vector2.zero, Z + 4).ShrinkToFit(_cardWidth - 80);
            AddTarget("startDuel", button, new Vector2(_cardWidth - 48, 56), StartDuel);
            AddTarget("newDuel", button, new Vector2(_cardWidth - 48, 56), StartDuel);
        }

        /// Pastille ronde portant une initiale (ou « ? »), silhouette sans nom.
        void AddAvatar(Transform parent, string initial, float radius, Color tint, Vector2 pos, bool raised)
        {
            var fill = raised ? tint : tint.WithAlpha(0.55f);
            var circle = ShapeView.Circle("Avatar", parent, P(pos.x, pos.y), radius, fill, default, 0, Z + 2);
            if (raised) Relief.Raise(circle, 4);
            var ink = Colors.White(0.24f);
            if (initial != null)
                TenGoText.Create(parent, initial, Fonts.AvenirHeavy, radius * 0.86f, ink, P(pos.x, pos.y), Z + 3);
            else
                VectorIcon.Create(parent, VectorIconName.IconSocial, P(pos.x, pos.y), radius * 1.1f, ink, Z + 3);
        }

        /// Carte « j'ai reçu un code » : un faux champ et son bouton ; le tap ouvre la saisie.
        void AddCodeCard(float y, float height)
        {
            var card = RenderShared.Node("answerDuel", Root, P(0, y));
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(_cardWidth, height), 26, Relief.Surface, default, 0, Z);
            Relief.Raise(bg, 4);

            var goTint = Look.BubbleColor(4);
            var go = TenGoText.Create(card, Loc.T("duel.go"), Fonts.AvenirBold, 17, goTint.ReadableInk(), Vector2.zero, Z + 3);
            float goW = Mathf.Max(104, go.WidthPt() + 44);
            float goX = _cardWidth / 2 - 20 - goW / 2;
            var goBg = ShapeView.Rect("Go", card, P(goX, 2), new Vector2(goW, 52), 26, goTint, default, 0, Z + 1);
            Relief.Raise(goBg, 4);
            go.transform.localPosition = P(goX, 2);

            float fieldLeft = -_cardWidth / 2 + 20;
            float fieldW = _cardWidth - 40 - goW - 14;
            TenGoText.Create(card, Loc.T("duel.answer"), Fonts.AvenirDemiBold, 14, Look.Logo.WithAlpha(0.8f), P(fieldLeft + 4, 26), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(fieldW);
            ShapeView.Rect("Field", card, P(fieldLeft + fieldW / 2, -12), new Vector2(fieldW, 40), 14,
                Look.Logo.WithAlpha(0.08f).Flattened(Relief.Surface), default, 0, Z + 1);
            TenGoText.Create(card, "A B C 1 2 3", Fonts.AvenirBold, 17, Look.Logo.WithAlpha(0.38f), P(fieldLeft + 16, -12), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            AddTarget("answerDuel", card, new Vector2(_cardWidth, height), PromptForCode);
            AddTarget("joinDuel", card, new Vector2(_cardWidth, height), PromptForCode);
        }

        /// Duels passés, rechargés depuis le serveur à partir de l'historique local.
        void ReloadDuels()
        {
            if (S.DuelHistory.Codes.Count == 0) return;
            SetStatus(Loc.T("duel.loading"));
            S.Duels.MyDuels(list =>
            {
                if (_disposed) return;
                if (!_joining) SetStatus("");
                _duels.Clear();
                _duels.AddRange(list);
                RenderDuelList();
            });
        }

        void RenderDuelList()
        {
            string uid = S.Duels.Uid;
            if (uid == null || _duels.Count == 0) return;
            TenGoText.Create(Root, Loc.T("duel.section_list"), Fonts.AvenirBold, 22, Look.Logo, P(-_cardWidth / 2 + 4, _listTopY - 16), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);

            // Liste défilante entre l'en-tête et les onglets (iOS : les 5 premiers qui tiennent).
            float top = _listTopY - 34;
            float bottom = -HalfH + Geo.BottomInset + TabBar.Height + 10;
            Scroll = new ScrollArea(Root, top, bottom, -UsableW / 2, UsableW / 2);
            const float rowH = 72;
            float cursor = -16;
            var now = System.DateTimeOffset.UtcNow;
            foreach (var duel in _duels)
            {
                AddDuelRow(duel, uid, now, cursor - rowH / 2, rowH);
                cursor -= rowH + 12;
            }
            Scroll.ContentHeight = -cursor + 4;
            Scroll.ApplyClip();
        }

        void AddDuelRow(Duel duel, string uid, System.DateTimeOffset now, float y, float height)
        {
            string code = duel.Code;
            // Duel lancé par ce joueur et toujours sans adversaire : on revient
            // ici quand on a oublié d'envoyer le code sur le moment.
            bool shareable = !duel.IsComplete && !duel.IsExpired(now) && duel.ChallengerUid == uid;
            var row = RenderShared.Node(shareable ? "duelShare:" + code : "duelRow", Scroll.Content, P(0, y));
            var bg = ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(_cardWidth, height), 24, Relief.Surface, default, 0, Z);
            Relief.Raise(bg, 4);

            // Adversaire : son initiale, ou « ? » tant que personne n'a répondu.
            string rival = duel.RivalName(uid).Trim();
            float avatarX = -_cardWidth / 2 + 20 + 24;
            int[] tints = { 6, 2, 8, 5, 7, 3 };
            int sum = 0;
            foreach (char c in code) sum += c;
            string initial = rival.Length > 0 ? rival.Substring(0, 1).ToUpperInvariant() : "?";
            AddAvatar(row, initial, 24, Look.BubbleColor(tints[Mathf.Abs(sum) % tints.Length]), new Vector2(avatarX, 0), false);

            // Statut : pastille colorée à droite.
            string statusKey;
            Color fill = Look.Logo.WithAlpha(0.08f).Flattened(Relief.Surface), ink = Look.Logo.WithAlpha(0.8f);
            if (duel.IsComplete && duel.OutcomeFor(uid) is DuelOutcome outcome)
            {
                switch (outcome)
                {
                    case DuelOutcome.Win:
                        statusKey = "duel.row_win";
                        fill = new Color(0.99f, 0.95f, 0.80f);
                        ink = new Color(0.45f, 0.34f, 0.10f);
                        break;
                    case DuelOutcome.Loss:
                        statusKey = "duel.row_loss";
                        fill = new Color(0.99f, 0.89f, 0.88f);
                        ink = new Color(0.60f, 0.24f, 0.22f);
                        break;
                    default: statusKey = "duel.row_draw"; break;
                }
            }
            else statusKey = duel.IsExpired(now) ? "duel.row_expired" : "duel.row_pending";

            var status = TenGoText.Create(row, Loc.T(statusKey), Fonts.AvenirBold, 14, ink, Vector2.zero, Z + 3);
            float tagW = status.WidthPt() + 28;
            float tagX = _cardWidth / 2 - 18 - tagW / 2 - (shareable ? 40 : 0);
            ShapeView.Rect("Tag", row, P(tagX, 0), new Vector2(tagW, 34), 17, fill, default, 0, Z + 1);
            status.transform.localPosition = P(tagX, 0);
            if (shareable) VectorIcon.Create(row, VectorIconName.IconShare, P(_cardWidth / 2 - 30, 0), 22, Look.Logo.WithAlpha(0.75f), Z + 3);

            // Nom de l'adversaire (ou code du duel), puis les scores.
            float textX = avatarX + 24 + 14;
            float textW = tagX - tagW / 2 - 12 - textX;
            TenGoText.Create(row, rival.Length == 0 ? code : rival, Fonts.AvenirBold, 17, Look.Logo, P(textX, 11), Z + 3,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            int mine = uid == duel.ChallengerUid ? duel.ChallengerScore : (duel.OpponentScore ?? 0);
            int? theirs = uid == duel.ChallengerUid ? duel.OpponentScore : duel.ChallengerScore;
            string points = Loc.T("game.points_label");
            string detail = theirs is int t ? $"{mine} – {t} {points}" : $"{mine} {points}";
            if (rival.Length > 0) detail = code + " · " + detail;
            TenGoText.Create(row, detail, Fonts.AvenirMedium, 13, Look.Logo.WithAlpha(0.7f), P(textX, -12), Z + 3,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            if (shareable)
                AddTarget("duelShare:" + code, row, new Vector2(_cardWidth, height), () => S.Play.ShareDuel(code), animate: false);
        }

        // MARK: - Actions

        /// Le challenger joue d'abord : le duel n'est créé qu'avec un vrai score.
        void StartDuel()
        {
            // `UInt64.random(in: 1...UInt64.max)` ; la graine d'un duel est un `long` (bits identiques).
            ulong raw = 0;
            var rng = SystemRng.Create();
            while (raw == 0) raw = rng.NextULong();
            App.Router.StartGame(GameLaunch.Duel(unchecked((long)raw), null));
        }

        void PromptForCode()
        {
            if (CodeEntry != null) return;
            _entry = new DuelCodeEntryOverlay(Root, JoinWithCode);
            Overlay = _entry;
        }

        /// Récupère le duel et lance la partie sur la MÊME grille. Les refus
        /// (introuvable, expiré, déjà joué, son propre duel) sont dits
        /// clairement plutôt que de mener à une partie qui ne comptera pas.
        public void JoinWithCode(string code)
        {
            string normalized = DuelRules.Normalize(code);
            if (!IsWellFormed(normalized))
            {
                SetStatus(Loc.T("duel.error_not_found"));
                return;
            }
            if (_joining) return;
            _joining = true;
            SetStatus(Loc.T("duel.loading"));
            S.Duels.Fetch(normalized, duel =>
            {
                if (_disposed) return;
                _joining = false;
                string refusal = DuelRules.JoinRefusal(duel, S.Duels.Uid, System.DateTimeOffset.UtcNow);
                if (refusal != null) { SetStatus(Loc.T(refusal)); return; }
                SetStatus(Loc.F("duel.score_to_beat", duel.ChallengerName, duel.ChallengerScore));
                App.Router.StartGame(GameLaunch.Duel(duel.Seed, duel.Code));
            }, message =>
            {
                if (_disposed) return;
                _joining = false;
                SetStatus(message);
            });
        }

        void SetStatus(string text)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.Wrap(_cardWidth);
        }

        /// Six caractères de l'alphabet des codes (sans 0/O/1/I).
        public static bool IsWellFormed(string code)
        {
            if (code == null || code.Length != DuelCodeEntryOverlay.CodeLength) return false;
            foreach (char c in code) if (DuelCodeEntryOverlay.Alphabet.IndexOf(c) < 0) return false;
            return true;
        }

        public override void Back()
        {
            if (Overlay != null && Overlay.IsOpen) { Overlay.Close(); return; }
            App.ShowTab(Tab.Play);
        }

        public override void Dispose()
        {
            _disposed = true;
            base.Dispose();
        }
    }
}
