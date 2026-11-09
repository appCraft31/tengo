using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Platform;
using TenGo.Rendering;
using TenGo.Services;
using TenGo.Ui;
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

        static float Pt(float v) => Design.Pt(v);

        // MARK: - UI (refonte 4.0, `design/refonte/06_autres`)

        void SetupUI()
        {
            _cardWidth = ContentW;
            AddTabTitle(Loc.T("duel.title"), _cardWidth);
            AddTabBar(Tab.Social);

            float cursor = HeaderBottom - Pt(10);
            float pitchH = Pt(232);
            AddPitchCard(cursor - pitchH / 2, pitchH);
            cursor -= pitchH + Pt(20);

            float left = -_cardWidth / 2;
            Design.Text(Root, Loc.T("duel.answer"), NunitoWeight.Black, 17, Design.Ink, new Vector2(left, cursor - Pt(10)), Z + 2,
                HorizontalAlignmentOptions.Left).ShrinkToFit(_cardWidth);
            cursor -= Pt(20 + 10);
            float codeH = Pt(68);
            AddCodeCard(cursor - codeH / 2, codeH);
            cursor -= codeH;

            _status = Design.Text(Root, "", NunitoWeight.Bold, 13, Design.Ink2, new Vector2(0, cursor - Pt(14)), Z + 2);
            _status.Wrap(_cardWidth);
            _listTopY = cursor - Pt(28);
        }

        /// Carte d'appel blanche : « moi VS ? », la règle, l'action principale.
        void AddPitchCard(float y, float height)
        {
            float half = height / 2;
            var card = RenderShared.Node("PitchCard", Root, Units.P(0, y));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cardWidth, height), Pt(Design.CardRadius), Z);

            float avatarY = half - Pt(20 + 33);
            // Initiale du pseudo Game Center / Play Games, sinon une silhouette.
            string me = S.Play.PlayerName?.Trim();
            AddAvatar(card, string.IsNullOrEmpty(me) ? null : me.Substring(0, 1).ToUpperInvariant(), Pt(33), Design.Tint(Look.BubbleColor(7), 0.5f),
                new Vector2(-Pt(60), avatarY));
            AddAvatar(card, "?", Pt(33), Design.Sunk, new Vector2(Pt(60), avatarY));
            Design.Text(card, "VS", NunitoWeight.Black, 15, Design.Ink3, new Vector2(0, avatarY), Z + 3);

            Design.Text(card, Loc.T("duel.subtitle"), NunitoWeight.Bold, 15, Design.Ink2, new Vector2(0, avatarY - Pt(33 + 32)), Z + 2)
                .Wrap(_cardWidth - Pt(48));

            float bw = _cardWidth - Pt(32);
            var button = Design.Button("startDuel", card, new Vector2(0, -half + Pt(16 + 28)), bw, Loc.T("duel.start"), true, Z + 2, VectorIconName.LineDuel);
            AddTarget("startDuel", button, new Vector2(bw, Pt(56)), StartDuel);
            AddTarget("newDuel", button, new Vector2(bw, Pt(56)), StartDuel);
        }

        /// Pastille ronde portant une initiale (ou « ? »), silhouette sans nom.
        void AddAvatar(Transform parent, string initial, float radius, Color fill, Vector2 pos)
        {
            ShapeView.Circle("Avatar", parent, Units.P(pos.x, pos.y), radius, fill, default, 0, Z + 2);
            if (initial != null)
                Design.Text(parent, initial, NunitoWeight.Black, radius / Design.Scale * 0.8f, initial == "?" ? Design.Ink3 : Design.Ink, pos, Z + 3);
            else
                VectorIcon.Create(parent, VectorIconName.LineProfile, Units.P(pos.x, pos.y), radius * 0.9f, Design.Ink, Z + 3);
        }

        /// Carte « j'ai reçu un code » : un faux champ et « Jouer » sur une ligne ; le tap ouvre la saisie.
        void AddCodeCard(float y, float height)
        {
            var card = RenderShared.Node("answerDuel", Root, Units.P(0, y));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cardWidth, height), Pt(Design.CardRadius), Z);
            float pad = Pt(10), h = height - 2 * pad;
            var go = Design.Text(card, Loc.T("duel.go"), NunitoWeight.Black, 16, Design.Bg, Vector2.zero, Z + 3);
            float goW = Mathf.Max(Pt(80), go.WidthPt() + Pt(36));
            float goX = _cardWidth / 2 - pad - goW / 2;
            ShapeView.Rect("Go", card, Units.P(goX, 0), new Vector2(goW, h), Pt(14), Design.Ink, default, 0, Z + 1);
            go.transform.localPosition = Units.P(goX, 0);
            float fieldW = _cardWidth - 3 * pad - goW;
            float fieldX = -_cardWidth / 2 + pad + fieldW / 2;
            ShapeView.Rect("Field", card, Units.P(fieldX, 0), new Vector2(fieldW, h), Pt(14), Design.Sunk, default, 0, Z + 1);
            Design.Text(card, "A B C 1 2 3", NunitoWeight.Black, 18, Design.Ink3, new Vector2(fieldX - fieldW / 2 + Pt(16), 0), Z + 2,
                HorizontalAlignmentOptions.Left);
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
            float left = -_cardWidth / 2;
            Design.Text(Root, Loc.T("duel.section_list"), NunitoWeight.Black, 17, Design.Ink, new Vector2(left, _listTopY - Pt(10)), Z + 2,
                HorizontalAlignmentOptions.Left);
            Design.Text(Root, _duels.Count.ToString(), NunitoWeight.ExtraBold, 13, Design.Ink3, new Vector2(-left, _listTopY - Pt(10)), Z + 2,
                HorizontalAlignmentOptions.Right);

            // Liste défilante entre l'en-tête et les onglets.
            Scroll = new ScrollArea(Root, _listTopY - Pt(24), TabBarTop, -UsableW / 2, UsableW / 2);
            float rowH = Pt(72);
            var now = System.DateTimeOffset.UtcNow;
            float cardH = rowH * _duels.Count;
            var card = RenderShared.Node("DuelList", Scroll.Content, Units.P(0, -Pt(4) - cardH / 2));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cardWidth, cardH), Pt(Design.CardRadius), Z);
            for (int i = 0; i < _duels.Count; i++)
                AddDuelRow(card, _duels[i], uid, now, cardH / 2 - rowH * (i + 0.5f), rowH, i > 0);
            Scroll.ContentHeight = cardH + Pt(20);
            Scroll.ApplyClip();
        }

        void AddDuelRow(Transform card, Duel duel, string uid, System.DateTimeOffset now, float y, float height, bool separator)
        {
            string code = duel.Code;
            // Duel lancé par ce joueur et toujours sans adversaire : on revient
            // ici quand on a oublié d'envoyer le code sur le moment.
            bool shareable = !duel.IsComplete && !duel.IsExpired(now) && duel.ChallengerUid == uid;
            var row = RenderShared.Node(shareable ? "duelShare:" + code : "duelRow", card, Units.P(0, y));
            if (separator) ShapeView.Rect("Sep", row, Units.P(0, height / 2), new Vector2(_cardWidth, Pt(1)), 0, Design.Line, default, 0, Z + 1);

            // Statut : pastille colorée à droite.
            string statusKey;
            Color fill = Design.Sunk, ink = Design.Ink2, tint = Look.BubbleColor(7);
            var icon = VectorIconName.LineDuel;
            if (duel.IsComplete && duel.OutcomeFor(uid) is DuelOutcome outcome)
            {
                switch (outcome)
                {
                    case DuelOutcome.Win:
                        statusKey = "duel.row_win";
                        fill = Design.Hex(0xE3F2EA); ink = Design.Hex(0x2F7A52); tint = Look.BubbleColor(5); icon = VectorIconName.LineTrophy;
                        break;
                    case DuelOutcome.Loss:
                        statusKey = "duel.row_loss";
                        fill = Design.Hex(0xF6E3E1); ink = Design.Hex(0x9A3B35); tint = Look.BubbleColor(1);
                        break;
                    default: statusKey = "duel.row_draw"; break;
                }
            }
            else statusKey = duel.IsExpired(now) ? "duel.row_expired" : "duel.row_pending";

            float lx = -_cardWidth / 2 + Pt(14), rx = _cardWidth / 2 - Pt(14);
            Design.IconChip(row, new Vector2(lx + Pt(20), 0), 40, 13, tint, icon, Z + 2, Design.Ink, 21);
            if (shareable) VectorIcon.Create(row, VectorIconName.LineShare, Units.P(rx - Pt(11), 0), Pt(20), Design.Ink2, Z + 3);
            float tagRight = rx - (shareable ? Pt(32) : 0);
            var status = Design.Text(row, Loc.T(statusKey), NunitoWeight.Black, 13, ink, Vector2.zero, Z + 3);
            float tagW = status.WidthPt() + Pt(22);
            float tagX = tagRight - tagW / 2;
            ShapeView.Rect("Tag", row, Units.P(tagX, 0), new Vector2(tagW, Pt(28)), Pt(14), fill, default, 0, Z + 2);
            status.transform.localPosition = Units.P(tagX, 0);

            // Code (ou nom de l'adversaire), puis les scores.
            string rival = duel.RivalName(uid).Trim();
            float textX = lx + Pt(40 + 12);
            float textW = tagX - tagW / 2 - Pt(10) - textX;
            Design.Text(row, rival.Length == 0 ? code : rival, NunitoWeight.Black, 16, Design.Ink, new Vector2(textX, Pt(10)), Z + 3,
                HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            int mine = uid == duel.ChallengerUid ? duel.ChallengerScore : (duel.OpponentScore ?? 0);
            int? theirs = uid == duel.ChallengerUid ? duel.OpponentScore : duel.ChallengerScore;
            string points = Loc.T("game.points_label");
            string detail = theirs is int t ? $"{Design.Number(mine)} – {Design.Number(t)} {points}" : $"{Design.Number(mine)} {points}";
            if (rival.Length > 0) detail = code + " · " + detail;
            Design.Text(row, detail, NunitoWeight.Bold, 12, Design.Ink2, new Vector2(textX, -Pt(11)), Z + 3, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(textW);
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
