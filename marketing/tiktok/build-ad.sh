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
SOURCE="rush7"; START=0; DURATION=14; SPEED=1.0; CTA_DUR=2.0; GAIN=0.40
# Insert facultatif : une capture d'écran glissée entre le gameplay et la carte
# finale, animée d'un zoom lent.
INSERT=""; INSERT_DUR=2.5; INSERT_CROP_Y=""
# Gel d'image : `FREEZE_AT` est un instant DANS LE RUSH ; l'image y est tenue
# `FREEZE_DUR` secondes. C'est le temps de réflexion des accroches « saurez-vous
# voir le coup ? ». Le son est décalé d'autant, sinon il jouerait sur une image
# figée.
FREEZE_AT=""; FREEZE_DUR=3.0
# Compte à rebours affiché pendant le gel (10 → 1 si COUNTDOWN=10).
COUNTDOWN=0
HOOKS=()
# `AUDIO_OFFSET` : décalage vidéo/son, mesuré une fois par corrélation entre
# l'activité visuelle et l'énergie sonore (voir README).
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
calc() { LC_ALL=C awk "BEGIN{printf \"%.3f\", $1}"; }

BODY_DUR="$(calc "$DURATION / $SPEED")"
HOLD_DUR=0
HOLD_AT=""
if [[ -n "$FREEZE_AT" ]]; then
  HOLD_DUR="$FREEZE_DUR"
  HOLD_AT="$(calc "($FREEZE_AT - $START) / $SPEED")"   # instant du gel, monté
  SEG_A="$(calc "$FREEZE_AT - $START")"                # source avant le gel
  SEG_B="$(calc "$START + $DURATION - $FREEZE_AT")"    # source après
fi
INS_DUR=0
[[ -n "$INSERT" ]] && INS_DUR="$INSERT_DUR"
TOTAL="$(calc "$BODY_DUR + $HOLD_DUR + $INS_DUR + $CTA_DUR")"

echo "▶︎ $NAME — corps ${BODY_DUR}s (×$SPEED) + gel ${HOLD_DUR}s + insert ${INS_DUR}s + carte ${CTA_DUR}s = ${TOTAL}s"

# 1) Bande-son : rendue depuis le journal, déjà à la vitesse du montage.
HOLD_ARGS=()
[[ -n "$HOLD_AT" ]] && HOLD_ARGS=(--hold-at "$HOLD_AT" --hold-dur "$HOLD_DUR")
( cd "$HERE/sfx_render" && gradle run -q --args="$LOG $WORK/audio.wav \
    --start $START --duration $DURATION --speed $SPEED --offset $AUDIO_OFFSET --gain $GAIN \
    ${HOLD_ARGS[*]:-}" )

# 2) Accroches, plus le compte à rebours s'il y en a un.
CUES=("${HOOKS[@]}")
if (( COUNTDOWN > 0 )); then
  # Une réplique par seconde, centrée, pendant le gel.
  STEP="$(calc "$FREEZE_DUR / $COUNTDOWN")"
  for ((i=0; i<COUNTDOWN; i++)); do
    S="$(calc "$HOLD_AT + $i * $STEP")"
    E="$(calc "$HOLD_AT + ($i + 1) * $STEP")"
    CUES+=("$S|$E|$((COUNTDOWN - i))|count")
  done
fi
python3 "$HERE/make_ass.py" "$WORK/hooks.ass" "${CUES[@]}" >/dev/null

# 3) Entrées ffmpeg. L'ordre fixe les index utilisés dans le graphe de filtres.
INPUTS=(); IDX=0
if [[ -n "$FREEZE_AT" ]]; then
  # L'image gelée est extraite d'abord : c'est la dernière image visible avant
  # le coup, celle que le spectateur doit avoir le temps de lire.
  ffmpeg -y -v error -ss "$FREEZE_AT" -i "$RAW" -frames:v 1 "$WORK/freeze.png"
  INPUTS+=(-ss "$START" -t "$SEG_A" -i "$RAW");            I_A=$IDX; ((IDX++))
  INPUTS+=(-loop 1 -t "$FREEZE_DUR" -i "$WORK/freeze.png"); I_F=$IDX; ((IDX++))
  INPUTS+=(-ss "$FREEZE_AT" -t "$SEG_B" -i "$RAW");        I_B=$IDX; ((IDX++))
