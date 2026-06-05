#!/usr/bin/env bash
#
# edit-social.sh — Monte la capture brute en une vidéo courte, énergique,
# verticale (9:16), prête pour TikTok / Reels / Shorts.
#
# Étapes ffmpeg : recadrage 9:16 plein cadre, accélération (rythme), boost
# léger de saturation/contraste, encodage H.264 optimisé réseaux sociaux.
#
# Usage :
#   edit-social.sh <raw.mov> [out.mp4]
#
# Variables (optionnelles) :
#   START      Début de la fenêtre source (s)   (def: 2.0  — saute l'apparition)
#   SPEED      Facteur d'accélération            (def: 1.5)
#   FINAL_DUR  Durée finale de la vidéo (s)      (def: 5)
#   CROP_Y     Décalage vertical du recadrage    (def: centré)
#   W,H        Résolution de sortie              (def: 1080x1920)
#
set -euo pipefail

IN="${1:?usage: edit-social.sh <raw.mov> [out.mp4]}"
OUT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="${2:-$OUT_DIR/gameplay-social.mp4}"

START="${START:-2.0}"
SPEED="${SPEED:-1.5}"
FINAL_DUR="${FINAL_DUR:-5}"
W="${W:-1080}"
H="${H:-1920}"

# Durée de source à prélever = durée finale × vitesse.
# LC_ALL=C : éviter la virgule décimale (locale fr) que ffmpeg rejette.
SRC_DUR="$(LC_ALL=C awk "BEGIN{printf \"%.3f\", $FINAL_DUR * $SPEED}")"

# Recadrage 9:16 : on garde toute la largeur, on rogne la hauteur (centré, ou CROP_Y).
if [[ -n "${CROP_Y:-}" ]]; then
  CROP="crop=in_w:in_w*${H}/${W}:0:${CROP_Y}"
else
  CROP="crop=in_w:in_w*${H}/${W}"
fi

echo "▶︎ Montage → $OUT  (start=${START}s, ×${SPEED}, ${FINAL_DUR}s, ${W}x${H})"

ffmpeg -y -ss "$START" -t "$SRC_DUR" -i "$IN" \
  -vf "${CROP},scale=${W}:${H}:flags=lanczos,setpts=PTS/${SPEED},eq=saturation=1.12:contrast=1.04,format=yuv420p" \
  -an -r 30 \
  -c:v libx264 -profile:v high -preset slow -crf 19 -pix_fmt yuv420p \
  -movflags +faststart \
  "$OUT"

echo "✅ Vidéo sociale : $OUT"
ffprobe -v error -select_streams v:0 -show_entries stream=width,height,duration \
  -of default=noprint_wrappers=1 "$OUT" 2>/dev/null || true
