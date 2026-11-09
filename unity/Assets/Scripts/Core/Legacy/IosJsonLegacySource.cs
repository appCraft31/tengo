#nullable enable
using System;

namespace TenGo.Core
{
    /// Source iOS : JSON typé renvoyé par le greffon `TenGoLegacy.mm` (ou une
    /// fixture). `readJson` reçoit la demande (`IosLegacyImporter.RequestJson`)
    /// et renvoie la réponse, ou null si le greffon est indisponible.
    public sealed class IosJsonLegacySource : ILegacySource
    {
        private readonly Func<string, string?> _readJson;

        public IosJsonLegacySource(Func<string, string?> readJson)
        {
            _readJson = readJson;
        }

        public string Name => "ios";

        public LegacyConversion? Read()
        {
            string? json = _readJson(IosLegacyImporter.RequestJson());
            if (json == null) return null;
            LegacyConversion conversion = IosLegacyImporter.ConvertPluginJson(json);
            return conversion.IsEmpty ? null : conversion;
        }
    }
}
