# Migration vers Unity : avancement

Objectif : tenGO **4.0.0** en Unity, en parité stricte avec la 3.3.0 (iOS build 282, Android versionCode 16). Il remplacera les deux apps le même jour.

Plan détaillé : `~/.claude/plans/je-voudrais-basculer-tengo-giggly-owl.md`.

| # | Étape | État |
|---|---|---|
| 1 | Squelette : Unity 6000.6.8f1, URP 2D, Player Settings (identifiants des apps en ligne, 4.0.0), assemblies, verrou, polices Nunito, `GlowCircle`, scène de démo | ✅ fait |
| 2 | Core en C# : grille, aléatoire Swift, génération, score, défi du jour, puzzles, miroir, graines, progression, économie, fin de partie + tests portés de Kotlin | ✅ fait |
| 3 | Reprise des données des apps natives (UserDefaults iOS, DataStore et SharedPreferences Android) + sauvegarde fichier JSON | ✅ fait (reste la vérification sur appareil, étape 9) |
| 4 | Rendu (bulles, relief, tracés, thèmes, effets) et audio synthétisé | ✅ fait (reste la vérification sur appareil, étape 9) |
| 5 | Partie (`GameScene` : 6 modes, boosters, sauvegarde, fin de partie) | ✅ fait (services simulés jusqu'à l'étape 7, écrans hors partie provisoires jusqu'à l'étape 6) |
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
- **Reste pour la suite** : lecture des UserDefaults / DataStore natifs (étape 3), `ScalesTest` et rendu (étape 4), analytics réelles derrière `IGameAnalytics` (étape 7), coûts et achats réels de la boutique (étape 7).
- **Point d'entrée prévu pour l'étape 5** : la scène de jeu compose les dépôts autour d'un même `ISaveStore` et d'une `IClock` système, puis appelle `GameEndCoordinator` : `OnGameStarted(mode)` au début, `OnChainCommitted` / `OnBonusScored` / `OnRushBoardCleared` en cours de partie, `OnGameEnded(GameEndInput)` à la fin (avec `ContinuedFrom` après « Continuer »). La grille vient de `GridModel.Generate`, `DailyChallenge.Make(clock)`, `PuzzleLevel.MakeGrid()` ou `GameStateRepository.Load()`.

## Notes de l'étape 4

