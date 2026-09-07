#!/usr/bin/env bash
#
# build-ad.sh — Monte une publicité TikTok à partir d'un rush et de sa partition.
#
#   creatives/<nom>.conf  +  capture/<rush>.mov  +  capture/<rush>.sfx.log
#        →  out/tengo-<nom>-<AAAAMMJJ>.mp4
#
# Le son n'est pas extrait de la vidéo (le simulateur n'en enregistre pas) : il
# est RENDU depuis le journal de notes par `sfx_render`, avec le DSP du jeu.
# Les temps sont divisés par l'accélération au moment du rendu, ce qui évite
# `atempo` et garde des attaques nettes.
#
# Usage : ./build-ad.sh <nom-de-création> [sortie.mp4]
#
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
NAME="${1:?usage: build-ad.sh <nom-de-création>}"
CONF="$HERE/creatives/$NAME.conf"
[[ -f "$CONF" ]] || { echo "✗ Création inconnue : $CONF" >&2; exit 1; }

# Valeurs par défaut, écrasées par le fichier de création.
SOURCE="rush-a"; START=0; DURATION=14; SPEED=1.0; CTA_DUR=2.0; GAIN=0.40
# Insert facultatif : une capture d'écran (l'écran Duel, par exemple) glissée
# entre le gameplay et la carte finale, animée d'un zoom lent.
INSERT=""; INSERT_DUR=2.5; INSERT_CROP_Y=""
HOOKS=()
# `AUDIO_OFFSET` : décalage vidéo/son, mesuré une fois par corrélation entre
# l'activité visuelle et l'énergie sonore (voir README). La latence de
# `simctl io recordVideo` s'est révélée négligeable.
AUDIO_OFFSET="${AUDIO_OFFSET:--0.05}"
# shellcheck source=/dev/null
source "$CONF"

RAW="$HERE/capture/$SOURCE.mov"
LOG="$HERE/capture/$SOURCE.sfx.log"
CTA="$HERE/cta/cta.png"
FONTS="$HERE/../../android/app/src/main/res/font"
OUT="${2:-$HERE/out/tengo-$NAME-$(date +%Y%m%d).mp4}"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT

for f in "$RAW" "$LOG" "$CTA"; do
  [[ -f "$f" ]] || { echo "✗ Manquant : $f" >&2; exit 1; }
done

# LC_ALL=C : la virgule décimale de la locale française est rejetée par ffmpeg.
BODY_DUR="$(LC_ALL=C awk "BEGIN{printf \"%.3f\", $DURATION / $SPEED}")"
INS_DUR=0
[[ -n "$INSERT" ]] && INS_DUR="$INSERT_DUR"
TOTAL="$(LC_ALL=C awk "BEGIN{printf \"%.3f\", $BODY_DUR + $INS_DUR + $CTA_DUR}")"

echo "▶︎ $NAME — corps ${BODY_DUR}s (×$SPEED) + insert ${INS_DUR}s + carte ${CTA_DUR}s = ${TOTAL}s"

# 1) Bande-son : rendue depuis le journal, déjà à la vitesse du montage.
( cd "$HERE/sfx_render" && gradle run -q --args="$LOG $WORK/audio.wav \
    --start $START --duration $DURATION --speed $SPEED --offset $AUDIO_OFFSET --gain $GAIN" )

# 2) Accroches.
python3 "$HERE/make_ass.py" "$WORK/hooks.ass" "${HOOKS[@]}" >/dev/null

# 3) Image + son en une passe.
#    - recadrage 9:16 pleine largeur (retire l'encoche et l'indicateur d'accueil)
#    - accélération par setpts, saturation/contraste légers (repris d'edit-social.sh)
#    - carte finale animée d'un zoom lent, enchaînée par concat
#    - audio normalisé à −14 LUFS, la référence des réseaux sociaux
#    - débit visé 4 Mb/s : l'image du jeu est lisse et pastel, un CRF confortable
#      y descendait sous les 2 Mb/s exigés par TikTok
BODY_CHAIN="[0:v]crop=in_w:in_w*1920/1080,scale=1080:1920:flags=lanczos,setpts=PTS/${SPEED},\
eq=saturation=1.12:contrast=1.04,fps=30,format=yuv420p,\
subtitles='$WORK/hooks.ass':fontsdir='$FONTS'[body];"
CTA_CHAIN="[1:v]scale=1080:1920,zoompan=z='min(1.0+0.0009*on\,1.05)':d=1:s=1080x1920:fps=30,\
fade=t=in:st=0:d=0.3,format=yuv420p[cta];"
AUDIO_CHAIN="[2:a]atrim=0:${TOTAL},apad=whole_dur=${TOTAL},\
loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo[a]"

if [[ -n "$INSERT" ]]; then
  # L'insert est recadré comme le gameplay : même cadrage, raccord invisible.
  # Un écran d'interface n'est pas rempli comme une grille : le recadrage 9:16
  # centré couperait le titre. `INSERT_CROP_Y` cale la fenêtre sur le contenu.
  INS_CROP="crop=in_w:in_w*1920/1080"
  [[ -n "$INSERT_CROP_Y" ]] && INS_CROP="crop=in_w:in_w*1920/1080:0:${INSERT_CROP_Y}"
  INS_CHAIN="[3:v]${INS_CROP},scale=1080:1920:flags=lanczos,\
zoompan=z='min(1.0+0.0012*on\,1.08)':d=1:s=1080x1920:fps=30,\
fade=t=in:st=0:d=0.25,format=yuv420p[ins];"
  CONCAT="[body][ins][cta]concat=n=3:v=1:a=0[v];"
  INS_INPUT=(-loop 1 -t "$INSERT_DUR" -i "$INSERT")
else
  INS_CHAIN=""; CONCAT="[body][cta]concat=n=2:v=1:a=0[v];"; INS_INPUT=()
fi

ffmpeg -y -v error -stats \
  -ss "$START" -t "$DURATION" -i "$RAW" \
  -loop 1 -t "$CTA_DUR" -i "$CTA" \
  -i "$WORK/audio.wav" \
  ${INS_INPUT[@]+"${INS_INPUT[@]}"} \
  -filter_complex "${BODY_CHAIN}${CTA_CHAIN}${INS_CHAIN}${CONCAT}${AUDIO_CHAIN}" \
  -map "[v]" -map "[a]" -t "$TOTAL" \
  -c:v libx264 -profile:v high -preset slow -b:v 4M -maxrate 5M -bufsize 10M \
  -pix_fmt yuv420p -r 30 \
  -c:a aac -b:a 192k -movflags +faststart \
  "$OUT"

echo "✅ $OUT"
ffprobe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate \
  -show_entries format=duration,bit_rate -of default=noprint_wrappers=1 "$OUT"
