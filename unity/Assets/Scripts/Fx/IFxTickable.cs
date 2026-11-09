namespace TenGo.Fx
{
    /// Objet avancé à chaque pas de `FxTime` (particules, nuit…).
    public interface IFxTickable
    {
        /// Retourne faux pour être retiré de la liste.
        bool FxTick(float dt);
    }
}
