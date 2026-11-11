# tenGO (Unity)

- Lire `README.md`, `CONVENTIONS.md` et `MIGRATION.md` avant de travailler.
- Les versions iOS (Swift) et Android (Kotlin) sont **archivées** dans `~/StudioProjects/Archives/tenGO/` (tag git `archive/ios-swift-3.3.0` du dépôt tenGO, tag `archive/android-kotlin` du dépôt tengo-android). Clés de signature et configs Firebase : `config/` (ignoré par git).
- Références de comportement, en lecture seule :
  - le jeu iOS `~/StudioProjects/Archives/tenGO/ios/tenGO/` (Swift + SpriteKit), qui fait foi ;
  - le portage Android `~/StudioProjects/Archives/tenGO/android/` (Kotlin), dont `core-game` et `app/.../progression`, déjà traduits et testés : partir de leur code et de leurs tests pour la logique pure, et vérifier chaque règle sur le Swift.
- **Parité au bit près** pour l'aléatoire et la génération des grilles (`SeededGenerator`, défi du jour, duels entre plateformes) : les vecteurs de `~/StudioProjects/Archives/tenGO/ios/tools/rng_vectors` et les tests `SwiftRngParityTest`/`DailyChallengeTest` d'Android doivent passer tels quels.
- `Assets/Scripts/Core` (`TenGo.Core`) ne doit jamais référencer `UnityEngine` : l'asmdef l'interdit (`noEngineReferences`).
- Après chaque changement : `Tools/compile_check.sh` puis `Tools/run_tests.sh`. Un commit par étape ou sous-étape, avec des tests verts, sur `develop` (dépôt parent `tenGO/`).
- Unity ne lance jamais deux instances sur le projet : passer **toujours** par `Tools/compile_check.sh`, `Tools/run_tests.sh` ou `Tools/unity.sh`, qui prennent le verrou `Logs/unity.lock`. Ne jamais appeler l'exécutable Unity directement ni supprimer un verrou dont le pid est vivant. Si l'éditeur a le projet ouvert, demander à l'utilisateur de le fermer.
- Identifiants des apps **en production** (`com.appcraft31.tengo`, `AppCraft31.tenGO`) : ne jamais installer un build de développement sur le téléphone de l'utilisateur (la version Play y tourne) ; tester sur `emulator-5554` et le simulateur iOS.
- Ne jamais committer de clés ni de secrets (dossier `config/` : `tengo-upload.jks`, `keystore.properties`, `.p8`, comptes de service, `google-services.json`, `GoogleService-Info.plist`).
