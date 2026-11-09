using System;
using UnityEngine;

namespace TenGo.App
{
    /// Surcouche modale posée sur un écran (`SettingsOverlay` d'iOS) : elle
    /// capte les touchers tant qu'elle est ouverte.
    public interface IGameOverlay
    {
        bool IsOpen { get; }
        void HandleTouch(Vector2 pt);
        void Close();
    }

    /// Fabrique des surcouches : l'étape 6 y branche les vrais réglages.
    public interface IOverlayFactory
    {
        /// Réglages (son, vibrations, revoir le tutoriel…) posés sur `parent`.
        IGameOverlay Settings(AppRoot app, Transform parent, Action replayTutorial);
    }
}
