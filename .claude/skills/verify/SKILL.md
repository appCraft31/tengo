---
name: verify
description: Vérifier un changement tenGO iOS de bout en bout dans le simulateur (build, install, pilotage UI, logs de consentement).
---

# Vérifier tenGO (iOS) au simulateur

## Build

```bash
xcodebuild -workspace tenGO.xcworkspace -scheme tenGO -configuration Debug \
  -destination 'generic/platform=iOS Simulator' build
```

Produit : `~/Library/Developer/Xcode/DerivedData/tenGO-*/Build/Products/Debug-iphonesimulator/tenGO.app`.

## Install + lancement avec logs

```bash
UD=<udid>   # xcrun simctl list devices booted — attention, souvent PLUSIEURS sims démarrés : toujours cibler par UDID, jamais « booted »
xcrun simctl uninstall $UD AppCraft31.tenGO   # reset l'état UMP (pas l'ATT !)
xcrun simctl install $UD <chemin tenGO.app>
xcrun simctl launch --console-pty $UD AppCraft31.tenGO > run.log 2>&1 &
```

Les `print()` de l'app ([Consent], [AdMob], [GameCenter]) sortent sur le pty. Les logs Firebase/UMP passent par os_log :

```bash
xcrun simctl spawn $UD log show --last 5m --predicate 'process == "tenGO"' | grep -i consent
```

## Piloter l'UI

- ⚠️ **Ne JAMAIS piloter la souris (cliclick / System Events click) pendant que l'utilisateur travaille** — il voit son curseur bouger. Demander d'abord, ou passer par des voies sans souris : `simctl launch/terminate`, variables d'env QA, écriture directe dans le conteneur du simulateur.
- État ATT : stocké dans `~/Library/Developer/CoreSimulator/Devices/$UD/data/Library/TCC/TCC.db`, ligne `kTCCServiceUserTracking|<bundle>|N` (2 = refusé). Forcer N=3 ne suffit PAS à simuler l'acceptation (cache atrackingd) — pour tester « accepté », il faut répondre au prompt réel sur un sim vierge.
- `xcrun simctl io $UD screenshot out.png` pour observer.
- Les clics AppleScript `System Events click at` ne marchent PAS sur le contenu iOS ; utiliser **cliclick** (installé via brew) : `cliclick c:X,Y` après avoir activé Simulator et fait AXRaise sur la bonne fenêtre.
- Mapping coordonnées : `tell process "Simulator" to get {position, size} of group 1 of window "<nom>"` → le groupe est l'écran du device à l'échelle 1 pt = 1 px ; point écran = position groupe + point logique device (px/3).
- Les alertes système (ATT) exposent parfois leurs boutons en AX : `perform action "AXPress"` sur l'AXButton par `description`. Le contenu des sheets (Game Center, UMP) n'est PAS exposé → cliclick.

## Pièges

- Le prompt ATT ne se re-déclenche pas après désinstallation ; il faut un simulateur vierge (« Erase All Content and Settings ») pour retester le premier lancement.
- Un simulateur non connecté à Game Center affiche une sheet « Se connecter à Game Center » au lancement qui masque le flux de consentement — l'annuler via cliclick (bouton Annuler en haut à gauche).
- Mode QA sans consentement : `SIMCTL_CHILD_GAME_NORMAL=1 xcrun simctl launch ...` charge la bannière directement (non personnalisée).

## Ce qu'il faut observer (flux consentement, ATT-only depuis le retrait d'UMP)

1. ATT refusé → aucun formulaire, log `[AdMob] Démarrage pubs — mode non personnalisé` + `Bannière chargée avec succès` ; `Pub récompensée prête` arrive APRÈS le démarrage (préchargée au menu, gated par isAdsSessionStarted).
2. ATT accepté → aucun formulaire, `[AdMob] Démarrage pubs — mode personnalisé`.
3. `[AdMob] Interstitiel prêt` ne doit PAS apparaître au lancement — seulement après la 1re partie complétée (markGameCompleted).
4. Relance → aucun prompt, mode restauré.
5. Paramètres → « Options de confidentialité » ouvre les réglages système (toggle « Autoriser le suivi »).
