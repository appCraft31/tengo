# Générateur de screenshots stores (fiche 3.0)

Pipeline : captures simulateur localisées → panneaux HTML (DA du jeu, hooks ASO)
→ rendu Chrome headless → distribution fastlane.

Les trois scripts partagent le dossier `render/` (ignoré par git). Ils
pointaient auparavant vers **trois chemins différents**, dont un scratchpad de
session éphémère — plus rien ne se régénérait.

1. `capture_langs.sh` — capture 6 écrans (menu, partie, défi, boutique, duel,
   profil) dans les 10 langues (`-AppleLanguages`).
   Prérequis : app buildée et installée sur le simulateur.
   `UDID=xxx ./capture_langs.sh` pour cibler un simulateur ; sans variable, le
   premier simulateur démarré est utilisé.
   Les captures sont prises **en pleine hauteur** : la bannière publicitaire
   ayant été supprimée du jeu (commit `3f62595`), l'ancien recadrage
   `crop=1206:2340` amputait la barre d'onglets.
2. `gen_screens.py` — 8 panneaux × 10 langues × 3 formats :
   iPhone 6,5" 1242×2688, iPad 12,9" 2048×2732 (ASC), 1080×2160 (Play, ≤ 2:1).
   Textes/hooks traduits dans le dict `T`. Rendu unitaire :
   `gen_screens.py 1 fr phone`.
3. `deploy_screens.py` — copie vers `ios/fastlane/screenshots/<locale>/`
   (15 locales ASC).
   **La distribution Play est désactivée par défaut** : l'app Android est en
   1.4.1 et n'a ni Duel, ni Rush, ni Profil — y pousser ces captures décrirait
   une app qui n'existe pas. `PLAY=1 python3 deploy_screens.py` pour la
   réactiver le jour où le portage aura rattrapé.
4. Upload : `cd ios && fastlane upload_screenshots` (la version vient du projet).

## Les 8 panneaux

| # | écran source | angle |
|---|---|---|
| 1 | menu | identité — « Relie. Additionne. Respire. » |
| 2 | partie | la règle en une phrase |
| 3 | partie | les chaînes longues rapportent bien plus |
| 4 | partie | chaque combo joue sa mélodie |
| 5 | défi du jour | une grille, la même pour tous |
| 6 | boutique | thèmes et personnalisation |
| 7 | duel | défier un ami sur la même grille |
| 8 | profil | progression, niveaux, succès |

Le panneau 3 portait jusqu'ici une liste « Pas de chronomètre / Pas de vies… »
devenue **fausse** avec le mode Rush. Il vend désormais le barème de score.

## Données affichées

`SCREENSHOT_SEED_DATA=1` (posé par `capture_langs.sh`) remplit des valeurs de
vitrine plausibles — niveau 25, série de 12, meilleurs scores, statistiques.
Sans elles le Profil affiche « Meilleur score 0 ». Ces valeurs ne sont écrites
que sous ce drapeau.

`SCREENSHOT_SCENE=menu|profile|duel|puzzles` ouvre directement un écran **et**
coupe la feuille « Se connecter à Game Center », qui se superposait aux
captures.

`asc_locales.py` — audit API ASC (JWT ES256 via openssl) : localisations d'une
version + comptage des screenshots par set. Utile après un deliver interrompu.
