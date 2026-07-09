# Réponse Resolution Center — rejet 5.1.1(iv) (v2.1)

À coller dans App Store Connect → Resolution Center, avec la soumission du nouveau build.

---

Hello,

Thank you for your detailed review. We have identified the root cause and fully reworked the app's consent flow in this new build to comply with guideline 5.1.1(iv).

For context: the app itself does not embed any web content or collect cookies directly. What appeared after the App Tracking Transparency prompt was the Google User Messaging Platform (UMP) GDPR consent form, and the Google Mobile Ads / Firebase Analytics SDKs were initialized without taking the user's ATT choice into account. Both issues are fixed.

What changed in this build:

1. The App Tracking Transparency prompt is now always shown FIRST, before any other consent UI.

2. If the user selects "Ask App Not to Track":
   - No cookie/GDPR consent prompt is ever displayed.
   - No data is collected for tracking purposes: all Google consent-mode advertising signals (ad_storage, ad_user_data, ad_personalization) are denied by default at app launch via Info.plist keys, and remain denied.
   - The app only requests non-personalized ads (Google Mobile Ads publisherPrivacyPersonalizationState = disabled, plus "npa=1" on every ad request).

3. The Google UMP GDPR consent form is only shown to users who have granted ATT authorization, and only where legally required (EEA/UK).

4. Advertising signals are granted to the Google SDKs only after the user has both authorized ATT and given GDPR consent where applicable.

Regarding regional behavior: the app behaves the same way in all countries. The only difference is that the GDPR consent form (shown only after ATT authorization) appears solely in regions where it is legally required (EEA/UK).

We believe this fully addresses the issue. Please let us know if you need any additional information.

Thank you!
