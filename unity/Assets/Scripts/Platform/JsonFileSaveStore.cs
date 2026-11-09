using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Platform
{
    /// `ISaveStore` persistant : un fichier JSON (`SaveStoreJson`) dans
    /// `Application.persistentDataPath`, gardé en mémoire et réécrit par `Flush()`.
    ///
    /// - Écriture atomique : fichier temporaire `.tmp` vidé sur disque, puis
    ///   remplacement (`File.Replace`, l'ancienne version passe en `.bak`).
    /// - Lecture tolérante : fichier principal, sinon `.bak`, sinon `.tmp` ; un
    ///   fichier illisible est mis de côté (`.corrupt-<horodatage>`) et l'on
    ///   repart à vide, ce qui relance la reprise des données natives.
    /// - Ne passe pas par `PlayerPrefs` : sur iOS ils vivent dans les mêmes
    ///   `standardUserDefaults` que les données de l'app native.
    ///
    /// Le jeu appelle `Flush()` aux points de sauvegarde (fin de partie, mise
    /// en arrière-plan, fermeture) ; `Dirty` dit s'il y a quelque chose à écrire.
    public sealed class JsonFileSaveStore : ISaveStore
    {
        public const string FileName = "tengo_save.json";

        private readonly MemorySaveStore _mem;
        private int _revision;
        private int _savedRevision;

        private JsonFileSaveStore(string path, MemorySaveStore mem, string loadedFrom)
        {
            Path = path;
            _mem = mem;
            LoadedFrom = loadedFrom;
        }

        public string Path { get; }

        /// Fichier relu à l'ouverture (`main`, `bak`, `tmp`, `none`, `corrupt`).
        public string LoadedFrom { get; }

        public bool Dirty => _revision != _savedRevision;

        /// Contenu en mémoire (diagnostic).
        public MemorySaveStore Memory => _mem;

        public static JsonFileSaveStore OpenDefault() =>
            Open(System.IO.Path.Combine(Application.persistentDataPath, FileName));

        public static JsonFileSaveStore Open(string path)
        {
            var candidates = new[] { (path, "main"), (path + ".bak", "bak"), (path + ".tmp", "tmp") };
            bool sawCorrupt = false;
            foreach (var (candidate, label) in candidates)
            {
                string json = TryRead(candidate);
                if (json == null) continue;
                try
                {
                    return new JsonFileSaveStore(path, SaveStoreJson.FromJson(json), label);
                }
                catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is FormatException || e is InvalidCastException || e is OverflowException)
                {
                    sawCorrupt = true;
                    Debug.LogWarning($"[Save] {System.IO.Path.GetFileName(candidate)} illisible : {e.GetType().Name}");
                    if (label == "main") SetAside(candidate);
                }
            }
            return new JsonFileSaveStore(path, new MemorySaveStore(), sawCorrupt ? "corrupt" : "none");
        }

        private static string TryRead(string p)
        {
            try
            {
                return File.Exists(p) ? File.ReadAllText(p, Encoding.UTF8) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] lecture impossible de {System.IO.Path.GetFileName(p)} : {e.Message}");
                return null;
            }
        }

        /// Garde une copie du fichier corrompu (analyse), sans bloquer le jeu.
        private static void SetAside(string p)
        {
            try
            {
                File.Move(p, p + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] mise de côté impossible : {e.Message}");
            }
        }

        /// Écrit le fichier s'il a changé. Renvoie true si une écriture a eu lieu.
        public bool Flush()
        {
            if (!Dirty) return false;
            int revision = _revision;
            string tmp = Path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                byte[] bytes = new UTF8Encoding(false).GetBytes(SaveStoreJson.ToJson(_mem));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                if (File.Exists(Path))
                {
                    try
                    {
                        File.Replace(tmp, Path, Path + ".bak");
                    }
                    catch (Exception)
                    {
                        // Certains systèmes de fichiers refusent Replace : copie puis suppression.
                        File.Copy(Path, Path + ".bak", true);
                        File.Copy(tmp, Path, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, Path);
                }
                _savedRevision = revision;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] écriture impossible : {e.Message}");
                return false;
            }
        }

        private void Touch() => _revision++;

        public bool Has(string key) => _mem.Has(key);
        public void Remove(string key) { if (_mem.Has(key)) { _mem.Remove(key); Touch(); } }

        public int GetInt(string key, int defaultValue = 0) => _mem.GetInt(key, defaultValue);
        public void SetInt(string key, int value) { _mem.SetInt(key, value); Touch(); }

        public long GetLong(string key, long defaultValue = 0) => _mem.GetLong(key, defaultValue);
        public void SetLong(string key, long value) { _mem.SetLong(key, value); Touch(); }

        public double GetDouble(string key, double defaultValue = 0) => _mem.GetDouble(key, defaultValue);
        public void SetDouble(string key, double value) { _mem.SetDouble(key, value); Touch(); }

        public bool GetBool(string key, bool defaultValue = false) => _mem.GetBool(key, defaultValue);
        public void SetBool(string key, bool value) { _mem.SetBool(key, value); Touch(); }

        public string GetString(string key) => _mem.GetString(key);
        public void SetString(string key, string value) { _mem.SetString(key, value); Touch(); }

        public IReadOnlyList<string> GetStringList(string key) => _mem.GetStringList(key);
        public void SetStringList(string key, IEnumerable<string> values) { _mem.SetStringList(key, values); Touch(); }

        public IReadOnlyList<int> GetIntList(string key) => _mem.GetIntList(key);
        public void SetIntList(string key, IEnumerable<int> values) { _mem.SetIntList(key, values); Touch(); }
    }
}
