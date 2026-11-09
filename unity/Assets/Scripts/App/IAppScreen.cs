using UnityEngine;

namespace TenGo.App
{
    /// Un écran (`SKScene` d'iOS) : une racine sous `AppRoot` et ses réactions.
    /// Positions en points de scène, origine au centre (`anchorPoint` 0,5).
    public interface IAppScreen
    {
        Transform Root { get; }
        /// Pas de jeu (`update(_:)`), `dt` en secondes réelles.
        void Tick(float dt);
        void PointerDown(Vector2 pt);
        void PointerMove(Vector2 pt);
        void PointerUp(Vector2 pt);
        void PointerCancel();
        /// Bouton retour d'Android (Échap dans l'éditeur).
        void Back();
        /// L'application passe en arrière-plan.
        void AppPaused();
        /// La géométrie de l'écran a changé (rotation, fenêtre).
        void GeometryChanged(SceneGeometry g);
        void Dispose();
    }
}
