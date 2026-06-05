#!/usr/bin/env bash
#
# record-brand.sh — Capture l'écran de marque animé (logo TEN•GO « pop + flash »)
# et le monte en un bumper de transition vertical (~1s) pour les publicités.
#
# Produit :
#   raw-brand.mov        capture brute (plusieurs cycles)
#   brand-transition.mp4 bumper final 1080×1920, ~1s
#
# Variables (optionnelles) :
#   SIM_NAME / SIM_ID   Simulateur cible (def: iPhone 16 Pro Max)
#   DURATION            Durée de capture brute (s)   (def: 6)
#   START               Début du cycle isolé (s)     (def: 2.4)
#   CLIP_DUR            Durée du bumper (s)          (def: 1.0)
#   W / H               Résolution                   (def: 1080×1920)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

SCHEME="tenGO"
WORKSPACE="tenGO.xcworkspace"
BUNDLE_ID="AppCraft31.tenGO"
SIM_NAME="${SIM_NAME:-iPhone 16 Pro Max}"
DURATION="${DURATION:-6}"
START="${START:-2.4}"
CLIP_DUR="${CLIP_DUR:-1.0}"
W="${W:-1080}"
H="${H:-1920}"
DERIVED="build/DerivedData"
RAW="$OUT_DIR/raw-brand.mov"
OUT="$OUT_DIR/brand-transition.mp4"

SIM_ID="${SIM_ID:-$(xcrun simctl list devices available \
  | grep -E "^[[:space:]]+${SIM_NAME} \(" | head -1 | grep -oiE '[0-9A-F-]{36}')}"
[[ -z "$SIM_ID" ]] && { echo "✗ Simulateur « $SIM_NAME » introuvable" >&2; exit 1; }
echo "▶︎ Simulateur : $SIM_NAME ($SIM_ID)"

xcrun simctl boot "$SIM_ID" 2>/dev/null || true
open -a Simulator
xcrun simctl bootstatus "$SIM_ID" -b >/dev/null

echo "▶︎ Build ($SCHEME)…"
xcodebuild -workspace "$WORKSPACE" -scheme "$SCHEME" \
  -configuration Debug -sdk iphonesimulator \
  -destination "id=$SIM_ID" -derivedDataPath "$DERIVED" build >/dev/null

APP_PATH="$(find "$DERIVED/Build/Products/Debug-iphonesimulator" -maxdepth 1 -name '*.app' | head -1)"
echo "▶︎ Install : $APP_PATH"
xcrun simctl install "$SIM_ID" "$APP_PATH"

echo "▶︎ Lancement en mode marque (BRAND_MODE=1)"
SIMCTL_CHILD_BRAND_MODE=1 \
  xcrun simctl launch --terminate-running-process "$SIM_ID" "$BUNDLE_ID" >/dev/null

sleep 1.5
echo "▶︎ Enregistrement (${DURATION}s) → $RAW"
xcrun simctl io "$SIM_ID" recordVideo --codec=h264 --force "$RAW" &
REC_PID=$!
sleep "$DURATION"
kill -INT "$REC_PID"
wait "$REC_PID" 2>/dev/null || true

echo "▶︎ Montage bumper → $OUT  (start=${START}s, ${CLIP_DUR}s, ${W}×${H})"
ffmpeg -y -ss "$START" -t "$CLIP_DUR" -i "$RAW" \
  -vf "crop=in_w:in_w*${H}/${W},scale=${W}:${H}:flags=lanczos,eq=saturation=1.10:contrast=1.03,format=yuv420p" \
  -an -r 30 -c:v libx264 -profile:v high -preset slow -crf 18 -pix_fmt yuv420p \
  -movflags +faststart "$OUT"

echo "✅ Bumper de transition : $OUT"
ffprobe -v error -select_streams v:0 -show_entries stream=width,height,duration \
  -of default=noprint_wrappers=1 "$OUT" 2>/dev/null || true
