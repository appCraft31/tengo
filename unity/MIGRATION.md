# Migration vers Unity : avancement

Objectif : tenGO **4.0.0** en Unity, en parité stricte avec la 3.3.0 (iOS build 282, Android versionCode 16). Il remplacera les deux apps le même jour.

Plan détaillé : `~/.claude/plans/je-voudrais-basculer-tengo-giggly-owl.md`.

| # | Étape | État |
|---|---|---|
| 1 | Squelette : Unity 6000.6.8f1, URP 2D, Player Settings (identifiants des apps en ligne, 4.0.0), assemblies, verrou, polices Nunito, `GlowCircle`, scène de démo | ✅ fait |
| 2 | Core en C# : grille, aléatoire Swift, génération, score, défi du jour, puzzles, miroir, graines, progression, économie, fin de partie + tests portés de Kotlin | ✅ fait |
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

## Notes de l'étape 2

- **Périmètre** : toute la logique pure est dans `Assets/Scripts/Core/` (namespace `TenGo.Core`, sans `UnityEngine`), un dossier par domaine :
  - `Random/` : SplitMix64 et extensions reproduisant la bibliothèque standard Swift au bit près (Lemire, Fisher-Yates avant, `randomElement`, `Int.random(in:)`), multiplication 128 bits faite à la main (pas de `Math.BigMul` en .NET Standard 2.1).
  - `Grid/` : `GridModel` / `BubbleModel` (noms Swift, pour ne pas heurter `UnityEngine.Grid`), gravité, obstacles, ancrage, gel, fleurs, `Scoring`, `PathSolver`, `GridValidator`.
  - `Daily/` : défi du jour (clé décalée de 5 h, twists, objectifs, cache rendant une copie).
  - `Puzzles/` : catalogue lu depuis `Assets/Resources/Puzzles/puzzles.json` (20 niveaux, générés depuis `PuzzleCatalog.swift`) ; le chargeur prend le texte JSON.
  - `Mirror/`, `Seeds/` : miroir cognitif (41 partitions) et graines de dix.
  - `Progression/` : XP et niveaux, missions, série (boucliers, paliers), stats, 26 succès, progression des puzzles, historique de duels, dépôts du miroir et des graines.
  - `Economy/` : pièces, boosters, thèmes et cosmétiques (données), produits de la boutique, mode sans pub.
  - `Ads/` : cadence des interstitielles (150 s, 2e partie, 3e en première session, 1 sur 4 en fin de partie auto) et des vidéos récompensées (5 par jour × 10 pièces).
  - `Game/` : `GameMode` et son codec, `RecordStatus`, `FirstRunState`, `GameEndCoordinator`.
  - `Save/`, `Time/`, `Settings/` : `ISaveStore` (+ `MemorySaveStore`), clés iOS centralisées dans `SaveKeys`, dates Swift (`SwiftDate`), horloge injectée (`IClock`, `FixedClock`), réglages.
- **Aucune horloge ni aléa global** : date, fuseau et générateur sont passés en paramètre (`IClock`, `IRng`).
- **Tests EditMode** : 117 tests, tous verts. 78 portés un à un des tests JUnit (mêmes cas, mêmes valeurs, dont les vecteurs `SwiftRngParityTest` et `dailyGridMatchesIos`), 38 ajoutés (formats de sauvegarde iOS, `SavedGame`, `SwiftDate`, économie, thèmes, cosmétiques, mode sans pub, cadence des pubs, premier lancement, partie continuée, miroir et graine dans le coordinateur, arrondi Swift, `Mul128` contre `BigInteger`), plus `ScreenRefTests` conservé. `ScalesTest` est laissé à l'étape 4.
- **Divergences Kotlin / Swift** (Swift fait foi, le C# suit Swift) :
  - Série : iOS stocke le dernier jour joué en `Double` (début du jour local, secondes depuis 2001) ; Kotlin stockait un numéro de jour epoch en chaîne.
  - Miroir : iOS utilise deux clés (`tenGO_mirrorRecords`, `tenGO_mirrorPartitions`) avec des dates Swift ; Kotlin une seule (`tenGO_mirrorData`) en ms.
  - Graine : dates du JSON en secondes depuis 2001 (`plantedAt`, `bloomAt`).
  - Clés de texte : clés pointées de Swift (`level.title.*`, `mission.*`, `achievement.*`, `booster.*`, `theme.*`, `style.*`, `trail.*`, `puzzle.world1`) au lieu des noms de ressources Android ; les tests portés utilisent les clés Swift.
  - `XpForNext` vaut 0 au niveau maximum (Kotlin rendait le coût du niveau).
  - Un succès est « débloqué » à l'affichage dès que la valeur atteint la cible (Swift), et non selon l'ensemble stocké (Kotlin).
  - Meilleurs scores : tableau d'entiers (Swift) et non chaîne CSV.
  - Partie sauvegardée : pas de champ `mode` (Swift `GameState`).
  - Historique de duels : les codes ne sont ni rognés ni mis en majuscules.
  - Écritures conditionnelles : `reportScore` ignore les deltas ≤ 0, le palier de série n'est écrit que s'il change, les étoiles de puzzle seulement si elles augmentent.
  - Démo d'un puzzle : les étoiles sont calculées (affichées) sans être enregistrées.
  - Premier lancement : `gameCompleted` appelé à la fin de toute partie hors démo et hors reprise.
  - Interstitielles : pas de plafond journalier sur iOS ; le compteur du jour ne sert qu'à l'offre sans pub.
  - Bonus sans pub : drapeau `noAdsBonusPending` d'iOS (et non « bonus accordé » de Kotlin).
- **Décisions** :
  - `GridModel` est une classe avec `Copy()` profonde (la grille Swift est une valeur) ; le cache du défi du jour rend une copie à chaque appel.
  - La graine d'un duel reste un `long` (comme Kotlin).
  - Arrondi Swift `.rounded()` = `MidpointRounding.AwayFromZero` ; tri stable (`OrderBy`) là où Swift trie.
  - Les dépôts lisent et écrivent exactement les formats d'iOS : la reprise des données natives (étape 3) n'aura qu'à recopier les valeurs.
- **Reste pour la suite** : lecture des UserDefaults / DataStore natifs (étape 3), `ScalesTest` et rendu (étape 4), implémentation d'`ISaveStore` sur `PlayerPrefs` ou fichier (étape 3), analytics réelles derrière `IGameAnalytics` (étape 7), coûts et achats réels de la boutique (étape 7).
- **Point d'entrée prévu pour l'étape 5** : la scène de jeu compose les dépôts autour d'un même `ISaveStore` et d'une `IClock` système, puis appelle `GameEndCoordinator` : `OnGameStarted(mode)` au début, `OnChainCommitted` / `OnBonusScored` / `OnRushBoardCleared` en cours de partie, `OnGameEnded(GameEndInput)` à la fin (avec `ContinuedFrom` après « Continuer »). La grille vient de `GridModel.Generate`, `DailyChallenge.Make(clock)`, `PuzzleLevel.MakeGrid()` ou `GameStateRepository.Load()`.
