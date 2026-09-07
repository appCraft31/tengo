#!/usr/bin/env bash
# Rend la carte finale en PNG 1080×1920 (Chrome headless, comme les autres
# visuels du dossier marketing).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
ICON="$HERE/../../icon/AppIcon-1024.png"
TMP="$HERE/.cta.html"
sed "s|ICON_PATH|file://$ICON|" "$HERE/cta.html" > "$TMP"
"$CHROME" --headless=new --disable-gpu --hide-scrollbars \
  --window-size=1080,1920 --force-device-scale-factor=1 \
  --screenshot="$HERE/cta.png" "file://$TMP" 2>/dev/null
rm -f "$TMP"
echo "✅ $HERE/cta.png"
