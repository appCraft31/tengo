using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
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

        static Vector2 P(float x, float y) => Units.P(x, y);

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

        void SetupUI()
        {
            float topY = SceneSize.y / 2;
            float bottomY = -HalfH + Geo.SafeBottomPx / Geo.PixelsPerScene;
            _cardWidth = Mathf.Min(UsableW - 48, 600);

            TenGoText.Create(_ui, Loc.T("achievements.title"), Fonts.AvenirHeavy, 36, MissionsScreen.TitleColor, P(0, topY - 110), Z)
                .ShrinkToFit(UsableW - 40);
            bool light = Look.Background.PerceivedLuminance() > 0.5f;
            TenGoText.Create(_ui, Loc.F("achievements.completion", S.Achievements.UnlockedCount, S.Achievements.TotalCount),
                Fonts.AvenirMedium, 15, light ? Colors.White(0.42f) : Look.Logo.WithAlpha(0.7f), P(0, topY - 142), Z);

            AddCategoryTabs(topY - 190);

            var items = S.Achievements.Achievements(Category);
            float listTop = topY - 250;
            float listBottom = bottomY + 150;
            float available = Mathf.Max(0, listTop - listBottom);
            const float gap = 12;
            int count = Mathf.Max(1, items.Count);
            float rowH = Mathf.Max(56, Mathf.Min(130, available / count - gap));
            float blockH = rowH * items.Count + gap * Mathf.Max(0, items.Count - 1);

            // Liste dans une zone défilante (marge pour le relief et l'ombre des cartes).
            const float pad = 8;
            Scroll = new ScrollArea(_ui, listTop + pad, listBottom - pad, -UsableW / 2, UsableW / 2);
            Scroll.ContentHeight = blockH + pad * 2;
            float cursorY = -pad - Mathf.Max(0, (available - blockH) / 2) - rowH / 2;
            foreach (var item in items)
            {
                AddAchievementRow(item, cursorY, rowH);
                cursorY -= rowH + gap;
            }
            Scroll.ApplyClip();

            var back = ProgressUi.BackButton(_ui, bottomY + 90, Z + 10);
            AddTarget("back", back, new Vector2(80, 80), () => App.ShowMenu());
        }

        void AddCategoryTabs(float y)
        {
            const float gap = 10;
            float tabW = (_cardWidth - gap * (TabOrder.Length - 1)) / TabOrder.Length;
            float x = -_cardWidth / 2 + tabW / 2;
            foreach (var cat in TabOrder)
            {
                bool selected = cat == Category;
                var tab = RenderShared.Node("category_" + cat.Id(), _ui, P(x, y));
                var bg = ShapeView.Rect("Bg", tab, Vector2.zero, new Vector2(tabW, 46), 23,
                    selected ? Look.Accent : Colors.White(0.9f, 0.7f), Colors.White(0.68f, 0.3f), 1, Z);
                Relief.Raise(bg, 3);
                TenGoText.Create(tab, Loc.T(cat.TitleKey()), selected ? Fonts.AvenirBold : Fonts.AvenirMedium, 15,
                    selected ? Colors.White(0.15f) : Colors.White(0.4f), Vector2.zero, Z + 2).ShrinkToFit(tabW - 12);
                var c = cat;
                AddTarget("category_" + cat.Id(), tab, new Vector2(tabW, 46), () => SelectCategory(c), animate: false);
                x += tabW + gap;
            }
        }

        void AddAchievementRow(DisplayAchievement item, float y, float height)
        {
            var def = item.Definition;
            float width = _cardWidth;
            var row = RenderShared.Node("achievement_" + def.Id, Scroll.Content, P(0, y));
            var bg = ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(width, height), 20,
                item.IsUnlocked ? new Color(0.85f, 0.95f, 0.87f, 0.95f) : new Color(0.97f, 0.95f, 0.92f, 0.9f),
                Colors.White(0.70f, 0.3f), 1, Z);
            Relief.Raise(bg, 3);

            float iconR = Mathf.Min(26, height * 0.28f);
            float iconX = -width / 2 + 22 + iconR;
            ShapeView.Circle("IconBg", row, P(iconX, 0), iconR, Look.Accent.WithAlpha(item.IsUnlocked ? 0.9f : 0.35f), default, 0, Z + 2);
            ProgressUi.CategoryIcon(row, new Vector2(iconX, 0), def.Category, iconR * 1.25f, Color.white, Z + 3);

            float textLeft = -width / 2 + 44 + iconR * 2;
            string statusText = item.IsUnlocked ? Loc.T("achievements.unlocked") : item.CurrentValue + " / " + def.Target;
            var status = ProgressUi.Right(row, statusText, Fonts.AvenirBold, 15,
                item.IsUnlocked ? new Color(0.32f, 0.58f, 0.38f) : Colors.White(0.45f), width / 2 - 16, height * 0.18f, Z + 2);
            float titleMax = width / 2 - 16 - status.WidthPt() - 12 - textLeft;
            ProgressUi.Left(row, ProgressUi.AchievementTitle(def), Fonts.AvenirDemiBold, 19, Colors.White(0.24f),
                textLeft, height * 0.18f, Z + 2, titleMax);
            ProgressUi.Left(row, ProgressUi.AchievementDescription(def), Fonts.AvenirMedium, 14, Colors.White(0.42f),
                textLeft, -height * 0.17f, Z + 2, width / 2 - 16 - textLeft);
        }

        public override void Back() => App.ShowMenu();
    }
}
