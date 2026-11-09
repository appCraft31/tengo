using UnityEngine;

namespace TenGo.Fx
{
    /// Avance `FxTime` à chaque image en mode jeu (créé à la demande).
    [AddComponentMenu("")]
    public sealed class FxDriver : MonoBehaviour
    {
        void Update() => FxTime.Step(Mathf.Min(Time.unscaledDeltaTime, FxTime.MaxStep));
        void OnDestroy() => FxTime.DriverDestroyed();
    }
}
