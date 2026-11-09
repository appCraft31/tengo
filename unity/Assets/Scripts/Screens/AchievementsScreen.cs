using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// Liste des succès (AchievementsScene.swift), filtrée par catégorie, avec
    /// la complétion globale. La liste défile quand elle dépasse la place
    /// (iOS tronquait au-delà de la hauteur disponible).
    /// Cibles : « category_chain|perfect|score|daily », « back ».
    public sealed class AchievementsScreen : ScreenBase
    {
        const int Z = 20;
        static readonly AchievementCategory[] TabOrder =
            { AchievementCategory.Chain, AchievementCategory.Perfect, AchievementCategory.Score, AchievementCategory.Daily };

        Transform _ui;
        float _cardWidth = 340;
        public AchievementCategory Category { get; private set; } = AchievementCategory.Chain;
        /// Le défilement de la liste (tests).
        public ScrollArea List => Scroll;


        public AchievementsScreen(AppRoot app) : base(app, "AchievementsScreen")
        {
            TapOnRelease = true;
            Rebuild();
        }

        /// Change de catégorie (fondu 0,18 s comme iOS).
        public void SelectCategory(AchievementCategory category)
        {
            if (category == Category) return;
            Category = category;
            Rebuild();
            FxAlpha.Of(_ui.gameObject).Alpha = 0;
            Tw.Run(_ui.gameObject, Tw.FadeIn(_ui.gameObject, 0.18f), "fade");
        }

        void Rebuild()
        {
            if (_ui != null) RenderShared.Destroy(_ui.gameObject);
            Targets.Clear();
            Scroll = null;
            _ui = RenderShared.Node("AchievementsUI", Root);
            SetupUI();
        }

        static float Pt(float v) => Design.Pt(v);

        /// Refonte 4.0 : retour en haut à gauche avec la complétion à droite,
        /// catégories en contrôle segmenté, liste compacte dans une carte
        /// (débloqué = pastille colorée + coche, sinon cadenas, barre et compteur).
        void SetupUI()
        {
            _cardWidth = ContentW;
            AddBackHeader(Loc.T("achievements.title"), S.Achievements.UnlockedCount + " / " + S.Achievements.TotalCount, () => App.ShowMenu(), _ui);

            float segY = HeaderBottom - Pt(10 + 20);
            var labels = new string[TabOrder.Length];
            for (int i = 0; i < TabOrder.Length; i++) labels[i] = Loc.T(TabOrder[i].TitleKey());
            var segs = Design.Segmented(_ui, new Vector2(0, segY), _cardWidth, labels, System.Array.IndexOf(TabOrder, Category), Z);
            float segW = (_cardWidth - Pt(8)) / TabOrder.Length;
            for (int i = 0; i < TabOrder.Length; i++)
            {
                var c = TabOrder[i];
                segs[i].name = "category_" + c.Id();
                AddTarget("category_" + c.Id(), segs[i], new Vector2(segW, Pt(40)), () => SelectCategory(c), animate: false);
            }

            var items = S.Achievements.Achievements(Category);
            float listTop = segY - Pt(20 + 12);
            Scroll = new ScrollArea(_ui, listTop, -HalfH + BottomInset, -UsableW / 2, UsableW / 2);
            float rowH = Pt(72);
            float cardH = rowH * items.Count;
            if (items.Count > 0)
            {
                var card = RenderShared.Node("List", Scroll.Content, Units.P(0, -Pt(4) - cardH / 2));
                Design.Surface("Bg", card, Vector2.zero, new Vector2(_cardWidth, cardH), Pt(Design.CardRadius), Z);
                for (int i = 0; i < items.Count; i++)
                    AddAchievementRow(card, items[i], cardH / 2 - rowH * (i + 0.5f), rowH, i > 0);
            }
            Scroll.ContentHeight = cardH + Pt(24);
            Scroll.ApplyClip();
        }

        static readonly System.Collections.Generic.Dictionary<AchievementCategory, int> CategoryTint = new()
        {
            { AchievementCategory.Chain, 3 }, { AchievementCategory.Perfect, 2 }, { AchievementCategory.Score, 9 }, { AchievementCategory.Daily, 4 },
        };

        void AddAchievementRow(Transform card, DisplayAchievement item, float y, float height, bool separator)
        {
            var def = item.Definition;
            float w = _cardWidth;
            var row = RenderShared.Node("achievement_" + def.Id, card, Units.P(0, y));
            if (separator) ShapeView.Rect("Sep", row, Units.P(0, height / 2), new Vector2(w, Pt(1)), 0, Design.Line, default, 0, Z + 1);
            float lx = -w / 2 + Pt(14), rx = w / 2 - Pt(14);
            bool ok = item.IsUnlocked;
            Design.IconChip(row, new Vector2(lx + Pt(20), 0), 40, 13, ok ? Look.BubbleColor(CategoryTint[def.Category]) : Design.Sunk,
                ok ? ProfileScreen.CategoryIcon(def.Category) : VectorIconName.LineLock, Z + 2, ok ? Design.Ink : Design.Ink3, 21, ok ? 0.55f : 1f);
            float textX = lx + Pt(40 + 12);
            float statusW;
            if (ok)
            {
                VectorIcon.Create(row, VectorIconName.LineCheck, Units.P(rx - Pt(10), 0), Pt(20), Design.SeedGreen, Z + 2);
                statusW = Pt(20);
            }
            else
            {
                var v = Design.Text(row, item.CurrentValue + " / " + def.Target, NunitoWeight.Black, 12, Design.Ink3, new Vector2(rx, 0), Z + 2,
                    TMPro.HorizontalAlignmentOptions.Right);
                statusW = v.WidthPt();
            }
            float textW = rx - statusW - Pt(10) - textX;
            float ty = ok ? Pt(9) : Pt(15);
            Design.Text(row, ProgressUi.AchievementTitle(def), NunitoWeight.Black, 15, Design.Ink, new Vector2(textX, ty), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            Design.Text(row, ProgressUi.AchievementDescription(def), NunitoWeight.Bold, 12, Design.Ink2, new Vector2(textX, ty - Pt(19)), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            if (!ok)
            {
                float ratio = def.Target > 0 ? Mathf.Clamp01(item.CurrentValue / (float)def.Target) : 0;
                Design.Bar(row, new Vector2(textX, ty - Pt(36)), 120, 6, ratio, Design.Ink, Z + 2);
            }
        }

        public override void Back() => App.ShowMenu();
    }
}
