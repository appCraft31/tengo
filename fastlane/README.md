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

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
