#!/usr/bin/env bash
#
# record-demo.sh — Capture un rush de gameplay AVEC son pour les pubs TikTok.
#
# Le simulateur n'enregistre pas l'audio et le jeu n'embarque aucun fichier son :
# tout est synthétisé à l'exécution. On capture donc deux choses en parallèle —
# l'image par `simctl io recordVideo`, et la PARTITION par le journal de notes de
# `SoundManager` (`SOUND_LOG=1`). La bande-son est rendue ensuite par
# `sfx_render`, avec le DSP du jeu lui-même.
#
# Le gameplay vient du mode démo : le solveur joue seul une grille déterministe,
# donc deux captures avec la même graine sont identiques.
#
# Usage : [SEED=7] [DURATION=25] [NAME=rush-a] ./record-demo.sh
#
# Variables :
#   SIM_NAME    Simulateur cible            (def: "iPhone 16 Pro Max")
#   SEED        Graine de la grille démo    (def: 7)
#   DEMO_SPEED  Vitesse du tracé in-app     (def: 1.0 — au-delà de 2.0 le seuil
#               anti-saturation de 40 ms avale des notes)
#   DURATION    Durée du rush (s)           (def: 25)
#   NAME        Nom du rush                 (def: rush-<SEED>)
#   MAXLEN      Longueur maxi des chaînes   (def: 5 comme le jeu ; 7 pour des
#               coups spectaculaires à 550 points)
#   PUZZLE      Niveau de Puzzles à jouer   (vide = grille aléatoire)
#   MOVES       Coups imposés               (cf. ios/tools/puzzle_solve)
#   PRE_ROLL    Attente avant enregistrement (def: 3 ; 1.2 pour un puzzle)
#   LANG_CODE   Langue de l'app             (def: en — les publicités sont en
#               anglais, et le panneau de fin porte du texte)
#
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
CAP="$HERE/capture"
mkdir -p "$CAP"

SCHEME="tenGO"
WORKSPACE="tenGO.xcworkspace"
BUNDLE_ID="AppCraft31.tenGO"
SIM_NAME="${SIM_NAME:-iPhone 16 Pro Max}"
SEED="${SEED:-7}"
DEMO_SPEED="${DEMO_SPEED:-1.0}"
MAXLEN="${MAXLEN:-5}"
PUZZLE="${PUZZLE:-}"
MOVES="${MOVES:-}"
DURATION="${DURATION:-25}"
NAME="${NAME:-rush-$SEED}"
DERIVED="$ROOT/ios/build/DerivedData"
RAW="$CAP/$NAME.mov"
LOG="$CAP/$NAME.sfx.log"

cd "$ROOT/ios"

SIM_ID="${SIM_ID:-$(xcrun simctl list devices available \
  | grep -E "^[[:space:]]+${SIM_NAME} \(" | head -1 \
  | grep -oiE '[0-9A-F-]{36}')}"
[[ -n "$SIM_ID" ]] || { echo "✗ Aucun simulateur « $SIM_NAME »" >&2; exit 1; }
echo "▶︎ Simulateur : $SIM_NAME ($SIM_ID)"

xcrun simctl boot "$SIM_ID" 2>/dev/null || true
xcrun simctl bootstatus "$SIM_ID" -b >/dev/null

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  echo "▶︎ Build ($SCHEME)…"
  xcodebuild -workspace "$WORKSPACE" -scheme "$SCHEME" \
    -configuration Debug -sdk iphonesimulator \
    -destination "id=$SIM_ID" -derivedDataPath "$DERIVED" build >/dev/null
fi
APP_PATH="$(find "$DERIVED/Build/Products/Debug-iphonesimulator" -maxdepth 1 -name '*.app' | head -1)"
xcrun simctl install "$SIM_ID" "$APP_PATH"

DESC="seed=$SEED, speed=$DEMO_SPEED, maxlen=$MAXLEN"
[[ -n "$PUZZLE" ]] && DESC="niveau $PUZZLE, $(echo "$MOVES" | tr '|' '\n' | wc -l | tr -d ' ') coups imposés"
echo "▶︎ Démo ($DESC) + journaux notes/score"
: > "$LOG"
SIMCTL_CHILD_DEMO_MODE=1 \
SIMCTL_CHILD_DEMO_SEED="$SEED" \
SIMCTL_CHILD_DEMO_SPEED="$DEMO_SPEED" \
SIMCTL_CHILD_DEMO_MAXLEN="$MAXLEN" \
SIMCTL_CHILD_DEMO_PUZZLE="$PUZZLE" \
SIMCTL_CHILD_DEMO_MOVES="$MOVES" \
SIMCTL_CHILD_SOUND_LOG=1 \
SIMCTL_CHILD_DEMO_SCORE_LOG=1 \
  xcrun simctl launch --console-pty --terminate-running-process "$SIM_ID" "$BUNDLE_ID" \
  -AppleLanguages "(${LANG_CODE:-en})" >> "$LOG" 2>&1 &
APP_PID=$!

# Laisse la grille apparaître (la démo attend elle-même 0,6 s avant le premier
# coup). Trois secondes conviennent à un rush qui tourne en boucle ; une
# résolution de puzzle, elle, est finie en quelques secondes — il faut alors
# réduire l'attente pour ne pas rater les premiers coups.
sleep "${PRE_ROLL:-3}"

# Origine des temps de la vidéo, sur la MÊME horloge que le journal :
# `time.monotonic()` et `CACurrentMediaTime()` mesurent tous deux la durée
# d'éveil du noyau, que le simulateur partage avec l'hôte.
python3 -c 'import time; print(f"SFX RECORD_START {time.monotonic():.6f}")' >> "$LOG"

echo "▶︎ Enregistrement (${DURATION}s) → $RAW"
xcrun simctl io "$SIM_ID" recordVideo --codec=h264 --force "$RAW" &
REC_PID=$!
sleep "$DURATION"
kill -INT "$REC_PID" 2>/dev/null || true
wait "$REC_PID" 2>/dev/null || true
kill "$APP_PID" 2>/dev/null || true

NOTES=$(grep -c "SFX NOTE" "$LOG" || true)
SCORES=$(grep -c "^SCORE " "$LOG" || true)
FINAL=$(grep "^SCORE " "$LOG" | tail -1 | awk '{print $3}')
echo "✅ $RAW"
echo "   $LOG — $NOTES notes, $SCORES relevés de score (dernier : ${FINAL:-0})"
