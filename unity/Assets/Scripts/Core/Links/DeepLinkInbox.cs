#nullable enable
namespace TenGo.Core
{
    /// Liens reçus mais pas encore traités (`pendingDaily`, `pendingDuelCode`,
    /// `pendingProgress` de DeepLink.swift) : un lien arrivé au lancement à
    /// froid attend que le premier écran soit affiché.
    public sealed class DeepLinkInbox
    {
        public bool PendingDaily { get; private set; }
        public bool PendingProgress { get; private set; }
        public string? PendingDuelCode { get; private set; }

        public bool HasPending => PendingDaily || PendingProgress || PendingDuelCode != null;

        /// Analyse et met en attente ; vrai si le lien est reconnu.
        public bool Receive(string? url)
        {
            var link = DeepLink.Parse(url);
            switch (link.Kind)
            {
                case DeepLinkKind.Daily: PendingDaily = true; return true;
                case DeepLinkKind.Progress: PendingProgress = true; return true;
                case DeepLinkKind.Duel: PendingDuelCode = link.DuelCode; return true;
                default: return false;
            }
        }

        public bool ConsumeDaily() { bool v = PendingDaily; PendingDaily = false; return v; }
        public bool ConsumeProgress() { bool v = PendingProgress; PendingProgress = false; return v; }
        public string? ConsumeDuelCode() { var v = PendingDuelCode; PendingDuelCode = null; return v; }
    }
}
