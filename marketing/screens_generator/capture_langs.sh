#!/bin/bash
# Capture les écrans du jeu dans chaque langue (UI localisée), pour la fiche
# des stores. Les captures atterrissent dans ./render/, là où gen_screens.py
# les lit — les trois scripts du pipeline partagent ce dossier.
#
# Prérequis : l'app buildée et installée sur le simulateur cible.
# Usage : [UDID=xxx] ./capture_langs.sh
set -e

S="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
R="$S/render"
mkdir -p "$R"

# UDID : celui passé en variable, sinon le premier simulateur démarré.
UD="${UDID:-$(xcrun simctl list devices booted -j \
     | python3 -c 'import json,sys;d=json.load(sys.stdin)["devices"];print(next(x["udid"] for v in d.values() for x in v))')}"
echo "simulateur : $UD"

# menu, profile et duel passent par SCREENSHOT_SCENE : il ouvre l'écran voulu
# ET coupe la feuille Game Center, qui se superposait aux captures.
for LANGCODE in fr en de es it ja ko nl pt-BR zh-Hans; do
  for MODE in menu game daily shop duel profile; do
    xcrun simctl terminate "$UD" AppCraft31.tenGO 2>/dev/null || true
    unset SIMCTL_CHILD_GAME_NORMAL SIMCTL_CHILD_SCREENSHOT_DAILY \
          SIMCTL_CHILD_SHOP_MODE SIMCTL_CHILD_SCREENSHOT_SCENE
    export SIMCTL_CHILD_SCREENSHOT_SEED_DATA=1
    case $MODE in
      menu)    export SIMCTL_CHILD_SCREENSHOT_SCENE=menu ;;
      game)    export SIMCTL_CHILD_GAME_NORMAL=1 ;;
      daily)   export SIMCTL_CHILD_SCREENSHOT_DAILY=1 ;;
      shop)    export SIMCTL_CHILD_SHOP_MODE=1 ;;
      duel)    export SIMCTL_CHILD_SCREENSHOT_SCENE=duel ;;
      profile) export SIMCTL_CHILD_SCREENSHOT_SCENE=profile ;;
    esac
    xcrun simctl launch "$UD" AppCraft31.tenGO -AppleLanguages "($LANGCODE)" >/dev/null
    sleep 4
    # Plus aucun recadrage : la bannière publicitaire a été supprimée du jeu
    # (commit 3f62595). L'ancien crop 1206x2340 amputait désormais la barre
    # d'onglets, c'est-à-dire précisément la nouveauté à montrer.
    xcrun simctl io "$UD" screenshot "$R/full_${MODE}_${LANGCODE}.png" >/dev/null 2>&1
    echo "$LANGCODE/$MODE"
  done
done
unset SIMCTL_CHILD_GAME_NORMAL SIMCTL_CHILD_SCREENSHOT_DAILY \
      SIMCTL_CHILD_SHOP_MODE SIMCTL_CHILD_SCREENSHOT_SCENE SIMCTL_CHILD_SCREENSHOT_SEED_DATA
echo "→ $R"