else
  INPUTS+=(-ss "$START" -t "$DURATION" -i "$RAW");         I_A=$IDX; ((IDX++))
fi
INPUTS+=(-loop 1 -t "$CTA_DUR" -i "$CTA");                 I_C=$IDX; ((IDX++))
INPUTS+=(-i "$WORK/audio.wav");                            I_S=$IDX; ((IDX++))
if [[ -n "$INSERT" ]]; then
  INPUTS+=(-loop 1 -t "$INSERT_DUR" -i "$HERE/$INSERT");   I_I=$IDX; ((IDX++))
fi

# 4) Graphe de filtres.
#    - recadrage 9:16 pleine largeur (retire l'encoche et l'indicateur d'accueil)
#    - accélération par setpts, saturation/contraste légers (repris d'edit-social.sh)
#    - accroches incrustées APRÈS le raccord des morceaux : leurs temps sont
#      ceux du montage final, gel compris
#    - audio normalisé à −14 LUFS, la référence des réseaux sociaux
#    - débit visé 4 Mb/s : l'image du jeu est lisse et pastel, un CRF confortable
#      y descendait sous les 2 Mb/s exigés par TikTok
GAME_FX="crop=in_w:in_w*1920/1080,scale=1080:1920:flags=lanczos"
GRADE="eq=saturation=1.12:contrast=1.04,fps=30,format=yuv420p"

FG="[${I_A}:v]${GAME_FX},setpts=PTS/${SPEED},${GRADE}[segA];"
if [[ -n "$FREEZE_AT" ]]; then
  FG+="[${I_F}:v]${GAME_FX},${GRADE}[hold];"
  FG+="[${I_B}:v]${GAME_FX},setpts=PTS/${SPEED},${GRADE}[segB];"
  FG+="[segA][hold][segB]concat=n=3:v=1:a=0[raw];"
else
  FG+="[segA]null[raw];"
fi
FG+="[raw]subtitles='$WORK/hooks.ass':fontsdir='$FONTS'[body];"
FG+="[${I_C}:v]scale=1080:1920,zoompan=z='min(1.0+0.0009*on\,1.05)':d=1:s=1080x1920:fps=30,\
fade=t=in:st=0:d=0.3,format=yuv420p[cta];"
if [[ -n "$INSERT" ]]; then
  # Un écran d'interface n'est pas rempli comme une grille : le recadrage 9:16
  # centré couperait le titre. `INSERT_CROP_Y` cale la fenêtre sur le contenu.
  INS_CROP="crop=in_w:in_w*1920/1080"
  [[ -n "$INSERT_CROP_Y" ]] && INS_CROP="crop=in_w:in_w*1920/1080:0:${INSERT_CROP_Y}"
  FG+="[${I_I}:v]${INS_CROP},scale=1080:1920:flags=lanczos,\
zoompan=z='min(1.0+0.0012*on\,1.08)':d=1:s=1080x1920:fps=30,\
fade=t=in:st=0:d=0.25,format=yuv420p[ins];"
  FG+="[body][ins][cta]concat=n=3:v=1:a=0[v];"
else
  FG+="[body][cta]concat=n=2:v=1:a=0[v];"
fi
FG+="[${I_S}:a]atrim=0:${TOTAL},apad=whole_dur=${TOTAL},\
loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo[a]"

ffmpeg -y -v error -stats "${INPUTS[@]}" \
  -filter_complex "$FG" \
  -map "[v]" -map "[a]" -t "$TOTAL" \
  -c:v libx264 -profile:v high -preset slow -b:v 4M -maxrate 5M -bufsize 10M \
  -pix_fmt yuv420p -r 30 \
  -c:a aac -b:a 192k -movflags +faststart \
  "$OUT"

echo "✅ $OUT"
ffprobe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate \
  -show_entries format=duration,bit_rate -of default=noprint_wrappers=1 "$OUT"
