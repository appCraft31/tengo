#!/bin/zsh
# Lanceur générique d'une méthode d'éditeur (builds, captures, réglages),
# sous le verrou Unity du projet.
# Usage : Tools/unity.sh <Namespace.Classe.Méthode> [arguments Unity en plus]
#   Tools/unity.sh TenGo.Editor.ProjectSetup.Apply
#   Tools/unity.sh TenGo.Editor.Captures.GlowDemo
#   Tools/unity.sh TenGo.Editor.BuildAndroid.Run -buildTarget Android
# Journal : Logs/unity_<Méthode>.log. Les lignes « [Tag] … » écrites par la
# méthode (Debug.Log("[Capture] …")) sont recopiées en sortie.
set -u
ROOT=${0:A:h:h}
source $ROOT/Tools/lib/unity_lock.zsh
if (( $# < 1 )); then
  echo "Usage : Tools/unity.sh <Namespace.Classe.Méthode> [arguments Unity]" >&2
  exit 2
fi
METHOD=$1; shift
LOG=$ROOT/Logs/unity_${METHOD##*.}.log
mkdir -p $ROOT/Logs
unity_run -batchmode -quit -projectPath $ROOT -executeMethod $METHOD -logFile $LOG "$@"
code=$?
grep -E "^\[[A-Z][A-Za-z]+\] " $LOG 2>/dev/null | grep -vE "^\[(Licensing|Package Manager|Physics|usbmuxd|Subsystems|MODES|Worker|ScriptCompilation|Api|Assembly|AssetDatabase|Licensing::[A-Za-z]+|UnityMemory|EntityIdStore|ExitDontLaunchBugReporter|Shader|Mono|Performance|Collab|XR|Input)\]" | tail -20
if (( code != 0 )); then
  unity_explain_failure $LOG
  grep -E "error CS[0-9]+|Exception|executeMethod" $LOG | sort -u | head -20
  echo "Échec de $METHOD (code $code, $UNITY_SECONDS s) : voir $LOG" >&2
  exit $code
fi
echo "$METHOD : OK (${UNITY_SECONDS} s)"
