#nullable enable
using System;

namespace TenGo.Core
{
    /// Cadence des interstitielles, partie pure d'InterstitialAdManager.swift
    /// (espacement TEN-3) :
    /// - 150 s minimum entre deux plein écran, vidéo récompensée comprise ;
    /// - aucun plein écran avant la 2e partie terminée de la session (3e pour
    ///   la fin de partie automatique), une de plus pendant la première session
    ///   de l'installation (TEN-4) ;
    /// - fin de partie automatique : 1 sur 4.
    /// Pas de plafond journalier d'interstitielles sur iOS : le compteur du jour
    /// ne sert qu'à l'offre « sans pub ». Le chargement des annonces relève du
    /// service AdMob (étape 7).
    public sealed class InterstitialPolicy
    {
        public const double CooldownSeconds = 150;

        private readonly ISaveStore _store;
        private readonly IClock _clock;
        private int _gameOverCount;

        public InterstitialPolicy(ISaveStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        /// Parties terminées depuis le lancement.
        public int GamesCompletedThisSession { get; private set; }

        /// Offres « sans pub » de fin de partie montrées depuis le lancement.
        public int AdFreeOffersShownThisSession { get; private set; }

        /// Dernier plein écran (date Swift `distantPast` si jamais : très ancien).
        public DateTimeOffset LastShownAt =>
            _store.Has(SaveKeys.InterstitialLastShownAt)
                ? SwiftDate.FromReferenceSeconds(_store.GetDouble(SaveKeys.InterstitialLastShownAt))
                : DateTimeOffset.MinValue;

        private void SetLastShownNow() =>
            _store.SetDouble(SaveKeys.InterstitialLastShownAt, SwiftDate.ToReferenceSeconds(_clock.Now));

        /// À chaque fin de partie (gagnée ou perdue).
        public void MarkGameCompleted() => GamesCompletedThisSession += 1;

        /// Un plein écran vient d'être vu ailleurs (vidéo récompensée) : relance le délai.
        public void NoteFullScreenAdShown() => SetLastShownNow();

        /// Toutes les conditions du déclencheur sont-elles réunies ? Le compteur
        /// « 1 sur 4 » de la fin automatique n'avance que si les autres gardes passent.
        public bool ShouldShow(AdTrigger trigger, bool isAdFree, bool adReady, bool isFirstSession)
        {
            if (isAdFree || !adReady) return false;
            if ((_clock.Now - LastShownAt).TotalSeconds < CooldownSeconds) return false;
            int baseMinimum = trigger == AdTrigger.GameOverAuto ? 3 : 2;
            int firstSessionMinimum = trigger == AdTrigger.GameOverAuto ? 4 : 3;
            int minimumGames = isFirstSession ? firstSessionMinimum : baseMinimum;
            if (GamesCompletedThisSession < minimumGames) return false;
            if (trigger != AdTrigger.GameOverAuto) return true;
            _gameOverCount += 1;
            return _gameOverCount % 4 == 0;
        }

        /// L'interstitielle est présentée : le délai repart.
        public void MarkPresented() => SetLastShownNow();

        /// L'interstitielle a été fermée : elle entre dans la série et dans le
        /// compte du jour (une pub qui échoue à s'afficher n'y entre pas).
        public void RecordShown()
        {
            _store.SetInt(SaveKeys.InterstitialsSinceNoAdsOffer, ShownSinceNoAdsOffer + 1);
            int today = ShownToday + 1;
            _store.SetString(SaveKeys.InterstitialsTodayDay, TodayString());
            _store.SetInt(SaveKeys.InterstitialsTodayCount, today);
        }

        /// Interstitielles vues depuis la dernière offre « sans pub », toutes sessions confondues.
        public int ShownSinceNoAdsOffer => _store.GetInt(SaveKeys.InterstitialsSinceNoAdsOffer);

        /// Interstitielles vues aujourd'hui (jour local).
        public int ShownToday =>
            _store.GetString(SaveKeys.InterstitialsTodayDay) == TodayString() ? _store.GetInt(SaveKeys.InterstitialsTodayCount) : 0;

        private string TodayString() => DayKeys.Calendar(_clock).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);

        /// L'offre « sans pub » de fin de partie peut-elle être montrée ? (série
        /// d'au moins 2 interstitielles, au plus 2 offres par session, prix connu).
        public bool CanOfferAdFree(bool isAdFree, bool priceKnown) =>
            !isAdFree && priceKnown
            && ShownSinceNoAdsOffer >= StoreCatalog.AdFreeOfferAfterAds
            && AdFreeOffersShownThisSession < StoreCatalog.AdFreeOffersPerSession;

        /// L'offre vient d'être montrée : une nouvelle série commence.
        public void MarkAdFreeOfferShown()
        {
            AdFreeOffersShownThisSession += 1;
            _store.SetInt(SaveKeys.InterstitialsSinceNoAdsOffer, 0);
        }
    }
}
