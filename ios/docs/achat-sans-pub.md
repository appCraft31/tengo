# Achat « Sans pub » — test, diagnostic, suivi (TEN-2)

Fiche kanban TEN-2 (audit du 23 octobre 2026) : 33 appuis sur un achat, 31
annulations, aucun achat enregistré.

## Diagnostic (31 octobre 2026)

Parcours relu de bout en bout (`StoreManager`, `AdFreeManager`,
`GameScene.presentNoAdsOffer`, `BoutiqueScene.handleNoAds`) :

- **Produit** : `com.tengo.noads` (non-consommable, 3,99 € de base FRA) est
  `APPROVED` sur App Store Connect (`fastlane ios iap_audit`, 31/10), comme
  les 4 packs de pièces. En juillet, il était encore `READY_TO_SUBMIT` : il
  n'était alors proposé qu'en bac à sable.
- **Prix** : toujours celui de StoreKit (`Product.displayPrice`), jamais en dur.
  L'offre ne s'affiche pas tant que le prix est inconnu.
- **Feuille de paiement** : c'est la feuille système (`product.purchase()`).
  `iap_cancelled` = l'utilisateur l'a fermée, ou il n'était pas connecté à
  l'App Store et a refermé la demande de connexion. Aucun bug de code ne
  renvoie « annulé » à tort.
- **Vérifié au simulateur (31/10)** : sans compte Apple connecté, StoreKit
  affiche « Connectez-vous à votre compte Apple » au lieu de la feuille de
  paiement ; « Annuler » y est remonté en `iap_cancelled`, comme un vrai refus.
  Un build Debug envoie ces événements au même projet Firebase.
- **Ce qui faussait la mesure** :
  1. Les tests en bac à sable (Xcode, TestFlight, vérification Apple de la 3.1.0)
     envoient les mêmes événements Firebase que les vrais joueurs. Sur une app à
     ~3 joueurs actifs par jour, quelques essais de test suffisent à faire les
     « 31 annulations ».
  2. L'événement `purchase` ne porte pas l'écran d'origine, et rien ne marquait
     un achat approuvé plus tard (« Demander l'autorisation », SCA).
  3. `iap_tapped` compte tous les produits (packs de pièces compris), pas
     seulement « Sans pub ».
- **Risque d'appui accidentel** : la carte de l'offre (500 × 76) est tout
  entière un bouton d'achat, et elle apparaît juste après la fermeture d'une
  pub. Un appui destiné à la pub ou au panneau pouvait ouvrir la feuille de
  paiement → annulation immédiate.

## Ce qui change dans l'app

- **Offre après une série de pubs** : elle n'apparaît plus après la première
  interstitielle de la session, mais après **au moins 2 interstitielles depuis
  la précédente offre** (compteur conservé d'une session à l'autre), **2 fois au
  plus par session**. Elle rappelle « Pubs vues aujourd'hui : N ».
  Réglages : `StoreManager.adFreeOfferAfterAds` / `adFreeOffersPerSession`.
- **Anti-appui accidentel** : pendant 0,7 s après l'apparition de l'offre, un
  appui sur la carte est ignoré.
- **Mesure** :
  - `iap_success` (`item_id`, `source` = `shop` | `game_over` |
    `transaction_update`, `store_env`) ;
  - `noads_offer_view` porte `ads_seen` (taille de la série) et `ads_today` ;
  - `noads_offer_decline` (« Non merci ») ;
  - propriété utilisateur `store_env` = `production` | `sandbox` | `xcode` |
    `debug`.

## Suivre le nombre d'achats

Dans Firebase (projet `tengo-b62b1`), **filtrer sur `store_env = production`**,
puis lire l'entonnoir :

```
noads_offer_view → iap_tapped (item_id = com.tengo.noads)
                 → iap_success | iap_cancelled | iap_failed | iap_pending
```

Le chiffre de référence pour les achats réels reste App Store Connect →
Ventes et tendances (produit « Remove Ads »). L'audit hebdomadaire devrait
compter `iap_success` filtré sur `production`, et non `iap_tapped`.

## Tester l'achat sur un iPhone

Pré-requis : un compte **bac à sable** (App Store Connect → Utilisateurs et
accès → Testeurs Sandbox), déclaré sur l'iPhone dans **Réglages → App Store →
Compte Sandbox**. Aucun débit réel.

1. Installer le build depuis TestFlight (ou par Run dans Xcode sur l'iPhone).
2. **Prix** : Boutique → carte « Sans pub » : le prix affiché doit être celui
   d'ASC dans la devise du compte (3,99 € en France).
3. **Feuille de paiement** : appuyer sur la carte. La feuille doit indiquer
   « Sans publicité », le même prix et « [Environment: Sandbox] ». La fermer :
   le toast « Achat annulé » apparaît et la carte reste en place.
4. **Achat** : appuyer de nouveau et confirmer. Attendu : toast « Publicités
   supprimées. +500 pièces ! », solde +500, plus de bannière ni d'interstitielle.
5. **Restauration** : supprimer puis réinstaller l'app, puis Réglages →
   Restaurer les achats : le mode sans pub revient, sans nouveau bonus.
6. **Offre après une série** (sur une installation sans l'achat) : enchaîner les
   parties et « Rejouer ». L'offre doit apparaître sous le panneau de fin juste
   après la **2ᵉ** interstitielle, avec « Pubs vues aujourd'hui : 2 ». Un appui
   immédiat dessus ne doit rien ouvrir ; un appui après 1 s ouvre la feuille.
7. Firebase → DebugView (Run Xcode avec `-FIRDebugEnabled`) : vérifier
   `noads_offer_view`, `iap_tapped`, `iap_cancelled`, puis `iap_success`.

Au simulateur (sans compte) : `QA_FAKE_INTERSTITIAL=1` simule les pubs ;
l'achat lui-même demande un Run Xcode avec `tenGO.storekit` ou un compte bac
à sable.
