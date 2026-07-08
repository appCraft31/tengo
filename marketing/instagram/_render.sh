#!/bin/bash
# Rend tous les HTML Instagram en PNG via Chrome headless.
# Usage : bash marketing/instagram/_render.sh
set -e
cd "$(dirname "$0")/../.."
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
ROOT="marketing/instagram"

render() {  # $1=format $2=W $3=H
  local fmt=$1 w=$2 h=$3
  for lang in fr en; do
    for f in "$ROOT/$fmt/$lang"/*.html; do
      [ -e "$f" ] || continue
      out="${f%.html}.png"
      "$CHROME" --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
        --window-size="$w,$h" --default-background-color=00000000 \
        --screenshot="$out" "file://$PWD/$f" 2>/dev/null
      echo "  ✓ $out"
    done
  done
}

echo "Rendu portrait 1080x1350…"
render portrait 1080 1350
echo "Rendu story 1080x1920…"
render story 1080 1920
echo "Terminé."
