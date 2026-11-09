#nullable enable
namespace TenGo.Core
{
    /// Définition figée d'une mission ; seul l'état est persisté.
    public sealed class MissionDefinition
    {
        public MissionDefinition(string id, MissionKind kind, int param, int target, int coinReward, bool isSuper, string titleKey)
        {
            Id = id;
            Kind = kind;
            Param = param;
            Target = target;
            CoinReward = coinReward;
            IsSuper = isSuper;
            TitleKey = titleKey;
        }

        public string Id { get; }
        public MissionKind Kind { get; }
        /// Seuil de chaîne pour `ChainAtLeast` ; ignoré sinon.
        public int Param { get; }
        public int Target { get; }
        public int CoinReward { get; }
        public bool IsSuper { get; }
        /// Clé de texte : `ChainAtLeast` → (target, param), les autres → (target).
        public string TitleKey { get; }
    }
}
