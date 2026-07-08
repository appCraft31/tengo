#!/bin/bash
#
# _render-screen-6.sh
# Rend screen-6 (iPhone 1284x2778 + iPad 2048x2732) en PNG pour les 10 langues.
# Taille logique × device-scale-factor 2 → résolution App Store Connect exacte.
# Les langues sont rendues en parallèle (1 process Chrome par langue).
#
cd "$(dirname "$0")"

BROWSER="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
LOCALES=(en fr de es it pt-BR nl ja ko zh-Hans)
ROOT="$(pwd)"

# Rend à la taille de point réelle du device × son device-pixel-ratio réel
# (identique au jeu de screens existant) :
#   iPhone 6.5" : 428×926 pt @3x → 1284×2778
#   iPad 12.9"  : 1024×1366 pt @2x → 2048×2732
render() {
  local html="$1" out="$2" lw="$3" lh="$4" dpr="$5"
  local profile; profile="$(mktemp -d)"
  "$BROWSER" --headless=new --disable-gpu --hide-scrollbars --no-sandbox \
    --user-data-dir="$profile" \
    --force-device-scale-factor="$dpr" \
    --window-size="${lw},${lh}" \
    --default-background-color=00000000 \
    --screenshot="$out" "file://$ROOT/$html" >/dev/null 2>&1
  rm -rf "$profile"
}

one_locale() {
  local code="$1"
  render "$code/screen-6.html"      "$code/png/iphone_1284x2778/screen-6.png" 428 926 3
  render "$code/screen-6_ipad.html" "$code/png/ipad_2048x2732/screen-6.png"   1024 1366 2
  echo "✅ $code"
}

for code in "${LOCALES[@]}"; do
  one_locale "$code" &
done
wait
echo "🎉 Terminé"
