#nullable enable
namespace TenGo.Core
{
    /// Cadeau de bienvenue de la refonte 4.0 : +50 pièces et 2 indices, versés
    /// UNE seule fois par installation (clé `tenGO_welcomeGiftPaid`).
    ///
    /// Règle : versé au premier passage du tutoriel jouable, qu'il soit mené
    /// au bout ou passé (« Passer » ne pénalise personne), au premier lancement
    /// comme depuis « Revoir le tutoriel ». Un joueur repris d'une app native
    /// (3.x) n'a pas la clé : il le reçoit donc au premier tutoriel jouable
    /// qu'il ouvre. Ensuite le tutoriel se rejoue sans rien redonner.
    public sealed class WelcomeGift
    {
        public const int Coins = 50;
        public const int Hints = 2;

        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;

        public WelcomeGift(ISaveStore store, EconomyRepository economy)
        {
            _store = store;
            _economy = economy;
        }

        public bool IsPaid => _store.GetBool(SaveKeys.WelcomeGiftPaid);

        /// Verse le cadeau s'il ne l'a jamais été ; vrai s'il vient de l'être.
        public bool PayIfNeeded()
        {
            if (IsPaid) return false;
            // La clé d'abord : un second appel (double tap, rejeu) ne paie jamais deux fois.
            _store.SetBool(SaveKeys.WelcomeGiftPaid, true);
            _economy.AddCoins(Coins);
            _economy.GrantBooster(Booster.Hint, Hints);
            return true;
        }
    }
}
