using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Tests
{
    /// Icônes vectorielles : tracés embarqués identiques aux SVG iOS, lecture correcte.
    public class IconTests
    {
        static string IosAssets => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Archives/tenGO/ios/tenGO/Assets.xcassets"));

        [Test]
        public void EmbeddedSvgMatchIosCatalog()
        {
            var names = IconLibrary.Svg.Keys.ToArray();
            Assert.AreEqual(13, names.Length);
            if (!Directory.Exists(IosAssets)) Assert.Ignore("Dossier iOS absent");
            foreach (var name in names)
            {
                var svg = Directory.GetFiles(Path.Combine(IosAssets, name + ".imageset"), "*.svg").Single();
                var inner = string.Join("\n", File.ReadAllLines(svg).Where(l => !l.StartsWith("<svg") && !l.StartsWith("</svg")).Select(l => l.Trim()));
                Assert.AreEqual(inner, IconLibrary.Svg[name], name);
            }
        }

        static string DesignJs => Path.GetFullPath(Path.Combine(Application.dataPath, "../../design/refonte/systeme.js"));

        /// La famille au trait de la refonte est recopiée de `design/refonte/systeme.js`, sans écart.
        [Test]
        public void LineIconsMatchDesignSystem()
        {
            var lineNames = System.Enum.GetNames(typeof(VectorIconName)).Where(n => n.StartsWith("Line")).ToArray();
            Assert.AreEqual(27, lineNames.Length);
            CollectionAssert.AreEquivalent(lineNames, IconLibrary.LineSvg.Keys);
            if (!File.Exists(DesignJs)) Assert.Ignore("Maquette absente");
            var js = File.ReadAllText(DesignJs);
            var block = js.Substring(js.IndexOf("const ICONS = {"));
            block = block.Substring(0, block.IndexOf("};"));
            var found = Regex.Matches(block, @"^\s*(\w+): '(.*)',\s*$", RegexOptions.Multiline);
            Assert.AreEqual(27, found.Count);
            foreach (Match m in found)
            {
                var key = "Line" + char.ToUpperInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value.Substring(1);
                Assert.AreEqual(m.Groups[2].Value, IconLibrary.LineSvg[key], key);
            }
        }

        [Test]
        public void CircleAndRoundedRectBecomeClosedPaths()
        {
            var circle = SvgPath.ParseElements(@"<circle cx=""12"" cy=""8"" r=""4""/>").Single();
            Assert.AreEqual(4f, circle.Max(p => p.x) - 12f, 0.02f);
            Assert.AreEqual(4f, 8f - circle.Min(p => p.y), 0.02f);
            Assert.AreEqual(circle[0].x, circle[^1].x, 1e-3f);
            var rect = SvgPath.ParseElements(@"<rect x=""4"" y=""5"" width=""16"" height=""15"" rx=""2.5""/>").Single();
            Assert.AreEqual(4f, rect.Min(p => p.x), 1e-3f);
            Assert.AreEqual(20f, rect.Max(p => p.x), 1e-3f);
            Assert.AreEqual(5f, rect.Min(p => p.y), 1e-3f);
            Assert.AreEqual(20f, rect.Max(p => p.y), 1e-3f);
        }

        [Test]
        public void AllIconsParseInsideViewBox()
        {
            foreach (VectorIconName n in System.Enum.GetValues(typeof(VectorIconName)))
            {
                var paths = IconLibrary.Paths(n);
                Assert.IsNotEmpty(paths, n.ToString());
                foreach (var p in paths.SelectMany(x => x))
                {
                    Assert.That(p.x, Is.InRange(0f, 24f), n.ToString());
                    Assert.That(p.y, Is.InRange(0f, 24f), n.ToString());
                }
            }
        }

        [Test]
        public void RelativeCommandsArcsAndCloseAreHandled()
        {
            var pl = SvgPath.ParsePath("M2 2h4v4H2Z").Single();
            Assert.AreEqual(new Vector2(2, 2), pl[0]);
            Assert.AreEqual(new Vector2(6, 2), pl[1]);
            Assert.AreEqual(new Vector2(6, 6), pl[2]);
            Assert.AreEqual(new Vector2(2, 2), pl[^1]);
            // Demi-cercle de rayon 2 de (0,0) à (4,0) : passe par (2,±2).
            var arc = SvgPath.ParsePath("M0 0a2 2 0 0 1 4 0").Single();
            Assert.AreEqual(new Vector2(4, 0), arc[^1]);
            Assert.AreEqual(2f, arc.Max(p => Mathf.Abs(p.y)), 0.02f);
            // Nombres collés : « 1-.9 » et drapeaux sans séparateur.
            var b = SvgPath.ParsePath("M1 1l1-.9.5.5").Single();
            Assert.AreEqual(new Vector2(2, 0.1f).x, b[1].x, 1e-5f);
            Assert.AreEqual(0.1f, b[1].y, 1e-5f);
            Assert.AreEqual(new Vector2(2.5f, 0.6f).y, b[2].y, 1e-5f);
        }

        [Test]
        public void DistanceFieldIsZeroOnStrokeAndGrowsAway()
        {
            // IconPlay : triangle dont un sommet du trait passe par (8.5, 12).
            var paths = IconLibrary.Paths(VectorIconName.IconPlay);
            Assert.AreEqual(0f, IconLibrary.Distance(paths, new Vector2(8.5f, 12f)), 1e-4f);
            Assert.AreEqual(3.5f, IconLibrary.Distance(paths, new Vector2(5f, 12f)), 1e-3f);
            var tex = IconLibrary.Field(VectorIconName.IconPlay);
            Assert.AreEqual(IconLibrary.Resolution, tex.width);
            Assert.AreSame(tex, IconLibrary.Field(VectorIconName.IconPlay));
        }
    }
}
