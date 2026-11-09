# Migration vers Unity : avancement

Objectif : tenGO **4.0.0** en Unity, en parité stricte avec la 3.3.0 (iOS build 282, Android versionCode 16). Il remplacera les deux apps le même jour.

Plan détaillé : `~/.claude/plans/je-voudrais-basculer-tengo-giggly-owl.md`.

| # | Étape | État |
|---|---|---|
| 1 | Squelette : Unity 6000.6.8f1, URP 2D, Player Settings (identifiants des apps en ligne, 4.0.0), assemblies, verrou, polices Nunito, `GlowCircle`, scène de démo | ✅ fait |
| 2 | Core en C# : grille, aléatoire Swift, génération, score, défi du jour, puzzles, miroir, graines, progression, économie, fin de partie + tests portés de Kotlin | ✅ fait |
| 3 | Reprise des données des apps natives (UserDefaults iOS, DataStore et SharedPreferences Android) + sauvegarde fichier JSON | ✅ fait (reste la vérification sur appareil, étape 9) |
| 4 | Rendu (bulles, relief, tracés, thèmes, effets) et audio synthétisé | ✅ fait (reste la vérification sur appareil, étape 9) |
| 5 | Partie (`GameScene` : 6 modes, boosters, sauvegarde, fin de partie) | ✅ fait (services simulés jusqu'à l'étape 7, écrans hors partie livrés à l'étape 6) |
| 6 | Écrans (menu, boutique, profil, missions, succès, miroir, duel, puzzles, classement, réglages, tutoriel) | ✅ fait (achats, pubs, consentement et Game Center simulés jusqu'à l'étape 7) |
| 7 | Services : AdMob + UMP/ATT + médiation, Unity IAP, Firebase (Analytics, Auth, Firestore), Game Center / Play Games, notifications, avis, partage, haptique, liens profonds | ✅ fait (intégration locale ; vérifications sur appareil et en console listées ci-dessous) |
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

### Émulateur Android

- Lancer l'émulateur avec `-gpu host` (`emulator -avd <nom> -gpu host`) : en rendu logiciel, il ne fournit pas OpenGL ES 3.1 et Unity échoue à initialiser l'API graphique au démarrage de l'APK.
- Les réglages URP (`Assets/Settings/UniversalRP.asset`, `UniversalRenderPipelineGlobalSettings.asset`) sont réécrits par le build (élagage des variantes de shaders) : commiter ces changements avec le build.

## Notes de l'étape 6

- **Écrans** (`Assets/Scripts/Screens`, espace `TenGo.Screens`), portés de leur scène Swift :
  - `MenuScreen` (MenuScene) : niveau, pièces, réglages, pub « +10 », logo, « Jouer » et son badge de graine, « Continuer », Défi du jour (série, boucliers, prochaine récompense), tuiles Rush / Duel / Puzzles, guide du premier achat sur l'onglet Boutique ;
  - `ShopScreen` (BoutiqueScene) : page unique défilante (« Sans pub », Thèmes, Matières, Tracés, Boosters, Pièces), achat et équipement, bouton de retour depuis une partie, mode guidé (un indice acheté puis remboursé, « Plus tard ») ;
  - `ProfileScreen`, `MissionsScreen`, `AchievementsScreen`, `MirrorScreen`, `LeaderboardScreen` (onglet Progression ; briques communes dans `ProfileScreenUi.cs`) ;
  - `DuelScreen` et `DuelCodeEntryOverlay` (saisie du code dans la scène), `PuzzleLevelsScreen` ;
  - `TutorialScreen` (2 pages au premier lancement, 3 pour « Revoir le tutoriel ») et `SettingsOverlay` (aussi utilisé en partie).
  - Les écrans provisoires de l'étape 5 (`ProvisionalScreens`, `DevMenuScreen`, `PlaceholderScreen`, `SimpleScreen`) sont supprimés.
- **Socle** :
  - `ScreenBase` : cibles nommées (tests, pilote), tap iOS (`animateTap`, action au toucher sur les menus, au relâchement sur les écrans défilants), barre d'onglets (`TabBar`, onglets sans transition), surcouche prioritaire ;
  - `ScrollArea` : défilement au doigt façon UIScrollView (seuil 8 pt, inertie, rebond élastique) ; `Clipping` remplace `SKCropNode` (rectangle `_TenGoClip` dans les shaders Shape / IconSdf / Trail, `_ClipRect` pour TextMeshPro) ;
  - `UiArt` : triangle plein, coche « ✓ » redessinés.
- **Liens profonds** : `Core/Links/DeepLink` (port pur de DeepLink.swift : `tengo://` et `https://appcraft31.app/tengo/*`, tests EditMode `DeepLinkTests`), `DeepLinkInbox`. `AppRoot` lit `Application.absoluteURL` au lancement à froid et `Application.deepLinkActivated` ensuite : Défi du jour → partie, Progression → Profil, code de duel → réponse au duel.
- **Premier lancement** (ordre de GameViewController / FirstRun) : tutoriel court → partie → consentement 1,6 s après la première fin de partie, Game Center après la réponse. Joueur connu : Game Center, puis consentement 0,5 s plus tard. `IConsentFlow` (`SimulatedConsentFlow`) jusqu'à l'étape 7.
- **Simulé en attendant l'étape 7** : `ICoinStore` (`SimulatedCoinStore` : 4 packs aux prix de la capture App Store, restauration), `IAdFreeStore`, `IConsentFlow`, et dans `IPlayServices` : `Authenticate`, `ShareApp`, `OpenSupportMail`, `OpenSystemSettings`. Analytics ajoutées avec les noms d'iOS : `shop_opened`, `noads_offer_view`, `shop_guide`, `spend_virtual_currency`, `earn_virtual_currency`, `tutorial_complete`, `sound_toggled`, `mirror_opened`.
- **Captures** : `Tools/unity.sh TenGo.Editor.Captures.Screens [-langs fr,en,ja] [-themes default,night] [-only menu,shop]` produit chaque écran avec les données de vitrine d'iOS (`seedShowcaseData`) dans `Logs/captures/screens/<langue>_<thème>_<écran>.png` (29 écrans × 3 langues × 2 thèmes), puis vide les atlas de polices. Comparées aux captures du simulateur iOS (accueil, boutique, pièces, profil, duel, puzzles, miroir).
- **Hygiène** : `Tools/run_tests.sh` vide les atlas dynamiques des polices (`ProjectSetup.ClearDynamicFontData`) s'ils ont changé, pour laisser `git status` propre.
- **Tests** : 20 PlayMode (`ScreenFlowTests` : chaque onglet et retour, tuiles de l'accueil, achat et équipement d'un thème, cosmétique et booster, « sans pub » puis restauration, pack de pièces, guide d'achat remboursé et « Plus tard », retour en partie depuis la boutique, missions réclamées, succès par catégorie, duel créé et rejoint, niveau de puzzle, réglages persistés, revoir le tutoriel, premier lancement puis plus jamais, ordre du consentement, liens au lancement et app ouverte) et les EditMode `DeepLinkTests`. Au total : **196** EditMode et **44** PlayMode, tous verts.
- **Écarts restants avec iOS** :
  - Marges sûres : sur le simulateur, iOS présente ses scènes avec des marges nulles (plancher de 47 pt en haut, 20 pt en bas) ; `SceneGeometry.IPhone17ProMax` simule les vraies (62 / 34 pt). La rangée du haut est donc ~20 points plus bas et la barre d'onglets un peu plus haut que sur les captures iOS. Gardé tel quel (partagé avec la partie) : à trancher à la recette sur appareil.
  - Duel : pas de lecture serveur (`IDuelBackend` sans `fetch` ni `myDuels`) : la liste montre l'historique local « en attente », la réponse dérive la graine du code (FNV-1a), sans refus « expiré / déjà joué / ton propre duel » ni score à battre. Un code reçu par lien lance directement la partie, comme iOS.
  - Classement : les boutons Game Center sont toujours affichés (`IPlayServices` n'expose pas encore l'état de connexion).
  - Glyphes redessinés en traits (‹ › ✓ ↑ ↓ ✦ ⌫, cadenas, pictogrammes SF Symbols des réglages et des succès).
  - Listes de succès et carnet du miroir défilants au lieu d'être tronqués ou tassés ; titres des Missions et Succès à l'encre du thème sur fond sombre (gris illisible de nuit sur iOS).
  - Le changement de catégorie des succès se fait sur place (fondu), sans nouvelle scène.
  - Alerte de restauration des achats dessinée dans la scène au lieu d'un `UIAlertController`.
  - Tutoriel : la phrase « 4 + 2 + … = 10 ? » chevauche la dernière rangée de bulles, comme sur iOS (mêmes coordonnées).

## Notes de l'étape 7

- **Paquets** (versions de Binero) :
  - Google Mobile Ads 11.6.0 (OpenUPM), adaptateurs de médiation Liftoff Monetize 5.7.10 et Unity Ads 3.22.0 ;
  - Firebase 14.1.0 : App, Analytics, Auth, Firestore ;
  - Unity IAP 4.15.0, Mobile Notifications 2.5.0 ;
  - un seul EDM, `com.unity.external-dependency-manager` 2.1.0.
- **Archives Firebase hors git.** Elles pèsent environ 80 Mo, dans `Packages/tarballs/`, qui est ignoré. `Tools/fetch_packages.sh` :
  - les télécharge depuis Google et vérifie leur SHA-256 ;
  - réécrit le `package.json` de Firebase App pour qu'il dépende de l'EDM d'Unity, et non d'un second EDM (`Tools/packages/repack_firebase_app.py`).

  `unity_run`, appelé par les trois scripts du verrou, lance ce script au premier lancement sur une machine neuve.
- **Symboles** `TENGO_ADMOB`, `TENGO_FIREBASE` et `TENGO_IAP`, posés pour Android et iOS par `NativeSetup.ApplyDefines`. Le choix des services se fait dans `App/RealServices.cs` : le service réel n'est pris que si son symbole est défini **et** hors éditeur. Éditeur, tests et pilote gardent donc les services simulés.
  - Classements, avis, notifications et partage (`NativePlayServices`) sont réels sur tout appareil, quels que soient les symboles.
  - Retirer un symbole rend la version simulée sur appareil (utile pour isoler un SDK).
- **Configuration Firebase** :
  - `Editor/FirebaseConfig.Copy` copie `../ios/tenGO/GoogleService-Info.plist` et `../android/app/google-services.json` dans `Assets/` ;
  - il est appelé par `NativeSetup.Apply` (lui-même appelé par `ProjectSetup.Apply`) et avant chaque build (`NativePreBuild`) ;
  - les deux fichiers, le module `FirebaseApp.androidlib` qui en est généré et `StreamingAssets/` sont ignorés par git : **ne jamais les committer**.
- **Consentement, comme la 3.3.0 (écart assumé avec le brief « UMP puis ATT »)** :
  - **iOS : ATT seul.** Le formulaire UMP présenté après un refus ATT a valu le rejet 5.1.1(iv) de la 2.1 et a été retiré sur décision de l'utilisateur.
    - Si ATT est refusé : `PublisherPrivacyPersonalizationState.Disabled` et `npa=1` sur chaque requête.
    - Consent mode Firebase : stockage analytique accordé ; signaux publicitaires accordés seulement si ATT est autorisé.
    - Les valeurs par défaut sont refusées dans l'Info.plist.
    - « Options de confidentialité » ouvre les réglages de l'app.
  - **Android : UMP**, puis la même ligne rouvre le formulaire.
  - Dans tous les cas, `MobileAds.Initialize` n'est appelé qu'une fois le consentement recueilli (`IAdsSdk.Start`, `AppRoot.GatherConsent`).
- **Publicités** (`Services/Native/AdMobAds.cs`, identifiants dans `AdUnits`) :
  - **cadence** : elle reste dans `InterstitialPolicy` (Core) ;
  - **chargement** : paresseux, après une fin de partie ;
  - **échec** : nouvel essai au bout de 30 s, délai doublé à chaque échec jusqu'à 10 min ;
  - **expiration** : annonce jetée au bout de 55 min ;
  - **vidéo récompensée** : interstitiel récompensé sur iOS, récompensée simple sur Android (formats des blocs natifs) ;
  - **récompense** : versée à la fermeture ;
  - **builds de développement** : blocs de test de Google ;
  - **analytics** : `ad_impression` sur chaque revenu ;
  - **fil d'exécution** : les rappels sont ramenés sur le fil d'Unity par `MainThread`.
- **Analytics** :
  - `Core/Analytics/AnalyticsCatalog` reprend tous les événements de Swift 3.3.0, avec les mêmes noms, paramètres et types (entiers en `long`).
  - `EventAnalytics` les envoie, avec deux implémentations : `LogAnalytics` dans l'éditeur, `FirebaseAnalyticsService` sur appareil.
  - Avant l'initialisation de Firebase, les événements sont mis en attente (100 au plus).
  - Propriétés envoyées :
    - `build_channel` : development, testflight ou production ;
    - `sound_on` à chaque lancement ;
    - `first_game_done` après la première partie.
  - Collecte selon le canal :
    - iOS : les builds de développement et TestFlight passent en mode debug (DebugView) ;
    - Android : une build de développement n'envoie rien.
- **Duels Firestore** (`FirestoreDuelBackend`, format décrit dans `DuelCodec`) :
  - **compatibilité** : document `duels/{code}` au champ près celui de DuelService.swift ; un duel lancé sur une app native se joue sur Unity et inversement ;
  - **connexion** : anonyme, paresseuse ;
  - **refus côté client**, dans l'ordre natif (`DuelRules.JoinRefusal`) : expiré, déjà joué, propre duel ;
  - **écran Duel** : affiche le score à battre, joue la graine du serveur et recharge la liste depuis le serveur (gagné, perdu, égalité, expiré, en attente ; partage d'un duel resté sans adversaire) ;
  - **éditeur** : le repli local par code (`LocalSeedForCode`) a disparu ; `SimulatedDuelBackend` applique les mêmes refus.
- **Achats** (`UnityIapStore` + `StoreFlow`, logique pure testée) :
  - **produits** : packs de pièces et mod « sans pub » (+500 pièces) ;
  - **achat en attente** (contrôle parental, SCA) : le bonus est versé à l'approbation ;
  - **transaction reçue hors achat** : `iap_success` avec `source=transaction_update` ;
  - **restauration** : iOS et Android, sans analytics ;
  - **source de vérité** : le reçu présent au démarrage active le mod ; le cache est collant, jamais dégradé localement.
- **Plateforme** (`NativePlayServices`) :
  - **iOS** : Game Center par l'API Social d'Unity, dépréciée mais sans plugin, comme Binero ; boutons de classement affichés une fois connecté (`IPlayServices.ShowsLeaderboardButtons` et `SignInChanged`) ; avis par `RequestStoreReview`.
  - **Android** : Play Games Services v2 et Play In-App Review par JNI (`PlayGamesBridge`). Les paquets `play-services-games-v2` 20.1.2 et `review` 2.0.2 sont déclarés dans `Editor/TenGoDependencies.xml`.
    - Le plugin `com.google.play.games` n'a pas été retenu : il impose un second EDM et sa propre configuration.
    - Son id de projet est la ressource `game_services_project_id` de `Plugins/Android/TenGo.androidlib`.
  - **Notifications locales** (`Core/Notifications/NotificationPlan`, testé) :
    - mêmes identifiants qu'iOS : `tenGO.dailyAvailable` à 9 h (répétée), `tenGO.streakAtRisk` à 20 h, `tenGO.seedBloom` entre 9 h et 21 h ;
    - permission demandée à la 3ᵉ partie (POST_NOTIFICATIONS sur Android 13 et plus) ;
    - purge de l'ancien `tenGO.dailyReminder` sur iOS ;
    - annonce d'éclosion annulée quand la fleur est cueillie ;
    - canal Android `tengo_daily`.
  - **Partage, support, haptique** : partage natif (`TenGoNative.mm` sur iOS, `ACTION_SEND` sur Android) ; mail au support avec version et système. L'haptique était déjà native sur les deux plateformes depuis l'étape 4.
- **Builds** :
  - **iOS** : `Editor/IosPostprocess.cs` règle dans le projet Xcode :
    - Info.plist : texte ATT en 10 langues (`InfoPlist.strings`), 50 SKAdNetworkItems tirés de l'Info.plist natif (`Editor/SKAdNetworkIds.txt`), `GADApplicationIdentifier`, consent mode par défaut, schéma `tengo`, ATS, `CFBundleLocalizations`, orientations iPad ;
    - entitlements : Game Center et domaines associés ;
    - frameworks : GameKit et AppTrackingTransparency.
  - **Android** :
    - `Plugins/Android/TenGo.androidlib` apporte les permissions et l'id Play Games ;
    - le plugin GMA ajoute l'id AdMob ;
    - `Editor/AndroidPostprocess.cs` ajoute les intent-filters `tengo://` et `https://appcraft31.app/tengo` ;
    - l'EDM écrit les dépendances dans les gabarits Gradle de `Plugins/Android`.
  - **Commandes** :
    - `Tools/unity.sh TenGo.Editor.BuildAndroidDev.Run -buildTarget Android` → APK `com.appcraft31.tengo.unitydev` ;
    - `Tools/unity.sh TenGo.Editor.BuildIos.Run -buildTarget iOS` → `Builds/iOS` : Podfile pour AdMob et la médiation, puis `pod install` lancé par l'EDM ; Firebase iOS 14 est ajouté au projet en Swift Package.
- **Vérifié sur émulateur** (emulator-5554, APK de dev, désinstallé ensuite) :
  - lancement sans erreur ;
  - Firebase prêt ;
  - connexion anonyme réussie : un utilisateur anonyme a été créé dans tengo-b62b1 ;
  - lien `tengo://duel/ABCDEF` : écran Duel, puis « Code introuvable » après lecture Firestore ;
  - IAP indisponible sur cet émulateur sans Play Store (attendu).
- **Reste à vérifier sur appareil ou en console** (étapes 8 et 9) :
  - **Comptes de test** :
    - achats réels en bac à sable (testeur de licence Play, Sandbox / TestFlight) ;
    - restauration ;
    - achat différé.
  - **Consentement et pubs** :
    - formulaire UMP sur un appareil européen ;
    - boîte ATT sur iPhone ;
    - affichage et revenu des annonces de test ;
    - médiation Liftoff / Unity Ads.
  - **Identifiants et domaines** :
    - Play Games : connexion avec le vrai paquet `com.appcraft31.tengo` signé avec la clé d'envoi (le paquet unitydev n'est pas déclaré dans la console Play Games) ;
    - Game Center ;
    - liens universels : `assetlinks.json` du domaine pour l'empreinte de la clé Unity, `apple-app-site-association` déjà en place pour l'app iOS.
  - **Mise à jour par-dessus la 3.3.0** :
    - les tâches WorkManager Kotlin des notifications doivent disparaître avec la mise à jour (à confirmer) ;
    - notifications iOS déjà programmées par l'app native (mêmes identifiants, donc remplacées) ;
    - reprise de l'uid Firebase anonyme : les SDK natifs gardent leur trousseau ou leurs préférences, à confirmer.
  - **Firebase sur l'APK de dev** : le paquet unitydev n'est pas déclaré dans google-services.json. Firebase s'y initialise avec l'app Android de production, mais une build de développement n'envoie aucune analytics.

