using System;
using System.Collections.Generic;

namespace TenGo.Services
{
    /// Service des duels (DuelService.swift ; Firestore : `FirestoreDuelBackend`).
    /// Les appels rendent la main par rappel, sur le fil principal ; `onError`
    /// reçoit un message lisible par le joueur.
    public interface IDuelBackend
    {
        /// Identifiant du joueur connecté (`FirebaseService.uid`), `null` tant
        /// que personne n'est connecté (la connexion anonyme est paresseuse).
        string Uid { get; }
        /// Le challenger publie son score : le duel est créé et reçoit un code.
        void CreateDuel(long seed, int score, Action<Duel> onDone, Action<string> onError);
        /// Récupère un duel par son code (introuvable → `duel.error_not_found`).
        void Fetch(string code, Action<Duel> onDone, Action<string> onError);
        /// L'adversaire publie son score sur le duel `code`, après les refus
        /// « expiré / déjà joué / ton propre duel » (`DuelRules.JoinRefusal`).
        void SubmitOpponentScore(string code, int score, Action<Duel> onDone, Action<string> onError);
        /// Duels de l'historique local relus sur le serveur, du plus récent au
        /// plus ancien ; les introuvables sont omis.
        void MyDuels(Action<IReadOnlyList<Duel>> onDone);
    }
}
