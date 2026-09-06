fastlane documentation
----

# Installation

Make sure you have the latest version of the Xcode command line tools installed:

```sh
xcode-select --install
```

For _fastlane_ installation instructions, see [Installing _fastlane_](https://docs.fastlane.tools/#installing-fastlane)

# Available Actions

## iOS

### ios beta

```sh
[bundle exec] fastlane ios beta
```

Build et upload sur TestFlight

### ios release

```sh
[bundle exec] fastlane ios release
```

Build et soumission App Store

### ios upload_binary

```sh
[bundle exec] fastlane ios upload_binary
```

Upload IPA uniquement (sans screenshots ni metadata) — après un release partiel

### ios upload_screenshots

```sh
[bundle exec] fastlane ios upload_screenshots
```

Upload screenshots uniquement (sans build) — toutes les langues

La version vient du projet (sinon deliver lit un vieux .ipa du dossier).

### ios bump_version

```sh
[bundle exec] fastlane ios bump_version
```

Met à jour MARKETING_VERSION — usage : bundle exec fastlane bump_version version:1.3

### ios release_notes

```sh
[bundle exec] fastlane ios release_notes
```

Envoie les métadonnées (notes de version) sur App Store Connect — sans binaire ni screenshots

Usage : bundle exec fastlane release_notes [version:1.8]

### ios iap_sync

```sh
[bundle exec] fastlane ios iap_sync
```

Crée / met à jour les achats in-app (packs de pièces) depuis fastlane/iap/coin_packs.json

Lecture seule par défaut. Pour appliquer : bundle exec fastlane iap_sync apply:true

Note : `deliver` ne gère pas les IAP — on passe par l'API App Store Connect (Spaceship).

### ios iap_audit

```sh
[bundle exec] fastlane ios iap_audit
```

Audit lecture seule des IAP sur App Store Connect (état, type, prix, localisations)

### ios iap_screenshot

```sh
[bundle exec] fastlane ios iap_screenshot
```

Attache la capture de review aux IAP d'une fiche.

Usage : fastlane iap_screenshot [file:coin_packs.json] [path:/chemin.png]

### ios iap_create

```sh
[bundle exec] fastlane ios iap_create
```

Crée un IAP depuis une fiche JSON, via l'API REST (Spaceship ne gère pas les IAP).

Usage : fastlane iap_create [file:no_ads.json] [apply:true]

Idempotent : un productId déjà présent n'est jamais recréé.

### ios iap_fix

```sh
[bundle exec] fastlane ios iap_fix
```

Corrige les IAP mal configurés (localisations + prix + review note) depuis coin_packs.json

Lecture seule par défaut. Pour appliquer : bundle exec fastlane iap_fix apply:true

### ios event_doctor

```sh
[bundle exec] fastlane ios event_doctor
```

Pré-vol LECTURE SEULE des événements intégrés (auth, état, visuels)

### ios in_app_event

```sh
[bundle exec] fastlane ios in_app_event
```

Crée/MAJ l'événement intégré « mise à jour majeure » de la 3.0

Usage : fastlane in_app_event [start:2026-09-15] [end:2026-09-29]

### ios event_asset_probe

```sh
[bundle exec] fastlane ios event_asset_probe
```

Teste un format de visuel sur une seule locale (avant l'envoi complet)

Usage : fastlane event_asset_probe path:/chemin.png [kind:EVENT_DETAILS_PAGE] [locale:fr-FR]

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
