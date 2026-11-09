using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TenGo.Core;
using TenGo.Platform;
using UnityEngine;
using UnityEngine.TestTools;

namespace TenGo.Tests
{
    /// Magasin persistant (fichier JSON) : types conservés, écriture atomique,
    /// fichier corrompu.
    public class JsonFileSaveStoreTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "tengo-save-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, JsonFileSaveStore.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void RoundTripKeepsEveryType()
        {
            var store = JsonFileSaveStore.Open(_path);
            Assert.AreEqual("none", store.LoadedFrom);
            store.SetInt("i", -7);
            store.SetLong("l", 9_000_000_000L);
            store.SetDouble("d", 815094000.0);
            store.SetDouble("f", 0.1);
            store.SetBool("b", true);
            store.SetString("s", "é \"x\"");
            store.SetStringList("sl", new[] { "a", "b" });
            store.SetIntList("il", new[] { 3, 1 });
            store.SetStringList("empty", new string[0]);
            Assert.IsTrue(store.Dirty);
            Assert.IsTrue(store.Flush());
            Assert.IsFalse(store.Dirty);
            Assert.IsFalse(store.Flush());
            Assert.IsFalse(File.Exists(_path + ".tmp"));

            var back = JsonFileSaveStore.Open(_path);
            Assert.AreEqual("main", back.LoadedFrom);
            Assert.AreEqual(-7, back.GetInt("i"));
            Assert.AreEqual(9_000_000_000L, back.GetLong("l"));
            Assert.AreEqual(815094000.0, back.GetDouble("d"));
            Assert.AreEqual(0.1, back.GetDouble("f"));
            Assert.IsTrue(back.GetBool("b"));
            Assert.AreEqual("é \"x\"", back.GetString("s"));
            CollectionAssert.AreEqual(new[] { "a", "b" }, back.GetStringList("sl"));
            CollectionAssert.AreEqual(new[] { 3, 1 }, back.GetIntList("il"));
            CollectionAssert.IsEmpty(back.GetIntList("empty"));
            CollectionAssert.IsEmpty(back.GetStringList("empty"));
            Assert.IsNull(back.GetIntList("sl"));
        }

        [Test]
        public void ImportedDataSurvivesAReload()
        {
            var store = JsonFileSaveStore.Open(_path);
            LegacyMigration.Run(store, LegacyFixtures.AndroidSource("complete", LegacyFixtures.Paris), "4.0.0");
            store.Flush();
            var back = JsonFileSaveStore.Open(_path);
            CollectionAssert.AreEqual(store.Memory.Values.Keys.OrderBy(k => k), back.Memory.Values.Keys.OrderBy(k => k));
            Assert.AreEqual(SaveStoreJson.ToJson(store.Memory), SaveStoreJson.ToJson(back.Memory));
            Assert.IsTrue(LegacyMigration.IsDone(back));
            Assert.AreEqual(LegacyMigrationStatus.AlreadyDone, LegacyMigration.Run(back, LegacyFixtures.AndroidSource("complete", LegacyFixtures.Paris), "4.0.0").Status);
        }

        [Test]
        public void SecondFlushKeepsPreviousVersionAsBackup()
        {
            var store = JsonFileSaveStore.Open(_path);
            store.SetInt("n", 1);
            store.Flush();
            store.SetInt("n", 2);
            store.Flush();
            Assert.AreEqual(2, JsonFileSaveStore.Open(_path).GetInt("n"));
            Assert.AreEqual(1, SaveStoreJson.FromJson(File.ReadAllText(_path + ".bak")).GetInt("n"));
        }

        [Test]
        public void CorruptFileFallsBackToBackup()
        {
            var store = JsonFileSaveStore.Open(_path);
            store.SetInt("n", 1);
            store.Flush();
            store.SetInt("n", 2);
            store.Flush();
            File.WriteAllText(_path, "{\"format\":1,\"values\":{\"n\":"); // écriture interrompue
            LogAssert.Expect(LogType.Warning, new Regex("illisible"));
            var back = JsonFileSaveStore.Open(_path);
            Assert.AreEqual("bak", back.LoadedFrom);
            Assert.AreEqual(1, back.GetInt("n"));
            Assert.AreEqual(1, Directory.GetFiles(_dir, JsonFileSaveStore.FileName + ".corrupt-*").Length);
        }

        [Test]
        public void CorruptFileWithoutBackupStartsEmpty()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllBytes(_path, new byte[] { 0xFF, 0x00, 0x7B });
            LogAssert.Expect(LogType.Warning, new Regex("illisible"));
            var store = JsonFileSaveStore.Open(_path);
            Assert.AreEqual("corrupt", store.LoadedFrom);
            Assert.IsFalse(store.Has("n"));
            Assert.IsFalse(File.Exists(_path));
            // Repartir à vide relance la reprise des données natives.
            Assert.IsFalse(LegacyMigration.IsDone(store));
            store.SetInt("n", 3);
            Assert.IsTrue(store.Flush());
            Assert.AreEqual(3, JsonFileSaveStore.Open(_path).GetInt("n"));
        }

        [Test]
        public void UnknownFormatIsRejected()
        {
            Assert.Throws<Newtonsoft.Json.JsonException>(() => SaveStoreJson.FromJson("{\"format\":2,\"values\":{}}"));
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => SaveStoreJson.FromJson("[]"));
        }
    }
}
