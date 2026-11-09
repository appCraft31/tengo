namespace TenGo.Fx
{
    /// Rendu qui suit l'alpha hérité de ses `FxAlpha` parents.
    public interface IFxAlphaTarget
    {
        void OnInheritedAlphaChanged();
    }
}
