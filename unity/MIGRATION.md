# Migration vers Unity : avancement

Objectif : tenGO **4.0.0** en Unity, en parité stricte avec la 3.3.0 (iOS build 282, Android versionCode 16). Il remplacera les deux apps le même jour.

Plan détaillé : `~/.claude/plans/je-voudrais-basculer-tengo-giggly-owl.md`.

| # | Étape | État |
|---|---|---|
| 1 | Squelette : Unity 6000.6.8f1, URP 2D, Player Settings (identifiants des apps en ligne, 4.0.0), assemblies, verrou, polices Nunito, `GlowCircle`, scène de démo | ✅ fait |
| 2 | Core en C# : grille, aléatoire Swift, génération, score, défi du jour, puzzles, miroir, graines, progression, économie, fin de partie + tests portés de Kotlin | à faire |
| 3 | Reprise des données des apps natives (UserDefaults iOS, DataStore et SharedPreferences Android) | à faire |
| 4 | Rendu (bulles, relief, tracés, thèmes, effets) et audio synthétisé | à faire |
| 5 | Partie (`GameScene` : 6 modes, boosters, sauvegarde, fin de partie) | à faire |
| 6 | Écrans (menu, boutique, profil, missions, succès, miroir, duel, puzzles, classement, réglages, tutoriel) | à faire |
| 7 | Services : AdMob + UMP/ATT + médiation, Unity IAP, Firebase (Analytics, Auth, Firestore), Game Center / Play Games, notifications, avis, partage, haptique, liens profonds | à faire |
| 8 | Builds signés (AAB, projet Xcode), fastlane iOS, `.play-deploy.json` | à faire |
| 9 | Recette (mise à jour par-dessus la 3.3.0) puis publication simultanée, sur accord de l'utilisateur | à faire |
| 10 | Gel des apps natives `ios/` et `android/` | à faire |

## Notes de l'étape 1

- **Origine du squelette** : l'étape 1 d'Orbis (`Orbis_Unity`, commit `7a4c147`), renommée de `Orbis` en `TenGo`, avec un nouveau `productGUID`.
  - Orbis avait été créé à partir de `com.unity.template.2d-cross-platform`, allégé ensuite.
  - Les ressources essentielles de TextMeshPro sont versionnées dans `Assets/TextMesh Pro`.
- **Surface de référence** : 750 × 1334 (`ScreenRef`), celle des scènes SpriteKit de tenGO, au lieu du 390 × 844 d'Orbis.
- **Polices** :
  - Nunito en 5 graisses statiques (Regular, SemiBold, Bold, ExtraBold, Black), générées avec fontTools `varLib.instancer` depuis la police variable d'Android (`android/app/src/main/res/font/nunito.ttf`), licence OFL dans `Assets/Fonts/Nunito-OFL.txt`.
  - La police variable ne couvre que l'alphabet latin : il faudra des replis CJK (Noto, comme dans Orbis) pour le japonais, le coréen et le chinois (étape 4).
- **Android minimum API 26**, contre 24 pour l'app native : c'est le plancher d'Unity 6.6. Les appareils sous Android 7 ne recevront pas la 4.0.0.
- **SDK de services** (AdMob, Firebase, IAP, Play Games) : reportés à l'étape 7. Les ajouter dès maintenant alourdirait chaque compilation (résolution EDM) sans rien apporter avant les écrans.
