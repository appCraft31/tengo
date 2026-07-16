#!/bin/bash
# Capture les 4 écrans du jeu dans chaque langue (UI localisée).
set -e
UD=179A2EA6-218F-4440-823B-60A323F46DE4
S=/private/tmp/claude-501/-Users-nicolas-StudioProjects-tenGO/f5f7da44-b45a-4058-88ad-c6c360ba25d9/scratchpad
FF=/opt/homebrew/bin/ffmpeg

for LANGCODE in fr en de es it ja ko nl pt-BR zh-Hans; do
  for MODE in menu game daily shop; do
    xcrun simctl terminate $UD AppCraft31.tenGO 2>/dev/null || true
    case $MODE in
      menu)  unset SIMCTL_CHILD_GAME_NORMAL SIMCTL_CHILD_SCREENSHOT_DAILY SIMCTL_CHILD_SHOP_MODE ;;
      game)  export SIMCTL_CHILD_GAME_NORMAL=1 ;;
      daily) unset SIMCTL_CHILD_GAME_NORMAL; export SIMCTL_CHILD_SCREENSHOT_DAILY=1 ;;
      shop)  unset SIMCTL_CHILD_SCREENSHOT_DAILY; export SIMCTL_CHILD_SHOP_MODE=1 ;;
    esac
    xcrun simctl launch $UD AppCraft31.tenGO -AppleLanguages "($LANGCODE)" >/dev/null
    sleep 4
    xcrun simctl io $UD screenshot "$S/render/raw_${MODE}_${LANGCODE}.png" >/dev/null 2>&1
    $FF -y -i "$S/render/raw_${MODE}_${LANGCODE}.png" -vf "crop=1206:2340:0:0" \
        "$S/render/full_${MODE}_${LANGCODE}.png" 2>/dev/null
    rm "$S/render/raw_${MODE}_${LANGCODE}.png"
    echo "$LANGCODE/$MODE"
  done
  unset SIMCTL_CHILD_GAME_NORMAL SIMCTL_CHILD_SCREENSHOT_DAILY SIMCTL_CHILD_SHOP_MODE
done
xcrun simctl terminate $UD AppCraft31.tenGO 2>/dev/null || true
echo "captures terminées"
