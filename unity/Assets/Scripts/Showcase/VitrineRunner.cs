using TenGo.Fx;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Showcase
{
    /// Lance la vitrine au démarrage de la scène de démonstration (GlowDemo).
    public sealed class VitrineRunner : MonoBehaviour
    {
        public Vitrine.Page page = Vitrine.Page.Grid;
        public string themeId = "default";

        void Start()
        {
            Look.Apply(themeId, "classic", "classic");
            Vitrine.Build(page, transform, Camera.main);
        }
    }
}
