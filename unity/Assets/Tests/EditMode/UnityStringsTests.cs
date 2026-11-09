using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Tests
{
    /// Textes propres à Unity (refonte 4.0, `strings_unity.json`) : toutes les
    /// langues du catalogue, fusion dans `Loc`, et glyphes CJK déjà présents
    /// dans les polices de repli (réduites aux caractères du catalogue iOS).
    public class UnityStringsTests
    {
        static string Dir => Path.Combine(Application.dataPath, "Resources/Localization");
        static string Xcstrings => Path.GetFullPath(Path.Combine(Application.dataPath, "../../ios/tenGO/Localizable.xcstrings"));

        [Test]
        public void EveryKeyHasEveryLanguage()
        {
            var main = JObject.Parse(File.ReadAllText(Path.Combine(Dir, "strings.json")));
            var extra = JObject.Parse(File.ReadAllText(Path.Combine(Dir, "strings_unity.json")));
            var langs = main["languages"]!.Select(l => (string)l).ToList();
            foreach (var entry in (JObject)extra["strings"]!)
            {
                var per = (JObject)entry.Value!;
                foreach (var l in langs) Assert.IsFalse(string.IsNullOrEmpty((string)per[l]), entry.Key + " / " + l);
                Assert.IsFalse(((JObject)main["strings"]!).ContainsKey(entry.Key), "clé déjà dans le catalogue iOS : " + entry.Key);
            }
        }

        [Test]
        public void MergeAddsKeysToLoc()
        {
            Loc.Load(File.ReadAllText(Path.Combine(Dir, "strings.json")), "fr");
            Loc.Merge(File.ReadAllText(Path.Combine(Dir, "strings_unity.json")));
            Assert.AreEqual("Meilleur 4 820", Loc.F("ui.game.best", "4 820"));
            Assert.AreEqual("Jouer", Loc.T("menu.play"));
            Loc.Use("en");
            Assert.AreEqual("Resume", Loc.T("ui.menu.resume"));
        }

        [Test]
        public void CjkGlyphsExistInFallbackFonts()
        {
            if (!File.Exists(Xcstrings)) Assert.Ignore("Catalogue iOS absent");
            var known = new HashSet<char>();
            void Walk(JToken t)
            {
                if (t is JObject o) { foreach (var p in o) { if (p.Key == "value" && p.Value!.Type == JTokenType.String) foreach (var c in (string)p.Value!) known.Add(c); else Walk(p.Value!); } }
                else if (t is JArray a) foreach (var v in a) Walk(v);
            }
            var cat = JObject.Parse(File.ReadAllText(Xcstrings));
            foreach (var e in (JObject)cat["strings"]!)
                if (e.Value!["localizations"] is JObject locs)
                    foreach (var l in new[] { "ja", "ko", "zh-Hans" })
                        if (locs[l] != null) Walk(locs[l]!);
            var extra = JObject.Parse(File.ReadAllText(Path.Combine(Dir, "strings_unity.json")));
            foreach (var entry in (JObject)extra["strings"]!)
                foreach (var l in new[] { "ja", "ko", "zh-Hans" })
                    foreach (var c in (string)entry.Value![l]!)
                    {
                        // ASCII, ponctuation CJK, pleine chasse et kana sont toujours dans les replis.
                        if (c < 0x7F || (c >= 0x3000 && c < 0x3100) || (c >= 0xFF00 && c < 0xFFF0)) continue;
                        Assert.IsTrue(known.Contains(c), $"{entry.Key} / {l} : « {c} » absent des polices de repli");
                    }
        }
    }
}
