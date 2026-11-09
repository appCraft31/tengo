#!/bin/zsh
# Compile le projet en batchmode (sous le verrou Unity) et échoue en cas
# d'erreur de compilation C#, en affichant les erreurs.
# Usage : Tools/compile_check.sh
set -u
ROOT=${0:A:h:h}
source $ROOT/Tools/lib/unity_lock.zsh
LOG=$ROOT/Logs/compile.log
mkdir -p $ROOT/Logs
rm -f $LOG
unity_run -batchmode -quit -nographics -projectPath $ROOT -logFile $LOG
code=$?
errors=$(grep -E "error CS[0-9]+" $LOG 2>/dev/null | sed -E 's/^[[:space:]]+//' | sort -u)
warnings=$(grep -cE "warning CS[0-9]+" $LOG 2>/dev/null)
if [[ -n $errors ]] || grep -q "Scripts have compiler errors" $LOG 2>/dev/null; then
  echo "Erreurs de compilation :"
  echo "${errors:-voir $LOG}"
  exit 1
fi
if (( code != 0 )); then
  unity_explain_failure $LOG
  echo "Unity a échoué (code $code) sans erreur C# : voir $LOG"
  grep -E "^(Error|Exception|.*Exception:)" $LOG | head -10
  exit $code
fi
echo "Compilation OK (${UNITY_SECONDS} s, ${warnings:-0} avertissement(s) C#)."
