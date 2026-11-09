using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Décodage des formats natifs : DataStore Preferences (protobuf),
    /// SharedPreferences (XML), sortie du greffon iOS (JSON typé).
    public class LegacyDecoderTests
    {
        [Test]
        public void DecodesRealEmulatorDataStore()
        {
            var prefs = LegacyFixtures.ReadAndroid("emulator").Preferences;
            Assert.AreEqual(LegacyValueKind.String, prefs["tengo_high_scores"].Kind);
            Assert.AreEqual("4180,3640,3120,2870,2440,2050,1890,1620,1410,1180", prefs["tengo_high_scores"].String);
            Assert.AreEqual(LegacyValueKind.Bool, prefs["hasSeenTutorial"].Kind);
            Assert.IsTrue(prefs["hasSeenTutorial"].Bool);
            Assert.AreEqual(LegacyValueKind.Int, prefs["tenGO_totalXP"].Kind);
            Assert.GreaterOrEqual(prefs["tenGO_totalXP"].Long, 4820);
            Assert.AreEqual(LegacyValueKind.StringSet, prefs["tenGO_ownedThemes"].Kind);
            CollectionAssert.AreEquivalent(new[] { "forest", "ocean" }, prefs["tenGO_ownedThemes"].Strings);
            Assert.AreEqual(3, prefs["tenGO_booster_hint"].Long);
            Assert.AreEqual(7, Enumerable.Range(1, 7).Count(i => prefs.ContainsKey("tenGO_puzzleStars_1_" + i)));
            Assert.AreEqual(LegacyValueKind.String, prefs["tenGO_streakLastPlayed"].Kind);
            StringAssert.StartsWith("{\"records\":[", prefs["tenGO_mirrorData"].String);
            StringAssert.Contains("\"mode\":\"normal\"", prefs["tengo_saved_game"].String);
            CollectionAssert.IsSupersetOf(prefs["tenGO_achievementsUnlocked"].Strings, new[] { "chain5", "streak14", "dailyTotal30" });
        }

        [Test]
        public void DecodesEveryProtobufValueCase()
        {
            var prefs = LegacyFixtures.ReadAndroid("complete").Preferences;
            Assert.AreEqual(LegacyValueKind.Int, prefs["zz_negativeInt"].Kind);
            Assert.AreEqual(-42, prefs["zz_negativeInt"].Long);
            Assert.AreEqual(LegacyValueKind.Long, prefs["zz_long"].Kind);
            Assert.AreEqual(-9_000_000_000L, prefs["zz_long"].Long);
            Assert.AreEqual(LegacyValueKind.Float, prefs["zz_float"].Kind);
            Assert.AreEqual(1.5, prefs["zz_float"].Double);
            Assert.AreEqual(LegacyValueKind.Double, prefs["zz_double"].Kind);
            Assert.AreEqual(-2.25, prefs["zz_double"].Double);
            Assert.AreEqual(LegacyValueKind.Bytes, prefs["zz_bytes"].Kind);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 255 }, prefs["zz_bytes"].Bytes);
            Assert.AreEqual("é€😀", prefs["zz_unicode"].String);
            Assert.AreEqual(1234, prefs["tenGO_coins"].Long);
            Assert.IsFalse(prefs["tenGO_hapticsEnabled"].Bool);
            CollectionAssert.AreEquivalent(new[] { "chain5", "perfect1", "streak7" }, prefs["tenGO_achievementsUnlocked"].Strings);
        }

        [Test]
        public void TruncatedOrGarbageProtobufThrows()
        {
            byte[] data = File.ReadAllBytes(Path.Combine(LegacyFixtures.FilesDir("complete"), AndroidLegacyData.DataStoreRelativePath));
            Assert.Throws<FormatException>(() => DataStorePreferencesDecoder.Decode(data.Take(data.Length - 5).ToArray()));
            Assert.Throws<FormatException>(() => DataStorePreferencesDecoder.Decode(new byte[] { 0x0A, 0xFF, 0xFF }));
            Assert.AreEqual(0, DataStorePreferencesDecoder.Decode(new byte[0]).Count);
        }

        [Test]
        public void DecodesSharedPreferencesXml()
        {
            var firstRun = LegacyFixtures.ReadAndroid("emulator").FirstRun;
            Assert.AreEqual("done", firstRun[SaveKeys.FirstRunState].String);
            Assert.AreEqual(LegacyValueKind.Long, firstRun[SaveKeys.FirstOpenAt].Kind);
            Assert.AreEqual(1794227761614L, firstRun[SaveKeys.FirstOpenAt].Long);
            Assert.IsTrue(firstRun[SaveKeys.FirstGameStartLogged].Bool);

            var ads = LegacyFixtures.ReadAndroid("complete").Ads;
            Assert.AreEqual(LegacyValueKind.Int, ads[SaveKeys.InterstitialsTodayDay].Kind);
            Assert.AreEqual(20261109, ads[SaveKeys.InterstitialsTodayDay].Long);
            Assert.AreEqual(0.25, ads["zz_float"].Double);
            Assert.AreEqual("<a & \"b\"> é", ads["zz_entities"].String);
            CollectionAssert.AreEqual(new[] { "x", "y & z" }, ads["zz_set"].Strings);
            Assert.IsFalse(ads.ContainsKey("zz_null"));
        }

        [Test]
        public void BrokenXmlThrows()
        {
            Assert.Throws<FormatException>(() => SharedPreferencesXmlDecoder.Decode("<map><int name=\"a\" value=\"1\""));
            Assert.Throws<FormatException>(() => SharedPreferencesXmlDecoder.Decode("<prefs />"));
            Assert.AreEqual(0, SharedPreferencesXmlDecoder.Decode("<map />").Count);
        }

        [Test]
        public void MissingAndroidFilesMeanNoData()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tengo-legacy-" + Guid.NewGuid().ToString("N"));
            Assert.IsNull(AndroidLegacyData.ReadFiles(Path.Combine(dir, "files"), Path.Combine(dir, "shared_prefs")));
        }

        [Test]
        public void ParsesIosPluginJson()
        {
            var lived = IosLegacyImporter.ParsePluginJson(LegacyFixtures.IosJson("lived"));
            Assert.AreEqual(LegacyValueKind.Date, lived[SaveKeys.InterstitialLastShownAt].Kind);
            Assert.AreEqual(815137457.215448, lived[SaveKeys.InterstitialLastShownAt].Double, 1e-6);
            Assert.AreEqual(LegacyValueKind.Double, lived[SaveKeys.StreakLastPlayed].Kind);
            Assert.AreEqual(LegacyValueKind.String, lived[SaveKeys.MissionsProgress].Kind);
            Assert.AreEqual(LegacyValueKind.IntList, lived[SaveKeys.HighScores].Kind);
            Assert.AreEqual("20261031", lived[SaveKeys.InterstitialsTodayDay].String);

            var seeded = IosLegacyImporter.ParsePluginJson(LegacyFixtures.IosJson("seeded"));
            Assert.AreEqual(LegacyValueKind.Bool, seeded[SaveKeys.HasSeenTutorial].Kind);
            CollectionAssert.AreEqual(new long[] { 4820, 4310, 3980, 3720, 3450 }, seeded[SaveKeys.HighScores].Longs);

            var odd = IosLegacyImporter.ParsePluginJson(
                "{\"a\":{\"t\":\"bytes\",\"v\":\"AAH/\"},\"b\":{\"t\":\"array\",\"v\":[\"x\",\"y\"]},\"c\":{\"t\":\"unsupported\",\"v\":\"NSDictionary\"},\"d\":{\"t\":\"array\",\"v\":[1,\"x\"]}}");
            CollectionAssert.AreEqual(new byte[] { 0, 1, 255 }, odd["a"].Bytes);
            Assert.AreEqual(LegacyValueKind.StringList, odd["b"].Kind);
            Assert.IsFalse(odd.ContainsKey("c"));
            Assert.IsFalse(odd.ContainsKey("d"));
            Assert.Throws<FormatException>(() => IosLegacyImporter.ParsePluginJson("!serialisation"));
        }

        [Test]
        public void IosRequestListsSchemaKeysAndPrefixes()
        {
            string request = IosLegacyImporter.RequestJson();
            StringAssert.Contains("\"tenGO_coins\"", request);
            StringAssert.Contains("\"AdMob.interstitialLastShownAt\"", request);
            StringAssert.Contains("\"tenGO_booster_\"", request);
            StringAssert.Contains("\"tenGO_puzzleStars_\"", request);
            StringAssert.DoesNotContain("IABTCF", request);
        }
    }
}
