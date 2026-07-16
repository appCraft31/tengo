# Achats in-app

Deux fiches, chacune source de vérité de ses produits :

| Fiche | Produits | Type |
|---|---|---|
| `coin_packs.json` | `com.tengo.coins.tier1..4` | CONSUMABLE |
| `no_ads.json` | `com.tengo.noads` | NON_CONSUMABLE |

Les `productId` doivent rester alignés avec :
- `tenGO/StoreManager.swift` → `coinAmounts` (packs) et `adFreeProductID`
- `tenGO/AdFreeManager.swift` → `productID` (mod sans pub)
- `tenGO/tenGO.storekit` (config de test locale)

| productId | Contenu | Prix |
|---|---|---|
| com.tengo.coins.tier1 | 500 pièces | 0,99 $ (base USA) |
| com.tengo.coins.tier2 | 1200 pièces | 1,99 $ (base USA) |
| com.tengo.coins.tier3 | 3000 pièces | 3,99 $ (base USA) |
| com.tengo.coins.tier4 | 6500 pièces | 7,99 $ (base USA) |
| com.tengo.noads | Supprime bannière + interstitielles | **3,99 € (base FRA)** |

## Synchronisation vers App Store Connect

⚠️ `deliver` ne gère pas les IAP, et **Spaceship non plus** sur la version
installée (pas de modèle `InAppPurchaseV2` → la lane `iap_sync` est
inopérante, gardée pour mémoire). Tout passe par l'**API REST**.

```bash
# ⚠️ bundle exec est cassé (bundler 1.17.2 vs Ruby 4.0) → fastlane global.

# Créer / compléter un IAP (dry-run par défaut)
/opt/homebrew/bin/fastlane iap_create file:no_ads.json
/opt/homebrew/bin/fastlane iap_create file:no_ads.json apply:true

# Attacher la capture de review (chemin ABSOLU)
/opt/homebrew/bin/fastlane iap_screenshot file:no_ads.json \
  path:/Users/nicolas/StudioProjects/tenGO/fastlane/iap/iap_noads_review.png

# Auditer l'état (écrit iap/asc_audit.json)
/opt/homebrew/bin/fastlane iap_audit
```

`iap_create` est **idempotente** et fait les 4 étapes qu'exige un IAP complet :
création, localisations, prix (price schedule), **disponibilité par territoire**.

## Pièges de l'API (vécus)

- **Création** : `POST /v2/inAppPurchases`. Le `/v1` est en lecture seule.
  Ne PAS envoyer `availableInAllTerritories` → 409 (l'attribut n'existe pas).
- **Lecture des sous-ressources** : `GET /v2/inAppPurchases/{id}/inAppPurchaseLocalizations`.
  Le chemin `/v1/inAppPurchases/{id}/...` renvoie **404** (piège : on croit
  qu'il n'y a aucune localisation, alors qu'elles existent).
- **Price schedule** : il partage l'id de l'IAP →
  `GET /v1/inAppPurchasePriceSchedules/{id}` (et non la sous-ressource
  `/iapPriceSchedule`, qui renvoie 404).
- **Relations** : les localisations et les captures pointent l'IAP via
  `inAppPurchaseV2` ; le price schedule via `inAppPurchase`. (Incohérence Apple.)
- **Disponibilité** : sans enregistrement `inAppPurchaseAvailabilities`, l'IAP
  reste bloqué en `MISSING_METADATA` même avec nom, prix, localisations et
  capture. C'est le piège le plus coûteux → `iap_create` la pose (175 territoires).
- **Longueurs** : description ≤ **55 caractères**, nom ≤ 30. Au-delà : 409.
- Prix en base EUR : `filter[territory]=FRA` renvoie des price points en euros.
  Ne pas réutiliser le chemin `priceUSD` de `iap_fix` (câblé sur USA).

## État attendu

`MISSING_METADATA` → `READY_TO_SUBMIT` une fois nom + localisations + prix +
disponibilité + review note + capture présents. L'IAP est ensuite soumis
**avec la prochaine version de l'app** dans App Store Connect.
