using System;
using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// Saisie d'un code de duel. iOS ouvre une alerte UIKit avec un champ de
    /// texte (`promptForCode`) ; SpriteKit n'ayant pas de champ, le portage
    /// pose dans la scène une carte du style des réglages : titre
    /// (`duel.answer`), consigne (`duel.enter_code`), six cases et un clavier
    /// réduit à l'alphabet des codes (sans 0/O/1/I), « Annuler » et « Jouer ».
    /// Même rendu dans l'éditeur, les tests et sur tous les appareils, sans
    /// clavier système. Un tap hors de la carte annule.
    public sealed class DuelCodeEntryOverlay : IGameOverlay
    {
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 6;
        const int Z = 700;
        const float CardW = 560;
        const float KeyW = 56, KeyH = 62, KeyGap = 8;

        sealed class Key
        {
            public Rect Frame;
            public Transform Node;
            public Action Action;
        }

        readonly Transform _root;
        readonly Transform _card;
        readonly List<Key> _keys = new();
        readonly List<TenGoText> _slots = new();
        readonly List<ShapeView> _slotBgs = new();
        readonly Action<string> _onSubmit;
        readonly float _cardH;
        string _code = "";
        bool _open = true;

        public bool IsOpen => _open && _root != null;
        public string Code => _code;

        public DuelCodeEntryOverlay(Transform parent, Action<string> onSubmit)
        {
            _onSubmit = onSubmit;
            _root = RenderShared.Node("DuelCodeEntry", parent);
            FxAlpha.Of(_root.gameObject).Alpha = 0;
            ShapeView.Rect("Dim", _root, Vector2.zero, new Vector2(1875, 3335), 0, Colors.White(0, 0.55f), default, 0, Z);

            int rows = Mathf.CeilToInt(Alphabet.Length / 8f);
            float keysH = rows * KeyH + (rows - 1) * KeyGap;
            _cardH = 48 + 40 + 34 + 30 + 70 + 30 + keysH + 24 + 56 + 32;
            _card = RenderShared.Node("Card", _root);
            FxAlpha.Of(_card.gameObject);
            ShapeView.Rect("Bg", _card, Vector2.zero, new Vector2(CardW, _cardH), 28, new Color(0.98f, 0.96f, 0.93f), default, 0, Z + 1);

            float y = _cardH / 2 - 48;
            TenGoText.Create(_card, Loc.T("duel.answer"), Fonts.AvenirHeavy, 28, Colors.White(0.25f), Units.P(0, y), Z + 3).ShrinkToFit(CardW - 60);
            y -= 40;
            TenGoText.Create(_card, Loc.T("duel.enter_code"), Fonts.AvenirMedium, 17, Colors.White(0.42f), Units.P(0, y), Z + 3).ShrinkToFit(CardW - 60);
            y -= 34 + 30 + 35;

            // Six cases.
            const float slotW = 58, slotH = 70, slotGap = 12;
            float slotsW = CodeLength * slotW + (CodeLength - 1) * slotGap;
            for (int i = 0; i < CodeLength; i++)
            {
                float x = -slotsW / 2 + slotW / 2 + i * (slotW + slotGap);
                var bg = ShapeView.Rect("Slot", _card, Units.P(x, y), new Vector2(slotW, slotH), 16, Color.white,
                    Colors.White(0.80f), 1.5f, Z + 2);
                _slotBgs.Add(bg);
                _slots.Add(TenGoText.Create(_card, "", Fonts.AvenirHeavy, 32, Colors.White(0.24f), Units.P(x, y), Z + 3));
            }
            y -= 35 + 30;

            // Clavier : 8 touches par rangée.
            float rowW = 8 * KeyW + 7 * KeyGap;
            for (int i = 0; i < Alphabet.Length; i++)
            {
                int col = i % 8, row = i / 8;
                float x = -rowW / 2 + KeyW / 2 + col * (KeyW + KeyGap);
                float ky = y - KeyH / 2 - row * (KeyH + KeyGap);
                char c = Alphabet[i];
                AddKey("key_" + c, x, ky, new Vector2(KeyW, KeyH), Color.white, c.ToString(), Colors.White(0.26f), 24, () => Type(c), 3);
            }
            y -= keysH + 24;

            // Effacer · Annuler · Jouer.
            float bw = (CardW - 40 - 2 * 12 - 80) / 2f;
            float bx = -CardW / 2 + 20;
            var erase = AddKey("erase", bx + 40, y - 28, new Vector2(80, 56), Color.white, null, Color.clear, 0, Erase, 3);
            // « ⌫ » : flèche-étiquette et croix, en trait.
            var ink = Colors.White(0.35f);
            StrokeView.Create("Tag", erase.Node, new[] { Units.P(-15, 0), Units.P(-7, 9), Units.P(14, 9), Units.P(14, -9), Units.P(-7, -9), Units.P(-15, 0) },
                ink, 2.2f, Z + 4);
            StrokeView.Create("X1", erase.Node, new[] { Units.P(-1, 4.5f), Units.P(8, -4.5f) }, ink, 2.2f, Z + 4);
            StrokeView.Create("X2", erase.Node, new[] { Units.P(-1, -4.5f), Units.P(8, 4.5f) }, ink, 2.2f, Z + 4);
            AddKey("cancel", bx + 80 + 12 + bw / 2, y - 28, new Vector2(bw, 56), new Color(0.94f, 0.91f, 0.88f), Loc.T("common.cancel"),
                Colors.White(0.35f), 19, Close, 4);
            var go = Look.BubbleColor(4);
            AddKey("submit", bx + 80 + 12 + bw + 12 + bw / 2, y - 28, new Vector2(bw, 56), go, Loc.T("duel.go"), go.ReadableInk(), 19,
                Submit, 4);

            Refresh();
            Tw.Run(_root.gameObject, Tw.FadeIn(_root.gameObject, 0.18f));
            _card.localScale = Vector3.one * 0.92f;
            Tw.Run(_card.gameObject, Tw.Scale(_card, 1f, 0.22f));
        }

        Key AddKey(string name, float x, float y, Vector2 size, Color fill, string label, Color ink, float fontPt, Action action, float depth)
        {
            var node = RenderShared.Node(name, _card, Units.P(x, y));
            var bg = ShapeView.Rect("Bg", node, Vector2.zero, size, Mathf.Min(size.y / 2, 16), fill, default, 0, Z + 3);
            Relief.Raise(bg, depth);
            if (label != null)
                TenGoText.Create(node, label, Fonts.AvenirBold, fontPt, ink, Vector2.zero, Z + 5).ShrinkToFit(size.x - 16);
            var key = new Key { Frame = new Rect(x - size.x / 2, y - size.y / 2, size.x, size.y), Node = node, Action = action };
            _keys.Add(key);
            return key;
        }

        void Refresh()
        {
            for (int i = 0; i < CodeLength; i++)
            {
                _slots[i].Text = i < _code.Length ? _code[i].ToString() : "";
                bool active = i == Mathf.Min(_code.Length, CodeLength - 1) && _code.Length < CodeLength;
                _slotBgs[i].Stroke = active ? Look.BubbleColor(4).LedgeShade() : Colors.White(0.80f);
                _slotBgs[i].LineWidthPt = active ? 3f : 1.5f;
            }
        }

        public void Type(char c)
        {
            if (_code.Length >= CodeLength) return;
            c = char.ToUpperInvariant(c);
            if (Alphabet.IndexOf(c) < 0) return;
            _code += c;
            Refresh();
        }

        public void Erase()
        {
            if (_code.Length == 0) return;
            _code = _code.Substring(0, _code.Length - 1);
            Refresh();
        }

        public void Submit()
        {
            // Comme l'alerte iOS : un champ vide ne fait rien.
            if (_code.Length == 0) return;
            var code = _code;
            Close();
            _onSubmit?.Invoke(code);
        }

        public void HandleTouch(Vector2 pt)
        {
            if (!IsOpen) return;
            Vector2 local = pt - (Vector2)_card.position * Units.PointsPerUnit;
            foreach (var k in _keys)
            {
                if (!k.Frame.Contains(local)) continue;
                TapFeedback.Play();
                var n = k.Node;
                Tw.Run(n.gameObject, Tw.Seq(Tw.Scale(n, 0.93f, 0.07f), Tw.Scale(n, 1f, 0.12f)), "tap");
                k.Action?.Invoke();
                return;
            }
            if (Mathf.Abs(local.x) > CardW / 2 || Mathf.Abs(local.y) > _cardH / 2) Close();
        }

        public void Close()
        {
            if (!_open || _root == null) return;
            _open = false;
            var go = _root.gameObject;
            Tw.Run(go, Tw.Seq(Tw.FadeOut(go, 0.15f), Tw.Remove(go)));
        }
    }
}
