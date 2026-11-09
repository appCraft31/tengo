using System;

namespace TenGo.Services
{
    /// Service des duels (DuelService.swift, Firestore à l'étape 7). Les appels
    /// rendent la main par rappel, sur le fil principal ; `onError` reçoit un
    /// message lisible par le joueur.
    public interface IDuelBackend
    {
        /// Identifiant du joueur connecté (`FirebaseService.uid`), `null` hors ligne.
        string Uid { get; }
        /// Le challenger publie son score : le duel est créé et reçoit un code.
        void CreateDuel(long seed, int score, Action<Duel> onDone, Action<string> onError);
        /// L'adversaire publie son score sur le duel `code`.
        void SubmitOpponentScore(string code, int score, Action<Duel> onDone, Action<string> onError);
    }
}
