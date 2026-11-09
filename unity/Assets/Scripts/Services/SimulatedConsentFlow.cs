using System;
using System.Collections.Generic;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Services
{
    /// Consentement simulé (étape 7 : UMP + ATT réels) : répond « non
    /// personnalisé » après un court délai réel et garde la trace des appels.
    public sealed class SimulatedConsentFlow : IConsentFlow
    {
        public readonly List<string> Calls = new();
        public float Latency = 0.2f;
        public bool Personalized;

        public void Gather(Action<bool> done)
        {
            Calls.Add("gather");
            Debug.Log("[Consent] recueil simulé du consentement");
            FxTime.AfterRealtime(Latency, () => done?.Invoke(Personalized));
        }

        public void ShowPrivacyOptions()
        {
            Calls.Add("privacy");
            Debug.Log("[Consent] options de confidentialité (simulées)");
        }
    }
}
