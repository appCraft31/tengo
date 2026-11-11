# Conventions du portage Swift/SpriteKit → C#/Unity

La référence est le jeu iOS `~/StudioProjects/Archives/tenGO/ios/tenGO/` (3.3.0, build 282). Le portage Android `~/StudioProjects/Archives/tenGO/android/` (Kotlin) est une seconde référence, déjà vérifiée contre l'iOS : sa logique pure (`core-game`, `app/.../progression`, `economy`, `game/GameEndCoordinator`) et ses tests JUnit servent de point de départ.

## Ce qui doit être identique au bit près

- **Aléatoire** : `SeededGenerator` (SplitMix64), `next(upperBound:)` de Lemire, `shuffle` (Fisher-Yates) et `randomElement` de Swift.
- **Génération** : grilles, validation par `GridValidator`, défi du jour (bascule à 5 h, twist, difficulté) et catalogue des puzzles.

Sans ça, un duel ou un défi du jour donnerait une grille différente selon la plateforme. Vérification par les vecteurs de `~/StudioProjects/Archives/tenGO/ios/tools/rng_vectors` et les tests `SwiftRngParityTest` et `DailyChallengeTest.dailyGridMatchesIos`.

- **Sauvegardes** : les clés et formats de `AppConfig.swift` (`tenGO_*`, `tengo_*`, `AdMob.*`) sont repris tels quels dans `ISaveStore`. Les données des apps natives sont importées au premier lancement (étape 3).
- **Analytics** : mêmes noms d'événements et de paramètres que `AnalyticsService.swift` et `AnalyticsService.kt`.

## Architecture

- **`TenGo.Core`** (`Assets/Scripts/Core`) :
  - règles, grille, génération, score, défi du jour, progression (XP, missions, série, stats, succès, puzzles), économie, miroir, graines, politique des pubs, sauvegarde, localisation ;
  - **aucune référence à UnityEngine** (`noEngineReferences: true`), seul Newtonsoft.Json est autorisé ;
  - testé en EditMode.
- **`TenGo`** (`Assets/Scripts`) : scènes, rendu, entrées, audio, services natifs. Référence Core.
- **`TenGo.Editor`** (`Assets/Editor`) : réglages reproductibles (`ProjectSetup.Apply`), captures, builds.
- **Tests** : `TenGo.Tests.EditMode` couvre Core, `TenGo.Tests.PlayMode` couvre le jeu.

## Correspondances

| Swift / Kotlin | C# / Unity |
|---|---|
| Fichier Swift sans SpriteKit ni UIKit (`GridModel`, `ScoreRules`, `DailyChallenge`, `MissionManager`…) ou fichier de `core-game` | `Assets/Scripts/Core/<Dossier>/<Fichier>.cs`, namespace `TenGo.Core` |
| `SKScene` (`GameScene`, `MenuScene`, `BoutiqueScene`…) | Un contrôleur `MonoBehaviour` par écran dans une scène unique |
| `SKShapeNode` (bulles, relief, anneaux) | `GlowCircle` et shaders SDF, sans texture |
| `SKLabelNode` (Avenir Next) | TextMeshPro, polices `Nunito-<Graisse> SDF` (Bold par défaut) |
| `SKAction` | Moteur d'interpolations maison (`Tw`, repris d'Orbis à l'étape 4), sans DOTween |
| `touchesBegan/Moved/Ended` | Input System (`EnhancedTouch`), souris en éditeur |
| `UserDefaults.standard` / DataStore | `ISaveStore` dans Core, fichier JSON dans `Application.persistentDataPath` |
| `L("clé")` / `Localizable.xcstrings` | `Loc.T("clé")`, `Resources/Localization/strings.json` généré depuis le `.xcstrings` |
| `SoundManager` (AVAudioEngine, 8 voix synthétisées) | Synthé `OnAudioFilterRead`, repris de `Sfx` dans Orbis ; depuis le son 4.0, kalimba et gammes tournantes (`Scales`, voir MIGRATION.md) |
| `HapticManager` | `IHaptics` |
| Game Center / Play Games, StoreKit / Play Billing, AdMob, Firebase | Services derrière des interfaces, avec une version simulée en éditeur |

## Unités et écran

- **Surface de référence** des scènes iOS : **750 × 1334** unités de scène (`Core.ScreenRef`), en aspectFill. Core raisonne dans ces unités, origine en bas à gauche comme SpriteKit.
- **Côté jeu** : 1 unité Unity = 100 unités de scène (`Units.PointsPerUnit`), origine au centre.
- Pas de déformation : on garde le cadrage aspectFill de SpriteKit et on ancre les éléments sur la zone visible et sûre (`safeArea`).

## Style

- Commentaires `///` en français, repris du Swift.
- `#nullable enable` en tête des fichiers de Core.
- `PascalCase` pour les types, méthodes et propriétés, `camelCase` pour les variables locales, `_camelCase` pour les champs privés.
- Indentation de 4 espaces. Une classe publique par fichier, nom de fichier identique.
- Aucun aléatoire global dans Core : générateur à graine injecté.
- Aucune horloge globale dans Core : la date est passée en paramètre.

## Tests

- `Tools/run_tests.sh [EditMode|PlayMode|all] [filtre]`, résultats dans `Logs/tests/`.
- Un test JUnit `android/**/<X>Test.kt` devient `Assets/Tests/EditMode/<X>Tests.cs`, avec les mêmes cas et les mêmes valeurs attendues.
- Captures : `Tools/unity.sh TenGo.Editor.Captures.<Méthode>` écrit un PNG dans `Logs/captures/`. Toujours regarder la capture après un changement visuel, et la comparer à l'écran iOS.

## Verrou Unity

Unity refuse deux instances sur le même projet. Toute invocation passe donc par `Tools/lib/unity_lock.zsh` (`unity_run`) :
- le verrou est le dossier `Logs/unity.lock`, pris par `mkdir` atomique ;
- l'attente se fait par pas de 5 s ;
- un verrou dont le pid est mort est repris ;
- le verrou est libéré par trap.
