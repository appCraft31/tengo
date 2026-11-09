#if TENGO_ADMOB
using System;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace TenGo.Services
{
    /// Consentement réel, comme les apps natives 3.3.0 :
    /// - iOS (ConsentManager.swift) : ATT seul, sans formulaire UMP. Seul
    ///   « autorisé » donne des pubs personnalisées ; sinon non personnalisées
    ///   (`publisherPrivacyPersonalizationState = disabled` + `npa=1`). Le
    ///   formulaire UMP présenté après un refus ATT a valu le rejet 5.1.1(iv) de
    ///   la 2.1 : il a été retiré sur décision de l'utilisateur (2.1.1).
    ///   « Options de confidentialité » ouvre les réglages de l'app (bascule ATT).
    /// - Android (ConsentManager.kt) : UMP (`requestConsentInfoUpdate` puis
    ///   `loadAndShowConsentFormIfRequired`), sans jamais bloquer le jeu ;
    ///   « Options de confidentialité » rouvre le formulaire UMP.
    /// `onOutcome(personnalisé)` aligne le consentement Firebase (iOS).
    public sealed class NativeConsentFlow : IConsentFlow
    {
        readonly Action<bool> _onOutcome;

        public NativeConsentFlow(Action<bool> onOutcome) { _onOutcome = onOutcome; }

        public void Gather(Action<bool> done)
        {
#if UNITY_IOS
            NativeBridge.RequestTracking(authorized =>
            {
                Debug.Log("[Consent] ATT " + (authorized ? "autorisé" : "refusé"));
                _onOutcome?.Invoke(authorized);
                done?.Invoke(authorized);
            });
#else
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null)
                {
                    MainThread.Run(() =>
                    {
                        Debug.Log("[Consent] UMP indisponible : " + updateError.Message);
                        done?.Invoke(true);
                    });
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MainThread.Run(() =>
                {
                    if (formError != null) Debug.Log("[Consent] formulaire UMP : " + formError.Message);
                    Debug.Log("[Consent] UMP résolu, pubs " + (ConsentInformation.CanRequestAds() ? "autorisées" : "non autorisées"));
                    done?.Invoke(true);
                }));
            });
#endif
        }

        public void ShowPrivacyOptions()
        {
#if UNITY_IOS
            NativeBridge.OpenAppSettings();
#else
            ConsentForm.ShowPrivacyOptionsForm(error => MainThread.Run(() =>
            {
                if (error != null)
                {
                    Debug.Log("[Consent] options de confidentialité : " + error.Message);
                    NativeBridge.OpenAppSettings();
                }
            }));
#endif
        }
    }
}
#endif