- **Espace colorimétrique : Gamma** (`ProjectSetup.Apply`, `PlayerSettings.colorSpace`). SpriteKit et Compose mélangent en sRGB ; en Linear, les voiles sombres sur fond crème (tracé encre, socles, ombres) sortaient 10 à 30 % trop clairs. Les couleurs passent telles quelles (sRGB) aux shaders, et `TenGoOut()` (`Resources/Shaders/TenGoCommon.hlsl`) gère les deux espaces si l'on revenait en Linear.
- **Repère** : 1 unité = 100 pt, scène de référence 750 × 1334 en aspectFill (`Units.ReferenceOrthoSize`), `Units.P(x, y)` en points. Ordre de tri = `zPosition` SpriteKit × 10 (`Rendering/Layers`).
- **Horloge et animations** (`Scripts/Fx`, briques d'Orbis adaptées) :
  - `Tw` (équivalent des `SKAction` : `Seq`, `Par`, `Forever`, `MoveBy` cumulable, `MoveToY`, `RotateBy`, `Custom`, clés, `Kill`, `SetSpeed`) ;
  - `FxTime` : `Now` (ralenti par `Speed`), `RealNow`, `AfterRealtime` (rendu de la main après un arrêt sur image), `Step`/`Advance` en éditeur (captures et tests reproductibles), `_TenGoTime` global pour les shaders. En jeu, un `FxDriver` caché l'avance avec `Time.unscaledDeltaTime`.
  - `JuiceSettings` (point de lecture unique de « effets réduits » + Reduce Motion système, `ForceFull` pour les captures), `PopTier` (valeurs de `JuiceDirector.swift`).
- **API pour l'étape 5 (partie)** :
  - `BubbleView.Create(parent, value, pos)` puis `SetSelected(bool, tension)`, `SetFrozen`, `Thaw`, `SetAnchored`, `SetBloom(power)`, `Nudge`, `CancelNudge`, `PlayPop(cb)`, `PlayFall(toY, duration, cb)` ; `BubbleView.MakeObstacle`, `MakeFlowerVisual`. Matière lue dans `Look.Bubble`, couleurs dans `Look.Theme` (`Look.Apply(themeId, bubbleId, trailId)` depuis les cosmétiques actifs).
  - `TrailRenderer.Make(parent, points, Look.Trail, Look.Accent, tension)` (5 styles, rampe t^2,2) et `TrailRenderer.MoveComet(comet, parent, head, tension, accent)` ; à recréer à chaque changement de chemin comme iOS.
  - `JuiceDirector(fxLayer, ShakeCamera.Attach(cam), sceneSize)` : `OnSelect`, `OnAppend(tension)`, `OnRejected(view,row,col)`, `OnCommit(tier,length)` (haptique, punch, secousse, flash, arrêt sur image), `Burst(pos, color, tier)`, `OnWin(center, accent)`, `OnLose`, `ResetTimeScale` (à appeler en fin de partie et au retour menu), `AllowsScreenEffects`.
  - `ThemeBackground.Make(parent, theme, sceneSizePt, rng)` (7 ambiances), `SoundManager.Shared` (`PlaySelect`, `PlayConnect(value, tension)`, `PlayRejected`, `PlayBacktrack`, `PlayCombo(length)`, `CancelPath`, `PlayWin`, `PlayLose`) et `SoundManager.Bind(settings)`, `HapticManager` (`Light/Medium/Heavy`, `Impact`, `Chain(length)`, `Bind(settings)`), `JuiceSettings.Bind(settings)`.
  - Libellé de gain, logo « TEN • GO » : voir `Showcase/Vitrine` (reprend `GameScene.updatePathGainLabel` et `setupUI`).
- **API pour l'étape 6 (écrans)** : `UiButton.Create`/`CreateRound` (relief, libellé ou icône, `Hit(world)`, `Tap()` avec `TapFeedback`), `Relief.Raise(shape, depth)`, `Relief.Surface`, `Relief.IconChip`, `ShapeView` (rectangle arrondi, ellipse, disque doux, anneau ; SDF), `StrokeView`, `VectorIcon.Create(parent, VectorIconName.X, pos, sizePt, color, order)`, `CoinIcon.Make`, `SeedArt` (pousse, fleur, horloge, pastille ×N, pulsation), `SceneFader.Fade(root, size, swap)` (0,28 s par la couleur du fond), `TenGoText.Create(parent, text, weight, sizePt, color, pos, order)` et `Fonts` (`AvenirMedium/DemiBold/Bold/Heavy` → graisses Nunito, `ApplyLanguage`).
- **Shaders** (`Resources/Shaders`, matériaux créés par `ProjectSetup` dans `Resources/Materials`) : `Shape`, `Trail` (polyligne ≤ 32 points, tirets), `Wave`, `IconSdf`, `Dots` (particules en un seul maillage). Aucune texture image.
- **Icônes** : les 13 SVG d'`ios/tenGO/Assets.xcassets` sont recopiés dans `IconLibrary.Data.cs` (le test `EmbeddedSvgMatchIosCatalog` signale toute divergence), lus par `SvgPath` (M L H V C S Q A Z, relatifs, `polyline`, `line`), puis convertis à l'exécution en champ de distance 128 × 128 (≈ 10 ms par icône, en cache). Raison : ce ne sont que des traits ronds de 2 unités ; la distance exacte reste nette à toute densité, se teinte et suit l'alpha sans recouvrements visibles, ce que ni un maillage de traits ni une image figée ne permettent.
- **Polices** : replis Noto Sans JP, KR, SC (OFL 1.1, `Assets/Fonts/LICENSES.md`), instance Bold réduite aux caractères ja/ko/zh-Hans de `Localizable.xcstrings` par `Assets/Fonts/Tools~/build_cjk_fallbacks.py` (~400 Ko chacune) : **à relancer quand les textes changent**. Chaînés derrière chaque graisse Nunito et dans les réglages TMP ; jeu `Assets/Fonts/Resources/TenGoFonts.asset`.
- **Audio** (`Scripts/Audio`) : synthé temps réel 8 voix (`SynthEngine`, 85 % sinus + 15 % triangle, ADSR longue et courte, passe-bas 1 kHz, réverbération Schroeder 30 %, master 0,5), notes différées à l'échantillon près dans le moteur, ce qui permet l'export hors ligne. `SoundBoard` reprend la table de `SoundManager.swift` (throttle 40 ms, égrenage 25 ms, accords). Sortie par `OnAudioFilterRead` d'une `AudioSource` cachée. Export : `Tools/unity.sh TenGo.Editor.AudioExport.All` → `Logs/audio/*.wav` + `report.txt` (crêtes de −25 à −9 dBFS, −0,9 dBFS pour 8 notes à pleine vélocité).
- **Haptique** : `IHaptics` (`Light/Medium/Heavy/Soft/Rigid` + intensité, notifications), `IosHaptics` (`Plugins/iOS/TenGoHaptics.mm`, générateurs UIKit), `AndroidHaptics` (`VibrationEffect.createOneShot`, durée et amplitude par style), `NullHaptics` en éditeur (enregistre les appels). `Platform/ReduceMotion` : `UIAccessibilityIsReduceMotionEnabled` (même greffon) et `animator_duration_scale == 0` sur Android.
- **Vitrine et captures** : `Showcase/Vitrine` (scène `GlowDemo` via `VitrineRunner`), `Tools/unity.sh TenGo.Editor.Captures.Step4` → `Logs/captures/step4_grid.png` (1320 × 2868), `step4_theme_<id>.png` (7 thèmes), `step4_materials.png`, `step4_effects_<ms>.png` (50, 120, 300, 600, 1200 ms), `step4_ui.png`. Comparées au simulateur iOS (iPhone 17 Pro Max, `SCREENSHOT_PATH=5 SCREENSHOT_FLOWER=3`) : grille, tailles, couleurs, sélection, fleur, badge, libellé de gain et comète concordent.
- **Écarts visuels connus avec iOS** :
  - Avenir Next → Nunito (chiffres en SemiBold, logo et gains en Black) : formes plus rondes, chasse un peu différente.
  - Les confettis sont des disques (iOS : texture d'émetteur `SKEmitterNode`), et l'ombre du relief est un aplat translucide comme sur iOS, pas un flou.
  - Les bulles restent plates comme sur iOS : le relief ne sert qu'aux boutons, cartes et pastilles.
  - `SetSelected(false)` remet le filet assombri de 1,5 pt quelle que soit la matière, exactement comme `BubbleNode.setSelected` (le filet néon ou verre ne revient pas) : fidèle, à corriger des deux côtés si besoin.
  - Les fonds animés n'ont été comparés que sur le thème par défaut (les autres thèmes demandent de naviguer dans l'app iOS, sans pilotage souris).
- **Tests** : 26 nouveaux EditMode (`ScalesTests` portés de Kotlin, `SynthTests`, `FxTests`, `IconTests`), soit **171**, et 9 PlayMode (`RenderingTests`), soit **10** ; tous verts.
- **À vérifier sur appareil (étapes 8-9)** : greffon `TenGoHaptics.mm` dans un vrai build Xcode, amplitude Android (`hasAmplitudeControl`) et ressenti des motifs, `ReduceMotion` par JNI, latence audio (taille de tampon DSP, `AudioSettings.outputSampleRate` 48 kHz), coût des champs de distance et des maillages de particules sur un appareil d'entrée de gamme, rendu des replis CJK dans les trois langues, Gamma sur Metal/Vulkan.

## Notes de l'étape 3

- **Sauvegarde Unity** : `Platform/JsonFileSaveStore` (assembly `TenGo`), fichier `tengo_save.json` dans `Application.persistentDataPath`, format `SaveStoreJson` (Core) `{"format":1,"values":{…}}`.
  - Écriture atomique : `.tmp` vidé sur disque puis `File.Replace` (l'ancienne version passe en `.bak`).
  - Lecture : fichier principal, puis `.bak`, puis `.tmp`. Un fichier illisible est renommé en `.corrupt-<horodatage>`. Si rien n'est lisible, on repart à vide, ce qui relance la reprise des données natives.
  - Écriture sur `Flush()` seulement (`Dirty` indique s'il reste des changements). **Étape 5** : appeler `Flush()` en fin de partie, sur `OnApplicationPause(true)` et à la fermeture.
  - Jamais de `PlayerPrefs` pour nos données : sur iOS, ils vivent dans les mêmes `standardUserDefaults` que l'app native.
- **Reprise** (`Core/Legacy`, sans UnityEngine) :
  - `LegacyMigration.Run` passe une seule fois. Il pose le marqueur `unity_migratedFromNative`, avec `…Version` (`Application.version`) et `…Source` (`ios` ou `android`), y compris quand il n'y a rien à reprendre.
  - Il ne touche jamais aux données natives : lecture seule, aucun effacement, donc retour arrière possible vers la 3.3.0.
  - Il ne remplace jamais une valeur Unity déjà présente.
  - Il journalise un bilan qui ne cite que des noms de clés, jamais leurs valeurs.
  - Une lecture qui échoue est retentée au lancement suivant, au plus 3 fois (`unity_migrationAttempts`), puis abandonnée.
- **Point d'entrée** : `Platform/Legacy/LegacyBootstrap.Run(store)`, à appeler au démarrage (étape 5), juste après `JsonFileSaveStore.OpenDefault()` et **avant** tout dépôt qui écrit une valeur par défaut (`FirstRunState.Bootstrap`…). En éditeur, la variable `TENGO_LEGACY_FIXTURE` peut désigner un dossier de fixtures (disposition Android ou `ios.json`).
- **Schéma cible** : `LegacySchema` liste les 50 clés iOS et les 3 préfixes, avec leur type. Toute valeur y est ramenée (booléen stocké en entier, nombre en chaîne, `Data` JSON vers une chaîne validée, `NSDate` vers des secondes Swift).
- **iOS** :
  - `Plugins/iOS/TenGoLegacy.mm` lit le domaine persistant de l'app (`persistentDomainForName:bundleIdentifier`), limité aux clés et préfixes demandés (`IosLegacyImporter.RequestJson`). Il renvoie un JSON typé : `bool` (CFBoolean), `int` ou `double` (selon `objCType`), `date`, `data` (UTF-8), `bytes` (base64), `array`.
  - Si la sérialisation échoue, il renvoie un JSON invalide exprès : la lecture compte alors comme un échec à retenter, et non comme « aucune donnée ».
  - Le greffon est compilé avec `-fobjc-arc` (meta), mais il fonctionne aussi sans ARC.
  - Il a été vérifié sous macOS avec un banc d'essai : sa sortie est identique aux fixtures, et les clés étrangères sont exclues.
- **Android** : `AndroidLegacyPaths` obtient par JNI `getFilesDir()` et `getDataDir()/shared_prefs`. `Application.persistentDataPath` n'est pas le dossier interne `files/`. `AndroidFilesLegacySource` lit ensuite les trois fichiers :
  - `files/datastore/tengo.preferences_pb` ;
  - `shared_prefs/tengo_first_run.xml` ;
  - `shared_prefs/tengo_ads.xml`.
- **Décodeurs** :
  - `DataStorePreferencesDecoder` décode le protobuf `PreferenceMap` (champs 1 à 8 : booléen, float, int32, int64, chaîne, ensemble de chaînes, double, octets). Le schéma a été vérifié sur `datastore-preferences-proto` 1.1.7.
  - `SharedPreferencesXmlDecoder` lit `int`, `long`, `float`, `boolean`, `string`, `set` et `null`, avec résolution des entités.
- **Correspondance Android → iOS** (`AndroidLegacyImporter.Rules`, chaque clé Kotlin a une règle, test `EveryKotlinKeyHasARule`) :

| Clé Kotlin | Conversion |
|---|---|
| Réglages, tutoriel, guide boutique, pièces, XP, stats, missions, défi du jour, boucliers, paliers, boosters `tenGO_booster_*`, coach-marks `tenGO_boosterCoachSeen_*`, étoiles `tenGO_puzzleStars_*`, thème/style/tracé actifs, `tenGO_noAdsPurchased`, `tenGO_seedFlowerExplained`, notifications | identique (mêmes noms, mêmes types) |
| `tengo_high_scores`, `tengo_rush_high_scores` (CSV) | tableau d'entiers |
| `tenGO_duelHistory` (CSV) | tableau de chaînes (vides retirées, ordre gardé) |
| `tenGO_achievementsUnlocked`, `tenGO_ownedThemes`, `tenGO_ownedBubbleStyles`, `tenGO_ownedTrails` (ensembles) | tableaux triés |
| `tenGO_streakLastPlayed` (jour epoch en chaîne) | minuit local de ce jour, en secondes Swift (fuseau de l'appareil) |
| `tenGO_mirrorData` (une clé, ms) | `tenGO_mirrorRecords` + `tenGO_mirrorPartitions` (secondes Swift) |
| `tenGO_seedGarden` `{plantedAtMs, chain, bloomAtMs}` | `{plantedAt, chain, bloomAt}` (secondes Swift) |
| `tengo_saved_game` (avec `id` par bulle et `mode`) | JSON iOS (relu puis réécrit par `SavedGame`) ; un mode autre que `normal` est ignoré |
| `tenGO_rewardedDayKey`, `tenGO_rewardedCount` | `tenGO_rewardedDay`, `tenGO_rewardedDayCount` |
| `tengo_first_run.xml` : `tenGO_firstOpenAt` (ms) | secondes Swift ; état et `firstGameStartLogged` identiques |
| `tengo_ads.xml` : `AdMob.interstitialLastShownAt` (ms, 0 = jamais) | secondes Swift, omis si 0 ; `interstitialsTodayDay` entier → chaîne `AAAAMMJJ` ; compteurs identiques |

- **Clés ignorées** :
  - `tenGO_noAdsBonusGranted` : Kotlin note « bonus déjà versé », alors qu'iOS ne garde qu'un drapeau « bonus en attente » (achat différé). Sur Android, le bonus est versé avec l'achat, donc il n'y a jamais rien en attente à reprendre.
  - Côté iOS, toutes les clés hors schéma (consentement `IABTCF_*`, Firebase, `PlayerPrefs`) sont ignorées : UMP et Firebase relisent eux-mêmes leurs données natives.
  - Toute clé inconnue est ignorée et signalée dans le bilan.
- **Fixtures** (`Assets/Tests/EditMode/Fixtures/Legacy`) :
  - `android/emulator` : **réelles**. App Android 3.3.0, variante `qa` (`com.appcraft31.tengo.test`), sur emulator-5554, avec `tengo_seed`, `tengo_qa_mirror` et `tengo_qa_seed`, puis des parties terminées, une vidéo « Continuer » et une partie laissée en cours. Récupérées par `run-as`. L'app a ensuite été désinstallée.
  - `android/complete` : le DataStore est **fabriqué avec les classes protobuf de DataStore 1.1.7** (petit programme Java hors dépôt) et contient toutes les clés de `Prefs.kt`, plus des clés `zz_*` pour chaque type protobuf. Les XML sont **écrits à la main** au format Android (entités, `set`, `float`, `null`).
  - `ios/lived.json` : **réel**. UserDefaults de l'app iOS (build 281) déjà installée sur le simulateur iPhone 16 Pro Max, lus sans y toucher.
  - `ios/seeded.json` : **réel**. Même build installé sur le simulateur iPhone 16e et lancé avec `SCREENSHOT_SEED_DATA=1`, `QA_MIRROR=1` et `QA_SEED=grow:7`, puis désinstallé.
  - Les deux fixtures iOS ont été converties du plist au format du greffon.
  - Aucune donnée personnelle dans les fixtures.
- **Tests** : 28 nouveaux tests EditMode, soit **145** en tout, tous verts (PlayMode : 1).
  - Décodage protobuf et XML.
  - Conversion Android, puis relecture par les dépôts de Core : pièces, boosters, XP, série, stats, succès, missions, puzzles, duels, thèmes, cosmétiques, miroir, graine, partie en cours, réglages, premier lancement, pubs.
  - Import iOS et normalisation des types.
  - Idempotence, non-écrasement, absence de données natives, échecs retentés puis abandonnés.
  - Fichiers natifs inchangés, journal sans valeurs.
  - Fichier de sauvegarde corrompu ou interrompu.
- **Reste à vérifier sur appareil (étape 9)**, en installant la 4.0.0 par-dessus une 3.3.0 qui a des données :
  - iOS : le greffon dans un vrai build Xcode (ARC, `__Internal`, libération de la chaîne par le marshalling IL2CPP) ; `persistentDomainForName` sur l'appareil.
  - Android : l'appel JNI (`getFilesDir`, `getDataDir`) sous IL2CPP. Les branches `UNITY_ANDROID` et `UNITY_IOS` n'ont pas été compilées ici (cible éditeur seulement) : à compiler au premier build de l'étape 8.
  - Android : vérifier que `getFilesDir()` d'Unity est bien `/data/data/com.appcraft31.tengo/files`.
  - Firebase (Auth anonyme des duels, Analytics), UMP et Play Games / Game Center : les SDK Unity reposent sur les SDK natifs et retrouvent leurs propres données. À confirmer à l'étape 7.
  - Retour arrière : réinstaller la 3.3.0 après la 4.0.0 doit retrouver les données natives intactes.

## Notes de l'étape 5

- **Architecture** (`Assets/Scripts/App`, `Game`, `Services`) :
  - Scène unique `Assets/Scenes/Main.unity`, reconstruite par `Tools/unity.sh TenGo.Editor.MainScene.Ensure` (seule scène du build). Elle contient une caméra et un `AppRoot`.
  - `AppRoot.Boot` : `JsonFileSaveStore.OpenDefault()`, puis `LegacyBootstrap.Run`, puis les textes (`Loc`, langue de l'appareil), les dépôts et services (`AppServices`), `FirstRunState.Bootstrap()`, le branchement des réglages (son, haptique, effets réduits) et le thème actif (`Look`). `FlushSave()` en fin de partie, au retour au menu, sur `OnApplicationPause(true)`, à la fermeture et à la destruction.
  - Caméra : cadrage aspectFill de 750 × 1334 (`SceneGeometry`), marges sûres en unités de scène avec le plancher de 47 pt d'iOS. L'entrée (`PointerInput` : EnhancedTouch, souris en éditeur, injection) convertit les pixels en points de scène sans passer par la caméra, si bien que les secousses ne déplacent pas les zones tactiles.
  - Navigation : `IScreenRouter` (fondu de 0,28 s, `SceneFader`) et `IAppScreen` pour chaque écran.
  - `GameController` porte `GameScene.swift` en fichiers partiels, qui suivent ses `MARK` : Layout, Input, Path, Boosters, End, Panel, Flow. Ce sont les mêmes constantes, tolérances et délais, et les mêmes priorités au toucher que `touchesBegan`.
  - Toutes les séquences passent par `Tw` sur la racine de l'écran, donc par `FxTime`, et l'arrêt sur image les ralentit comme le `scene.speed` d'iOS. Le chrono du Rush, l'inactivité avant l'offre d'indice et les mesures du miroir suivent `FxTime.RealNow`.
  - Fin de partie : un seul bilan par fin, garanti par `_endRecorded`, via `GameEndCoordinator.OnGameEnded`. Après « Continuer », la fin suivante passe par `ContinuedFrom`.
  - Textes : `Tools/i18n/gen_strings.py` convertit `ios/tenGO/Localizable.xcstrings` en `Resources/Localization/strings.json` (291 clés, 10 langues, `%d`/`%@` deviennent `{0}`). `Core/Localization/Loc` les sert.
- **Interface prévue pour l'étape 6** :
  - remplacer `AppRoot.Screens`, une `IScreenFactory` : `Menu`, `PuzzleLevels(world)`, `Duel`, `Shop(returnToGame)`, `Tutorial(firstRun)` ;
  - remplacer `AppRoot.Overlays`, une `IOverlayFactory` : `Settings(app, parent, replayTutorial)` qui renvoie un `IGameOverlay` ;
  - chaque écran implémente `IAppScreen` ; `SimpleScreen` sert de base aux écrans faits de boutons ;
  - pour lancer une partie : `app.StartGame(GameLaunch.Normal(saved) | ForDaily() | Rush() | Puzzle(w, i) | Duel(seed, code) | Demo(seed, …))`.
  - En attendant, `ProvisionalScreens` affiche un menu de développement (un bouton par mode et « Continuer ») et des écrans d'attente : une boutique qui vend les boosters, une liste de 6 niveaux, la création et la réponse à un duel.
- **Simulé en attendant l'étape 7** (`Services/`, interfaces appelées aux mêmes moments que Swift ; la politique de Core décide) :
  - analytics : `LogAnalytics` journalise avec les noms et paramètres d'iOS ;
  - interstitielles : `SimulatedInterstitialAds`, enveloppées par `InterstitialAdManager` avec `InterstitialPolicy` ;
  - vidéos récompensées : `SimulatedRewardedAds`, enveloppées par `RewardedAdManager` avec `RewardedPolicy` ;
  - duels : `SimulatedDuelBackend`, avec des codes au format iOS et l'historique local ;
  - classements, demande d'avis, notifications, partage, programmation de l'éclosion : `LogPlayServices` ;
  - achat « sans pub » : `SimulatedAdFreeStore`.
- **Pilote automatique** : `Tools/unity.sh TenGo.Editor.Autopilot.Run`.
  - Il joue chaque mode avec des gestes injectés et les chemins du solveur, sur l'écran simulé d'un iPhone 17 Pro Max.
  - Pour le puzzle, il prend une solution complète trouvée par `BoardSolver`.
  - Captures dans `Logs/captures/play/`.
  - Les atlas de polices sont vidés à la fin.
- **Comparaison avec iOS** (iPhone simulé, partie normale et fin de partie `SCREENSHOT_END`) : disposition « rangée haute », carte de score, logo, grille, barre du bas et panneau de fin se superposent à quelques points près. Écarts corrigés au passage :
  - format de la durée de la graine (« 3h 59min ») ;
  - code de duel arrivé avant l'ouverture du panneau ;
  - cadre du coach-mark gonflé par le halo d'apparition.
- **Écarts restants avec iOS** :
  - Glyphes absents de Nunito et des replis : ⌂, ↺, ★/☆ et l'engrenage SF Symbol sont redessinés en traits (`Glyphs`) et ressemblent aux originaux sans les reproduire. Les flèches « ↑ » et « → » du miroir et de la graine sont supprimées.
  - Avenir Next est remplacée par Nunito (étape 1) : le score de la carte et les titres sont un peu moins gras.
  - La pointe du coach-mark est un losange à demi caché, au lieu d'un triangle.
  - La partie normale est aussi sauvegardée sur `OnApplicationPause` (comme Android) ; iOS ne la sauvegardait qu'au retour à l'accueil.
  - La « fin de partie » QA (`QA_GAME_OVER`, `SCREENSHOT_*`) devient des méthodes publiques `QaForceGameOver`, `QaShowcasePath` et `QaEndRush`, sans variables d'environnement.
  - Les réglages en partie sont provisoires (son, vibrations, tutoriel).
- **Tests** : 14 nouveaux PlayMode (`GameFlowTests`), qui couvrent :
  - la validation, l'annulation, le refus d'une bulle de trop et le retour en arrière ;
  - la gravité, avec la cohérence entre vues et modèle ;
  - les trois boosters et leurs coach-marks ;
  - la sauvegarde puis la reprise à l'identique ;
  - une partie complète par mode, dont le Rush qui expire seul ;
  - la seconde chance sans double comptage ;
  - un bilan unique par fin de partie.
  Au total : **171** EditMode et **24** PlayMode, tous verts.
- **À vérifier sur appareil (étapes 8-9)** : la conversion pixels → points (`Screen.dpi`) et les marges sûres sur des Android variés, la latence du toucher, le bouton retour d'Android (Échap en éditeur), et la reprise après mise en arrière-plan en plein Rush (un écart de plus d'une seconde est ignoré, comme iOS).
