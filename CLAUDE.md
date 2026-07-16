# tenGO — monorepo iOS + Android

Jeu de puzzle mobile (chemins de bulles dont la somme fait 10). Le dépôt est organisé par plateforme :

- **`ios/`** — projet iOS d'origine (Swift + SpriteKit), **référence fonctionnelle et visuelle**.
  Workspace : `ios/tenGO.xcworkspace`, code dans `ios/tenGO/`, Fastlane iOS dans `ios/fastlane/`,
  docs iOS dans `ios/docs/`.
- **`android/`** — portage Kotlin/Jetpack Compose. ⚠️ **Dépôt git séparé (imbriqué)**, ignoré par
  le repo parent : commits Android à faire depuis `android/`. Voir `android/CLAUDE.md`.
- **`marketing/`** — assets communs (screenshots, icônes, visuels, fiches store, pubs).

## Commandes usuelles

```bash
# iOS (depuis ios/)
xcodebuild -workspace tenGO.xcworkspace -scheme tenGO -configuration Debug \
  -destination 'generic/platform=iOS Simulator' build
bundle exec fastlane <lane>          # Gemfile/Fastfile dans ios/

# Android (depuis android/)
./gradlew :core-game:test
./gradlew :app:assembleDebug
```

## Git

- Branches : features sur `develop`, `main` = prod (avec validation).
- Le repo parent ne versionne que `ios/`, `marketing/` et la config racine ; `android/` a son
  propre historique.
