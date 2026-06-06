#!/usr/bin/env bash
#
# record-gameplay.sh — Capture brute du mode démo (auto-player) sur simulateur.
#
# Build l'app, l'installe sur un simulateur, la lance en mode démo (grille
# déterministe jouée toute seule par le solveur) et enregistre l'écran.
# Produit un .mov brut, ensuite monté par edit-social.sh.
#
# Variables (toutes optionnelles) :
#   SIM_NAME    Nom du simulateur            (def: "iPhone 16 Pro Max")
#   SEED        Graine de la grille démo     (def: 7)
#   DEMO_SPEED  Vitesse du tracé in-app      (def: 1.0)
#   DURATION    Durée de capture brute (s)   (def: 14)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

SCHEME="tenGO"
WORKSPACE="tenGO.xcworkspace"
BUNDLE_ID="AppCraft31.tenGO"
SIM_NAME="${SIM_NAME:-iPhone 16 Pro Max}"
SEED="${SEED:-7}"
DEMO_SPEED="${DEMO_SPEED:-1.0}"
DURATION="${DURATION:-14}"
DERIVED="build/DerivedData"
RAW="$OUT_DIR/raw-gameplay.mov"

# Résout un UDID précis (le nom seul est ambigu : plusieurs versions d'iOS).
SIM_ID="${SIM_ID:-$(xcrun simctl list devices available \
  | grep -E "^[[:space:]]+${SIM_NAME} \(" | head -1 \
  | grep -oiE '[0-9A-F-]{36}')}"
if [[ -z "$SIM_ID" ]]; then
  echo "✗ Aucun simulateur disponible nommé « $SIM_NAME »" >&2
  exit 1
fi
echo "▶︎ Simulateur : $SIM_NAME ($SIM_ID)"

xcrun simctl boot "$SIM_ID" 2>/dev/null || true
open -a Simulator
xcrun simctl bootstatus "$SIM_ID" -b >/dev/null

echo "▶︎ Build ($SCHEME)…"
xcodebuild -workspace "$WORKSPACE" -scheme "$SCHEME" \
  -configuration Debug -sdk iphonesimulator \
  -destination "id=$SIM_ID" \
  -derivedDataPath "$DERIVED" build >/dev/null

APP_PATH="$(find "$DERIVED/Build/Products/Debug-iphonesimulator" -maxdepth 1 -name '*.app' | head -1)"
echo "▶︎ Install : $APP_PATH"
xcrun simctl install "$SIM_ID" "$APP_PATH"

echo "▶︎ Lancement en mode démo (seed=$SEED, speed=$DEMO_SPEED)"
SIMCTL_CHILD_DEMO_MODE=1 \
SIMCTL_CHILD_DEMO_SEED="$SEED" \
SIMCTL_CHILD_DEMO_SPEED="$DEMO_SPEED" \
  xcrun simctl launch --terminate-running-process "$SIM_ID" "$BUNDLE_ID" >/dev/null

# Laisse la grille s'installer avant d'enregistrer.
sleep 2

echo "▶︎ Enregistrement (${DURATION}s) → $RAW"
xcrun simctl io "$SIM_ID" recordVideo --codec=h264 --force "$RAW" &
REC_PID=$!
sleep "$DURATION"
kill -INT "$REC_PID"
wait "$REC_PID" 2>/dev/null || true

echo "✅ Capture brute : $RAW"
echo "   Montage 5s :  marketing/gameplay/edit-social.sh \"$RAW\""
