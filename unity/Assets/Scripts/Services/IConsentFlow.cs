using System;

namespace TenGo.Services
{
    /// Recueil du consentement (ConsentManager.swift : UMP puis ATT), avant le
    /// démarrage des pubs. `done(personalized)` rend la main sur le fil principal.
    public interface IConsentFlow
    {
        void Gather(Action<bool> done);
        /// Formulaire des options de confidentialité (réglages).
        void ShowPrivacyOptions();
    }
}
