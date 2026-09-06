#!/bin/bash
# Capture les écrans du jeu ANDROID dans chaque langue, pour la fiche Play.
#
# Pendant iOS de `capture_langs.sh` : mêmes écrans, mêmes noms de fichiers, mais
# rangés dans `render_android/` — la fiche Play doit montrer l'app Android, pas
# des captures d'iPhone.
#
# Prérequis : APK debug installé sur l'émulateur (le mode capture est compilé
# hors build release). Le pilotage passe par deux extras d'intent :
#   --es tengo_screen  … ouvre directement l'écran
#   --ez tengo_seed    … remplit les données de vitrine (cf. ScreenshotMode.kt)
#
# Usage : [SERIAL=emulator-5554] ./capture_langs_android.sh
set -e

S="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
R="$S/render_android"
PKG="com.appcraft31.tengo"
ACT="$PKG/com.tengo.MainActivity"
SERIAL="${SERIAL:-emulator-5554}"
mkdir -p "$R"

# La prod Play tourne sur le téléphone de l'utilisateur : on ne capture QUE sur
# l'émulateur, jamais sur un appareil physique.
case "$SERIAL" in
  emulator-*) ;;
  *) echo "refus : $SERIAL n'est pas un émulateur" >&2; exit 1 ;;
esac
adb -s "$SERIAL" get-state >/dev/null

# « code de langue du pipeline : locale Android » (le bash 3.2 de macOS n'a pas
# de tableau associatif).
PAIRS="fr:fr-FR en:en-US de:de-DE es:es-ES it:it-IT ja:ja-JP ko:ko-KR nl:nl-NL pt-BR:pt-BR zh-Hans:zh-CN"

for PAIR in $PAIRS; do
  LANG="${PAIR%%:*}"
  LOC="${PAIR##*:}"
  # ⚠️ `pm clear` efface aussi la langue par application : la reposer APRÈS.
  adb -s "$SERIAL" shell pm clear "$PKG" >/dev/null
  adb -s "$SERIAL" shell cmd locale set-app-locales "$PKG" --locales "$LOC" >/dev/null
  for MODE in menu game daily shop duel profile; do
    adb -s "$SERIAL" shell am force-stop "$PKG"
    # force-stop obligatoire : l'activité est en singleTask, un second `am start`
    # passerait par onNewIntent et les extras seraient ignorés.
    adb -s "$SERIAL" shell am start -n "$ACT" \
        --es tengo_screen "$MODE" --ez tengo_seed true >/dev/null
    # Démarrage à froid + JIT + transition : une capture trop précoce ramène le
    # fond animé (~40 Ko) ou l'écran de démarrage (~66 Ko). On mesure le poids
    # du PNG — un écran rempli dépasse 100 Ko — et on réessaie.
    OUT="$R/full_${MODE}_${LANG}.png"
    for TRY in 1 2 3; do
      sleep 9
      adb -s "$SERIAL" exec-out screencap -p > "$OUT"
      SIZE=$(wc -c < "$OUT")
      [ "$SIZE" -gt 100000 ] && break
      echo "  (reprise $LANG/$MODE : $SIZE octets)"
    done
    echo "$LANG/$MODE"
  done
done

adb -s "$SERIAL" shell cmd locale set-app-locales "$PKG" --locales "" >/dev/null || true
echo "→ $R"
