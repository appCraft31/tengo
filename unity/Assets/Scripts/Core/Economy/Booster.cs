#nullable enable
namespace TenGo.Core
{
    /// Boosters consommables (BoosterManager.swift).
    public enum Booster
    {
        /// Indice : surligne un chemin valide (PathSolver).
        Hint,
        /// Mélange : régénère les valeurs de la grille.
        Shuffle,
        /// Marteau : supprime une bulle au choix, puis la gravité s'applique.
        Hammer,
    }
}
