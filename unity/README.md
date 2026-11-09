# tenGO — Unity

Réécriture en C# / Unity 6 de tenGO, pour remplacer les deux apps natives par une seule base :
- iOS : Swift + SpriteKit, `../ios/`, qui sert de référence ;
- Android : Kotlin + Compose, `../android/`.

Version visée : 4.0.0, en parité stricte avec la 3.3.0. Avancement dans `MIGRATION.md`, correspondances et règles dans `CONVENTIONS.md`.

## Prérequis

- Unity **6000.6.8f1** (`/Applications/Unity/Hub/Editor/6000.6.8f1`), avec les modules iOS et Android.
- Une licence Unity active sur la machine. La licence Personal suffit, y compris en batchmode.

## Commandes

Toutes les commandes passent par le verrou `Logs/unity.lock`, qui garantit une seule instance d'Unity à la fois sur le projet.

| Commande | Effet |
|---|---|
| `Tools/compile_check.sh` | Compile le projet. En cas d'échec : code de sortie non nul et liste des erreurs `CS` (journal `Logs/compile.log`) |
| `Tools/run_tests.sh [EditMode\|PlayMode\|all] [filtre]` | Lance les tests NUnit, résultats dans `Logs/tests/` |
| `Tools/unity.sh <Namespace.Classe.Méthode> [args]` | Lance une méthode d'éditeur en batchmode (journal `Logs/unity_<Méthode>.log`) |
| `Tools/unity.sh TenGo.Editor.ProjectSetup.Apply` | Réapplique les réglages du Player, les polices TMP, le matériau et la scène de démo |
| `Tools/unity.sh TenGo.Editor.Captures.GlowDemo` | Rend la scène de démo dans `Logs/captures/glow_demo.png` |

Variables d'environnement :
- `UNITY` : chemin de l'exécutable d'Unity ;
- `UNITY_LOCK_TIMEOUT` : attente maximale du verrou, 3600 s par défaut.

## Organisation

```
Assets/
  Scripts/Core/        TenGo.Core : logique pure, sans UnityEngine
  Scripts/             TenGo : jeu (Rendering, Units, Palette)
  Editor/              TenGo.Editor : ProjectSetup, Captures
  Tests/EditMode/      TenGo.Tests.EditMode (Core)
  Tests/PlayMode/      TenGo.Tests.PlayMode (jeu)
  Resources/Shaders/   GlowCircle.shader (cercle SDF)
  Resources/Materials/ GlowCircle.mat
  Fonts/               Nunito (Regular → Black) + SDF TMP dynamiques, licence OFL
  Scenes/GlowDemo.unity
  Settings/            URP (UniversalRP, Renderer2D)
  TextMesh Pro/        ressources essentielles de TMP
Tools/                 scripts batchmode et verrou (lib/unity_lock.zsh)
```

## Réglages

- **Identité** : nom « tenGO », société AppCraft31, identifiants `com.appcraft31.tengo` (Android) et `AppCraft31.tenGO` (iOS), ceux des apps en ligne.
- **Version** : 4.0.0, versionCode 17, build iOS 283.
- **Plateformes** :
  - portrait seulement ;
  - iOS 15 minimum, sur iPhone et iPad ;
  - Android API 26 minimum (plancher d'Unity 6.6, l'app native était à 24), cible la plus récente installée ;
  - IL2CPP, ARM64, AAB.
- **Rendu** : URP avec renderer 2D, espace linéaire, sans post-traitement ni écran de démarrage Unity.

Le squelette vient de l'étape 1 du portage d'Orbis (`~/StudioProjects/En_developpement/Orbis_Unity`, commit `7a4c147`). Les services de publication reprendront ceux de Binero (`~/StudioProjects/Multistore/binero/binero_Unity`).
