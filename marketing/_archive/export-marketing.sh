#!/bin/bash
#
# export-marketing.sh
# Génère les PNG App Store Connect depuis les HTML marketing-*.html
#
# Stratégie : on rend la page à une taille logique raisonnable (~640 px de large
# pour iPhone, ~1024 px pour iPad) puis on force un device-pixel-ratio x2 ou x3
# pour obtenir un PNG à la résolution exacte demandée par App Store Connect.
# Cela évite les bricolages CSS (zoom) qui cassent le centrage flex.
#

set -e
cd "$(dirname "$0")"

# --- Dossier de sortie (argument optionnel, défaut : Screenshot-App-Store) ---

usage() {
  echo "Usage: $0 [-i <dossier_html>] [-o <dossier_png>]"
  echo "  -i  Dossier contenant les fichiers HTML (défaut : .)"
  echo "  -o  Dossier de destination des PNG     (défaut : Screenshot-App-Store)"
  exit 1
}

IN="."
OUT="Screenshot-App-Store"
while getopts ":i:o:" opt; do
  case $opt in
    i) IN="$OPTARG" ;;
    o) OUT="$OPTARG" ;;
    *) usage ;;
  esac
done

# --- Détection du navigateur ---

BROWSER=""
CANDIDATES=(
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
  "/Applications/Chromium.app/Contents/MacOS/Chromium"
  "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
  "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser"
)
for c in "${CANDIDATES[@]}"; do
  if [ -x "$c" ]; then BROWSER="$c"; break; fi
done
[ -z "$BROWSER" ] && BROWSER="$(command -v chromium 2>/dev/null || command -v google-chrome 2>/dev/null || true)"

if [ -z "$BROWSER" ] || [ ! -x "$BROWSER" ]; then
  echo "❌ Chrome / Chromium / Edge / Brave introuvable."
  echo "   Installe Google Chrome : https://www.google.com/chrome/"
  exit 1
fi

echo "🌐 Navigateur : $BROWSER"

# --- Sortie ---

mkdir -p "$OUT"

# --- Cibles App Store Connect (format : "output_w x output_h : logical_w x logical_h : dpr") ---
# Toutes les tailles logiques restent > 480 px pour rester en layout "desktop"
# (les media queries mobiles ne s'activent pas).

TARGETS=(
  # iPhone 6.5"
  "iphone:1284x2778:642x1389:2"
  "iphone:1242x2688:621x1344:2"
  # iPad 13"
  "ipad:2064x2752:1032x1376:2"
  "ipad:2048x2732:1024x1366:2"
)

# --- Fichiers à exporter ---

FILES=( "$IN"/marketing-{1,2,3,4,5,6}.html )

render() {
  local file="$1" w="$2" h="$3" dpr="$4" output="$5"
  "$BROWSER" \
    --headless=new \
    --disable-gpu \
    --hide-scrollbars \
    --virtual-time-budget=3000 \
    --window-size="${w},${h}" \
    --force-device-scale-factor="${dpr}" \
    --screenshot="$(cd "$OUT" && pwd)/$(basename "$output")" \
    "file://$file" >/dev/null 2>&1
}

TOTAL=0
START=$(date +%s)

for file in "${FILES[@]}"; do
  [ ! -f "$file" ] && { echo "⚠️  $file absent, skip"; continue; }
  name="$(basename "${file%.html}")"
  echo ""
  echo "📸 $file"

  for target in "${TARGETS[@]}"; do
    device="${target%%:*}"
    rest="${target#*:}"
    out_dim="${rest%%:*}"
    rest="${rest#*:}"
    log_dim="${rest%%:*}"
    dpr="${rest#*:}"

    out_w="${out_dim%x*}"
    out_h="${out_dim#*x}"
    log_w="${log_dim%x*}"
    log_h="${log_dim#*x}"

    out="$OUT/${name}_${device}_${out_w}x${out_h}.png"
    render "$(cd "$(dirname "$file")" && pwd)/$(basename "$file")" "$log_w" "$log_h" "$dpr" "$out"

    # Vérifie la taille du PNG produit
    actual=""
    if command -v sips >/dev/null 2>&1; then
      actual=$(sips -g pixelWidth -g pixelHeight "$out" 2>/dev/null | awk '/pixel/ {print $2}' | paste -sd x -)
    fi
    label="${device} ${out_w}×${out_h}"
    [ -n "$actual" ] && label="${label}  (rendu : ${actual})"
    echo "   ✓ ${label}  → $out"
    TOTAL=$((TOTAL+1))
  done
done

END=$(date +%s)
echo ""
echo "✅ $TOTAL screenshots générés en $((END-START))s dans $OUT/"
