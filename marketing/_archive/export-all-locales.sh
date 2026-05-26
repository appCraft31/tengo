#!/bin/bash
#
# export-all-locales.sh
# Génère les screenshots App Store Connect pour toutes les langues disponibles.
#
# Usage : ./export-all-locales.sh
# Les PNG sont écrits dans screen-<code>/ pour chaque langue.
#

set -e
cd "$(dirname "$0")"

LOCALES=(en fr de es it pt-BR nl ja ko zh-Hans)

TOTAL_ALL=0
START=$(date +%s)

for code in "${LOCALES[@]}"; do
  IN="marketing-${code}"
  OUT="screen-${code}"

  if [ ! -d "$IN" ]; then
    echo "⚠️  Répertoire $IN absent, ignoré."
    continue
  fi

  echo ""
  echo "════════════════════════════════════════"
  echo "  Langue : $code  →  $OUT/"
  echo "════════════════════════════════════════"

  bash export-marketing.sh -i "$IN" -o "$OUT"

  count=$(ls "$OUT"/*.png 2>/dev/null | wc -l | tr -d ' ')
  TOTAL_ALL=$((TOTAL_ALL + count))
done

END=$(date +%s)
echo ""
echo "✅ Total : $TOTAL_ALL screenshots générés en $((END-START))s"
echo "   Répertoires : $(echo "${LOCALES[@]}" | tr ' ' '\n' | sed 's/^/screen-/' | tr '\n' ' ')"
