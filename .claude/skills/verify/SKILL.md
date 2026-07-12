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

- `xcrun simctl io $UD screenshot out.png` pour observer.
- Les clics AppleScript `System Events click at` ne marchent PAS sur le contenu iOS ; utiliser **cliclick** (installé via brew) : `cliclick c:X,Y` après avoir activé Simulator et fait AXRaise sur la bonne fenêtre.
- Mapping coordonnées : `tell process "Simulator" to get {position, size} of group 1 of window "<nom>"` → le groupe est l'écran du device à l'échelle 1 pt = 1 px ; point écran = position groupe + point logique device (px/3).
- Les alertes système (ATT) exposent parfois leurs boutons en AX : `perform action "AXPress"` sur l'AXButton par `description`. Le contenu des sheets (Game Center, UMP) n'est PAS exposé → cliclick.

## Pièges

- Le prompt ATT ne se re-déclenche pas après désinstallation ; il faut un simulateur vierge (« Erase All Content and Settings ») pour retester le premier lancement.
- Un simulateur non connecté à Game Center affiche une sheet « Se connecter à Game Center » au lancement qui masque le flux de consentement — l'annuler via cliclick (bouton Annuler en haut à gauche).
- En DEBUG, la géographie UMP est forcée EEA (`ConsentManager.swift`), donc le formulaire GDPR apparaît toujours si ATT accepté.
- Mode QA sans consentement : `SIMCTL_CHILD_GAME_NORMAL=1 xcrun simctl launch ...` charge la bannière directement (non personnalisée).

## Ce qu'il faut observer (flux consentement)

1. ATT refusé → aucun formulaire cookies, log `[AdMob] Pas de consentement exploitable — pas de pub` (EEA) ; Firebase loggue « Ad storage consent is denied ».
2. ATT accepté → formulaire UMP ensuite, puis `[AdMob] Démarrage pubs — mode personnalisé` + bannière/interstitiel/rewarded chargés.
3. Relance → aucun prompt, mode restauré.
4. Paramètres → « Options de confidentialité » rouvre le formulaire UMP (si requis).
