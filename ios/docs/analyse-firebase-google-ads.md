# Analyse — Remontées Firebase & préparation campagne Google Ads

> Branche : `analyse/firebase-analytics-google-ads`
> Date : 2026-06-26
> Périmètre : app iOS native **tenGO** (SpriteKit), Firebase SDK, AdMob, StoreKit 2.

## TL;DR

**Non, les remontées d'analyse ne sont pas suffisantes pour piloter une campagne Google Ads.**

On peut techniquement *lancer* une campagne (Google optimisera l'install via SKAdNetwork + conversions modélisées), mais on est **aveugle sur la qualité du trafic et sur la conversion in-app** : impossible d'optimiser sur l'achat ou la rétention, impossible d'importer des conversions dans Google Ads, pas d'audiences. Le ROAS ne sera pas mesurable.

La cause racine est simple : **Firebase Analytics est désactivé et aucun événement n'est tracké.**

---

## Ce qui est en place (le bon)

| Élément | État | Fichier |
|---|---|---|
| Firebase Core initialisé | ✅ `FirebaseApp.configure()` | `tenGO/AppDelegate.swift:19` |
| SDK Firebase Analytics lié | ✅ `FirebaseAnalytics 12.9.0` (CocoaPods) | `Podfile.lock` |
| Google Mobile Ads SDK | ✅ `13.2.0`, bannière + interstitiel | `tenGO/GameViewController.swift`, `tenGO/InterstitialAdManager.swift` |
| Consentement GDPR (UMP) | ✅ flow complet `requestConsentInfoUpdate` → formulaire | `tenGO/ConsentManager.swift` |
| App Tracking Transparency (ATT) | ✅ `ATTrackingManager.requestTrackingAuthorization` + `NSUserTrackingUsageDescription` | `tenGO/ConsentManager.swift:62`, `tenGO/Info.plist:56` |
| Achats intégrés (StoreKit 2) | ✅ packs de pièces consommables | `tenGO/StoreManager.swift` |
| Conversion on-device (SDK) | ✅ pod `GoogleAdsOnDeviceConversion 3.2.0` présent | `Podfile.lock` |

Le socle technique (consentement, ATT, ads, IAP) est propre et conforme. C'est une bonne base.

---

## Ce qui bloque (le critique)

### 🔴 1. Firebase Analytics est désactivé dans la config
```
tenGO/GoogleService-Info.plist
  <key>IS_ANALYTICS_ENABLED</key>
  <false></false>
```
Même si le SDK est lié, **aucune donnée ne remonte à Firebase** : pas de `first_open`, pas de `session_start`, pas de `screen_view`, pas de `in_app_purchase` auto-collecté. La console Firebase Analytics est vide.

**Conséquence Google Ads** : on ne peut pas lier Firebase à Google Ads ni importer de conversions in-app. L'optimisation se limite à l'install brut.

### 🔴 2. Aucun événement custom tracké
Recherche exhaustive : **0 occurrence** de `logEvent`, `setUserProperty`, `setUserID`, `Analytics.*` dans tout le code Swift. Aucun service de tracking maison (pas d'`AnalyticsService`), aucun autre SDK (AppsFlyer, Adjust, Branch, Amplitude… : absents).

Il manque tout le funnel produit qui sert à mesurer la qualité d'une campagne et à optimiser :
- `tutorial_begin` / `tutorial_complete`
- `level_start` / `level_complete` / `game_over`
- `daily_challenge_played`
- `in_app_purchase` (achat de pièces) ← **la conversion clé pour le ROAS**
- `ad_impression` (revenu pub) ← clé pour modéliser la LTV
- signaux de rétention (D1/D7)

### 🟠 3. SDK de conversion on-device présent mais non utilisé
`GoogleAdsOnDeviceConversion` est tiré en dépendance transitive de `GoogleAppMeasurement`, mais **n'est initialisé nulle part** dans le code. La mesure de conversion on-device (qui améliore l'attribution iOS) n'est donc pas active.

### 🟠 4. SKAdNetwork minimal
`tenGO/Info.plist` ne déclare **qu'un seul** `SKAdNetworkIdentifier` (`cstr6suwn9.skadnetwork`, celui de Google/AdMob). Pour une attribution iOS complète côté monétisation/mediation, il faut la liste étendue des réseaux. Point secondaire pour l'advertiser, mais à corriger.

---

## Verdict pour la campagne

| Besoin campagne Google Ads | Disponible aujourd'hui ? |
|---|---|
| Lancer une campagne « installs » (UAC) | ⚠️ Oui, mais à l'aveugle |
| Mesurer le coût par install (SKAdNetwork) | ⚠️ Partiel (1 seul SKAN ID) |
| Optimiser sur **l'achat in-app** (tROAS / conversions) | ❌ Non |
| Optimiser sur l'engagement (tutoriel, niveau, rétention) | ❌ Non |
| Importer des conversions Firebase → Google Ads | ❌ Non (Analytics off) |
| Créer des **audiences** (remarketing, lookalike) | ❌ Non |
| Mesurer la LTV / le revenu pub par cohorte d'acquisition | ❌ Non |

---

## Plan d'action recommandé (avant de dépenser 1 €)

**Étape 1 — Activer Firebase Analytics**
1. Passer `IS_ANALYTICS_ENABLED` à `true` dans `GoogleService-Info.plist` (ou retirer la clé).
2. Vérifier la remontée des événements auto (`first_open`, `session_start`) dans la console Firebase (DebugView).

**Étape 2 — Instrumenter le funnel** (ajouter `FirebaseAnalytics` + `Analytics.logEvent`)
- Achat : logger `in_app_purchase` dans `StoreManager.credit(_:)` (montant, devise, productID) — **priorité n°1**.
- Engagement : `tutorial_complete`, `level_complete`, `daily_challenge_played`.
- Pub : `ad_impression` (avec valeur eCPM AdMob) pour modéliser la LTV.
- Idéalement un wrapper `AnalyticsService` centralisé.

**Étape 3 — Lier Firebase ↔ Google Ads**
- Dans Firebase : lier le compte Google Ads.
- Marquer `in_app_purchase` (et un événement d'engagement) comme **conversions** importables dans Google Ads.

**Étape 4 — Activer la mesure de conversion on-device**
- Initialiser `GoogleAdsOnDeviceConversion` (déjà disponible via le pod) pour améliorer l'attribution iOS post-ATT.

**Étape 5 — Compléter SKAdNetwork**
- Ajouter la liste étendue des `SKAdNetworkIdentifier` recommandée par Google/AdMob dans `Info.plist`.

**Étape 6 — Laisser tourner ~2 semaines** pour accumuler des données de base avant le lancement, puis démarrer en optimisation « install » et basculer sur « conversion/achat » une fois le volume suffisant.

---

## Conclusion
L'infrastructure pub/consentement est saine, mais la **boucle de mesure est manquante** : Analytics off + zéro événement. En l'état, une campagne Google Ads serait du « spray and pray ». Les étapes 1 à 3 (1 à 2 jours de dev) sont le minimum pour rendre la campagne pilotable et mesurable.
