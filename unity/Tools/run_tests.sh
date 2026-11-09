#!/bin/zsh
# Lance les tests Unity en batchmode, sous le verrou Unity du projet.
# Usage : Tools/run_tests.sh [EditMode|PlayMode|all] [filtre]
set -u
ROOT=${0:A:h:h}
source $ROOT/Tools/lib/unity_lock.zsh
OUT=$ROOT/Logs/tests
mkdir -p $OUT
MODE=${1:-all}
FILTER=${2:-}
[[ $MODE == all ]] && MODES=(EditMode PlayMode) || MODES=($MODE)
rc=0
for m in $MODES; do
  rm -f $OUT/$m.xml
  args=(-batchmode -projectPath $ROOT -runTests -testPlatform $m -testResults $OUT/$m.xml -logFile $OUT/$m.log)
  [[ -n $FILTER ]] && args+=(-testFilter $FILTER)
  unity_run $args
  code=$?
  summary=$(grep -oE '<test-run [^>]*' $OUT/$m.xml 2>/dev/null | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' ')
  echo "$m : ${summary:-pas de résultats (voir $OUT/$m.log)} (${UNITY_SECONDS:-?} s)"
  if (( code != 0 )); then
    unity_explain_failure $OUT/$m.log
    grep -E "error CS[0-9]+" $OUT/$m.log 2>/dev/null | sort -u | head -20
    rc=$code
  fi
done
# Les tests remplissent les atlas dynamiques des polices (Assets/Fonts/*SDF.asset) :
# on les vide pour laisser `git status` propre.
if ! git -C $ROOT diff --quiet -- Assets/Fonts 2>/dev/null; then
  $ROOT/Tools/unity.sh TenGo.Editor.ProjectSetup.ClearDynamicFontData >/dev/null 2>&1 \
    && echo "Atlas de polices dynamiques vidés." \
    || echo "Échec du vidage des atlas de polices (Tools/unity.sh TenGo.Editor.ProjectSetup.ClearDynamicFontData)."
fi
exit $rc
