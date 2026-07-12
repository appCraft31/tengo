#!/bin/bash
# Re-rend les captures téléphone (cadre Android punch-hole) pour les 10 langues.
cd "$(dirname "$0")"
BROWSER="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
LOCALES=(en fr de es it pt-BR nl ja ko zh-Hans)
ROOT="$(pwd)"

render() {
  local html="$1" out="$2"
  local profile; profile="$(mktemp -d)"
  "$BROWSER" --headless=new --disable-gpu --hide-scrollbars --no-sandbox \
    --user-data-dir="$profile" --force-device-scale-factor=3 \
    --window-size=428,926 --default-background-color=00000000 \
    --screenshot="$out" "file://$ROOT/$html" >/dev/null 2>&1
  rm -rf "$profile"
}

one() {
  local code="$1"
  for n in 1 2 3 4 5 6; do
    [ -f "$code/screen-$n.html" ] && render "$code/screen-$n.html" "$code/png/iphone_1284x2778/screen-$n.png"
  done
  echo "OK $code"
}

if [ -n "$1" ]; then
  # Mode test : un seul fichier → $1=html $2=out
  render "$1" "$2"; echo "rendered $2"; exit 0
fi

for code in "${LOCALES[@]}"; do one "$code" & done
wait
echo "DONE"
