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

----

This README.md is auto-generated and will be re-generated every time [_fastlane_](https://fastlane.tools) is run.

More information about _fastlane_ can be found on [fastlane.tools](https://fastlane.tools).

The documentation of _fastlane_ can be found on [docs.fastlane.tools](https://docs.fastlane.tools).
