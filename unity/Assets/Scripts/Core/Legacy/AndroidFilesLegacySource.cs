#nullable enable
using System;

namespace TenGo.Core
{
    /// Source Android : lit (sans jamais les modifier) les fichiers internes
    /// de l'app 3.3.0. Les chemins viennent de JNI sur l'appareil
    /// (`AndroidLegacyPaths`), d'un dossier de fixtures dans les tests.
    public sealed class AndroidFilesLegacySource : ILegacySource
    {
        private readonly string _filesDir;
        private readonly string _sharedPrefsDir;
        private readonly TimeZoneInfo _zone;

        public AndroidFilesLegacySource(string filesDir, string sharedPrefsDir, TimeZoneInfo zone)
        {
            _filesDir = filesDir;
            _sharedPrefsDir = sharedPrefsDir;
            _zone = zone;
        }

        public string Name => "android";

        public LegacyConversion? Read()
        {
            AndroidLegacyData? data = AndroidLegacyData.ReadFiles(_filesDir, _sharedPrefsDir);
            return data == null || data.IsEmpty ? null : AndroidLegacyImporter.Convert(data, _zone);
        }
    }
}
