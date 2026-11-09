#if TENGO_FIREBASE
using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Analytics;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Services
{
    /// Firebase Analytics (symbole TENGO_FIREBASE) : les événements
    /// d'`AnalyticsCatalog`, mis en attente (100 au plus) tant que Firebase
    /// n'est pas prêt, comme dans Binero.
    ///
    /// Canal de build (propriété `build_channel`) : sur iOS, les builds de
    /// développement et TestFlight passent en mode debug (DebugView, exclues des
    /// rapports) ; sur Android, une build de développement n'envoie rien.
    public sealed class FirebaseAnalyticsService : EventAnalytics
    {
        const int MaxPending = 100;
        bool _ready, _failed;
        readonly Queue<AnalyticsEvent> _pending = new();
        readonly Dictionary<string, string> _pendingProperties = new();
        bool? _pendingConsent;

        public FirebaseAnalyticsService()
        {
            string channel = NativeBridge.BuildChannel;
#if UNITY_IOS
            NativeBridge.SetFirebaseDebug(channel != "production");
#endif
            FirebaseBootstrap.WhenReady(ok =>
            {
                if (!ok) { _failed = true; _pending.Clear(); _pendingProperties.Clear(); return; }
                _ready = true;
#if UNITY_IOS
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
#else
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(channel == "production");
#endif
                SetUserProperty(AnalyticsCatalog.BuildChannelProperty, channel);
                if (_pendingConsent is bool personalized) ApplyConsent(personalized);
                foreach (var kv in _pendingProperties) SetUserProperty(kv.Key, kv.Value);
                _pendingProperties.Clear();
                while (_pending.Count > 0) Send(_pending.Dequeue());
            });
        }

        protected override void Send(AnalyticsEvent e)
        {
            if (Debug.isDebugBuild) Debug.Log("[Analytics] " + e);
            if (_failed) return;
            if (!_ready)
            {
                if (_pending.Count < MaxPending) _pending.Enqueue(e);
                return;
            }
            try { FirebaseAnalytics.LogEvent(e.Name, e.Parameters.Select(ToParameter).ToArray()); }
            catch (Exception ex) { Debug.LogWarning("[Analytics] " + e.Name + " : " + ex.Message); }
        }

        static Parameter ToParameter(KeyValuePair<string, object> p) => p.Value switch
        {
            long l => new Parameter(p.Key, l),
            int i => new Parameter(p.Key, (long)i),
            double d => new Parameter(p.Key, d),
            IReadOnlyList<IReadOnlyDictionary<string, object>> items =>
                new Parameter(p.Key, items.Select(i => (IDictionary<string, object>)i.ToDictionary(kv => kv.Key, kv => kv.Value)).ToList()),
            _ => new Parameter(p.Key, p.Value?.ToString() ?? ""),
        };

        public override void SetUserProperty(string name, string value)
        {
            if (_failed) return;
            if (!_ready) { _pendingProperties[name] = value; return; }
            try { FirebaseAnalytics.SetUserProperty(name, value); }
            catch (Exception ex) { Debug.LogWarning("[Analytics] propriété " + name + " : " + ex.Message); }
        }

        /// Consent mode (ConsentManager.swift) : stockage analytique accordé,
        /// signaux publicitaires accordés seulement en mode personnalisé
        /// (défauts refusés par GOOGLE_ANALYTICS_DEFAULT_ALLOW_* dans l'Info.plist).
        public void ApplyConsent(bool personalized)
        {
            if (_failed) return;
            if (!_ready) { _pendingConsent = personalized; return; }
            var ad = personalized ? ConsentStatus.Granted : ConsentStatus.Denied;
            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                [ConsentType.AnalyticsStorage] = ConsentStatus.Granted,
                [ConsentType.AdStorage] = ad,
                [ConsentType.AdUserData] = ad,
                [ConsentType.AdPersonalization] = ad,
            });
        }
    }
}
#endif
