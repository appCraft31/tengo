# tenGO — Unity

Jeu de puzzle mobile (chemins de bulles dont la somme fait 10). Depuis la 4.0.0, une seule base Unity
pour iOS et Android.

- **`unity/`** — le jeu (Unity 6). Voir `unity/CLAUDE.md`, `unity/README.md` et `unity/MIGRATION.md` ;
  toujours passer par `unity/Tools/*.sh` (verrou Unity). Clés de signature et configs Firebase dans
  `unity/config/` (ignoré par git).
- **`marketing/`**, **`design/`** — assets communs (captures, icônes, visuels, fiches store, pubs, refonte).
- **`firebase/`** — règles Firestore et leurs tests.

## Anciennes versions (archivées, lecture seule)

- iOS Swift + SpriteKit 3.3.0 : `~/StudioProjects/Archives/tenGO/ios/`, et dans ce dépôt au tag
  `archive/ios-swift-3.3.0` (`git show archive/ios-swift-3.3.0:ios/<chemin>`).
- Android Kotlin + Compose : `~/StudioProjects/Archives/tenGO/android/` (dépôt git `appCraft31/tengo-android`,
  tag `archive/android-kotlin`).

## Git

- Branches : features sur `develop`, `main` = prod (avec validation).
