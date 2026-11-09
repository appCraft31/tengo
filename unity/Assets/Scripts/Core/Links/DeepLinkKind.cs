#nullable enable
namespace TenGo.Core
{
    /// Destination d'un lien entrant (DeepLink.swift).
    public enum DeepLinkKind
    {
        /// Lien non reconnu.
        None,
        /// `tengo://daily` : défi du jour.
        Daily,
        /// `tengo://duel/CODE` : écran Duel, code prêt à être accepté.
        Duel,
        /// `tengo://progress` ou `tengo://event` : onglet Progression.
        Progress,
    }
}
