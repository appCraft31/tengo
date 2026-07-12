# Achats in-app — packs de pièces

`coin_packs.json` est la **source de vérité** des 4 packs de pièces (consommables).
Les `productId` doivent rester alignés avec :
- `tenGO/StoreManager.swift` → `coinAmounts`
- `tenGO/tenGO.storekit` (config de test locale)

| productId | Pièces | Prix (USD) |
|---|---|---|
| com.tengo.coins.tier1 | 500 | 0.99 |
| com.tengo.coins.tier2 | 1200 | 1.99 |
| com.tengo.coins.tier3 | 3000 | 3.99 |
| com.tengo.coins.tier4 | 6500 | 7.99 |

## Synchronisation vers App Store Connect

`deliver` (fastlane) **ne gère pas** les achats in-app : on passe par l'API App
Store Connect via Spaceship, encapsulée dans la lane `iap_sync`.

```bash
# Lecture seule : liste les IAP existants et le plan (ne modifie rien)
bundle exec fastlane iap_sync

# Applique : crée les produits manquants + localisations
bundle exec fastlane iap_sync apply:true
```

La lane est **idempotente** : un produit déjà présent (même `productId`) est
ignoré (jamais recréé).

### À finaliser manuellement dans App Store Connect
- **Prix** : l'API gère les prix via *price points* (par territoire). La lane
  affiche le prix cible (`priceUSD`) mais ne le fixe pas automatiquement — à
  confirmer/ajuster dans App Store Connect.
- **Captures de la review** (1 par IAP) si exigées par la review.
- **Disponibilité** par territoire.

### Pré-requis
- `fastlane/AuthKey_U5B34558L6.p8` présent (clé API App Store Connect).
- `bundle install` (fastlane récent, avec `Spaceship::ConnectAPI::InAppPurchaseV2`).

> Astuce : toujours lancer la version lecture seule d'abord pour vérifier le plan,
> puis `apply:true`. Tester ensuite l'achat en sandbox avant la mise en vente.
