# Générateur de screenshots stores (fiche 2026-07)

Pipeline : captures simulateur localisées → panneaux HTML (DA du jeu, hooks ASO)
→ rendu Chrome headless → distribution fastlane.

1. `capture_langs.sh` — capture menu/jeu/défi/boutique dans les 10 langues
   (`-AppleLanguages`), barre d'état 9:41, recadrage de la bannière de test.
   Prérequis : app buildée installée sur le simulateur (UDID en tête de script),
   `defaults write … tenGO_noAdsPurchased -bool true` pour une boutique sans prix.
2. `gen_screens.py` — 6 panneaux × 10 langues × 3 formats :
   iPhone 6,5" 1242×2688, iPad 12,9" 2048×2732 (ASC), 1080×2160 (Play, ≤ 2:1).
   Textes/hooks traduits dans le dict `T` du script.
3. `deploy_screens.py` — copie vers `ios/fastlane/screenshots/<locale>/` (15 locales)
   et `android/fastlane/metadata/android/<locale>/images/…` (téléphone + 7"/10").
4. Upload : `cd ios && fastlane upload_screenshots` (crée/complète la version ASC,
   la version vient du projet) ; `cd android && fastlane shots` (fiche Play).

`asc_locales.py` — audit API ASC (JWT ES256 via openssl) : localisations d'une
version + comptage des screenshots par set. Utile après un deliver interrompu
(voir aussi le réordonnancement des sets dans l'historique du 2026-07-16).
