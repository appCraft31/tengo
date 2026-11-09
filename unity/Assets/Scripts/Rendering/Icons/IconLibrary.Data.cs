using System.Collections.Generic;

namespace TenGo.Rendering
{
    public static partial class IconLibrary
    {
        /// Contenu des SVG de `ios/tenGO/Assets.xcassets/<Nom>.imageset/` (viewBox
        /// 24 × 24, trait 2, bouts et jonctions ronds), recopié tel quel ;
        /// `IconLibraryTests` vérifie qu'il n'a pas divergé des fichiers iOS.
        public static readonly IReadOnlyDictionary<string, string> Svg = new Dictionary<string, string>
        {
            ["BoosterHammer"] = @"<path d=""m15 12-8.4 8.4a2 2 0 0 1-2.9-2.9L12 9""/>
<path d=""M17.6 15 22 10.6""/>
<path d=""M21 11.7 19.7 10.4a3 3 0 0 1-.9-2.1v-.9l-2.8-2.8A5.5 5.5 0 0 0 12 3H9l.9.8A6 6 0 0 1 12 8.4V10l2 2h1.6a3 3 0 0 1 2.1.9l1.3 1.3""/>",
            ["BoosterHint"] = @"<path d=""M9 18h6""/>
<path d=""M10 21.5h4""/>
<path d=""M12 2.5a6.5 6.5 0 0 0-4 11.6c.75.75 1 1.4 1 2.4h6c0-1 .25-1.65 1-2.4A6.5 6.5 0 0 0 12 2.5Z""/>",
            ["BoosterShuffle"] = @"<polyline points=""16 4 20 4 20 8""/>
<line x1=""4"" y1=""20"" x2=""20"" y2=""4""/>
<polyline points=""20 16 20 20 16 20""/>
<line x1=""15"" y1=""15"" x2=""20"" y2=""20""/>
<line x1=""4"" y1=""4"" x2=""9"" y2=""9""/>",
            ["IconDuel"] = @"<path d=""M19.4 4.6 9.8 14.2""/>
<path d=""M15.9 4.6h3.5v3.5""/>
<path d=""M4.6 4.6l9.6 9.6""/>
<path d=""M8.1 4.6H4.6v3.5""/>
<path d=""m7.4 17 2.4-2.8 2.2 2.2-2.8 2.4a1.3 1.3 0 0 1-1.8-1.8Z""/>
<path d=""m16.6 17-2.4-2.8-2.2 2.2 2.8 2.4a1.3 1.3 0 0 0 1.8-1.8Z""/>",
            ["IconFlame"] = @"<path d=""M12.6 2.7c.3 2.6-1 4-2.4 5.2-1.5 1.3-3.1 2.8-3.1 5.5a6.9 6.9 0 0 0 13.8 0c0-3.4-2.3-5.2-4-7.3-.3 1.2-1 2-1.9 2.4.4-2.2-.5-4.3-2.4-5.8Z""/>
<path d=""M12 21.4a3.2 3.2 0 0 1-1.5-6c.9 1 2.1 1.1 2.7.2.9.8 1.5 1.9 1.5 2.9a2.7 2.7 0 0 1-2.7 2.9Z""/>",
            ["IconPlay"] = @"<path d=""M8.5 5.2v13.6a1 1 0 0 0 1.53.85l10.4-6.8a1 1 0 0 0 0-1.7L10.03 4.35A1 1 0 0 0 8.5 5.2Z""/>",
            ["IconProgress"] = @"<path d=""M3.5 20.5h17""/>
<path d=""M6.5 20.5v-5.2""/>
<path d=""M12 20.5V7.8""/>
<path d=""M17.5 20.5v-8.4""/>",
            ["IconPuzzle"] = @"<path d=""M9.8 4.4a2.1 2.1 0 0 1 4.2 0c0 .35-.08.68-.23.97h2.13a1 1 0 0 1 1 1v2.13c.29-.15.62-.23.97-.23a2.1 2.1 0 0 1 0 4.2c-.35 0-.68-.08-.97-.23v3.83a1 1 0 0 1-1 1h-3.83c.15.29.23.62.23.97a2.1 2.1 0 0 1-4.2 0c0-.35.08-.68.23-.97H6.5a1 1 0 0 1-1-1V6.37a1 1 0 0 1 1-1h3.53a2.08 2.08 0 0 1-.23-.97Z""/>",
            ["IconRush"] = @"<path d=""M13.2 2.6 5.4 13.4h5.1L10.8 21.4l7.8-10.8h-5.1l-.3-8Z""/>",
            ["IconShare"] = @"<path d=""M12 15.2V3.4""/>
<path d=""m8.2 7.2 3.8-3.8 3.8 3.8""/>
<path d=""M5.4 12.6v6.4a1.6 1.6 0 0 0 1.6 1.6h10a1.6 1.6 0 0 0 1.6-1.6v-6.4""/>",
            ["IconShield"] = @"<path d=""M12 2.8 5.4 5.4v5.9c0 4.3 2.7 8.2 6.6 9.9 3.9-1.7 6.6-5.6 6.6-9.9V5.4L12 2.8Z""/>
<path d=""m9.3 11.8 1.9 1.9 3.5-3.5""/>",
            ["IconShop"] = @"<path d=""M5.2 7.8h13.6l-1.15 11.9a1 1 0 0 1-1 .9H7.35a1 1 0 0 1-1-.9L5.2 7.8Z""/>
<path d=""M9 7.8V6.2a3 3 0 0 1 6 0v1.6""/>",
            ["IconSocial"] = @"<path d=""M9 11.2a3.6 3.6 0 1 0 0-7.2 3.6 3.6 0 0 0 0 7.2Z""/>
<path d=""M2.8 20.4a6.2 6.2 0 0 1 12.4 0""/>
<path d=""M16.4 4.6a3.1 3.1 0 0 1 0 6""/>
<path d=""M18.2 13.4a5.6 5.6 0 0 1 3 5""/>",
        };
    }
}
