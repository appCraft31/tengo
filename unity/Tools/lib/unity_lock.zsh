# Verrou partagé : un seul Unity à la fois sur ce projet.
#
# Plusieurs agents travaillent en parallèle sur le projet et Unity refuse deux
# instances sur le même dossier. Tout script qui lance Unity source ce fichier
# et passe par `unity_run` :
#
#   ROOT=${0:A:h:h}
#   source $ROOT/Tools/lib/unity_lock.zsh
#   unity_run -batchmode -quit -projectPath $ROOT ...
#
# Le verrou est le dossier Logs/unity.lock (création atomique par `mkdir`),
# qui contient le pid du détenteur et sa commande. Attente active par pas de
# 5 s ; un verrou dont le pid est mort est repris. Libéré à la fin de
# `unity_run`, et par trap si le script est interrompu.
# Variables : UNITY (exécutable), UNITY_LOCK_TIMEOUT (s, 3600 par défaut).

UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.6.8f1/Unity.app/Contents/MacOS/Unity}
: ${ROOT:?ROOT doit être défini avant de sourcer unity_lock.zsh}
UNITY_LOCK=$ROOT/Logs/unity.lock
typeset -gi _UNITY_LOCK_HELD=0

unity_lock() {
  mkdir -p $ROOT/Logs
  local -i waited=0 timeout=${UNITY_LOCK_TIMEOUT:-3600}
  local owner
  while ! mkdir $UNITY_LOCK 2>/dev/null; do
    owner=$(cat $UNITY_LOCK/pid 2>/dev/null)
    if [[ -n $owner ]] && ! kill -0 $owner 2>/dev/null; then
      echo "[verrou] verrou orphelin (pid $owner disparu) : repris." >&2
      rm -rf $UNITY_LOCK
      continue
    fi
    # Dossier sans pid depuis plus d'une minute : créateur tué entre mkdir et l'écriture du pid.
    if [[ -z $owner && -n $(find $UNITY_LOCK -maxdepth 0 -mmin +1 2>/dev/null) ]]; then
      echo "[verrou] verrou sans propriétaire : repris." >&2
      rm -rf $UNITY_LOCK
      continue
    fi
    if (( waited % 60 == 0 )); then
      echo "[verrou] Unity occupé par pid ${owner:-?} ($(cat $UNITY_LOCK/cmd 2>/dev/null)) : attente…" >&2
    fi
    if (( waited >= timeout )); then
      echo "[verrou] abandon après ${timeout} s d'attente." >&2
      return 75
    fi
    sleep 5
    (( waited += 5 ))
  done
  echo $$ > $UNITY_LOCK/pid
  echo "${ZSH_ARGZERO:t} $(date '+%H:%M:%S')" > $UNITY_LOCK/cmd
  _UNITY_LOCK_HELD=1
  (( waited > 0 )) && echo "[verrou] obtenu après ${waited} s." >&2
  return 0
}

unity_unlock() {
  (( _UNITY_LOCK_HELD )) || return 0
  [[ $(cat $UNITY_LOCK/pid 2>/dev/null) == $$ ]] && rm -rf $UNITY_LOCK
  _UNITY_LOCK_HELD=0
}

# Les traps doivent être posés au niveau du script (en zsh, un trap EXIT posé
# dans une fonction se déclenche à la sortie de la fonction).
trap 'unity_unlock' EXIT
trap 'unity_unlock; exit 130' INT
trap 'unity_unlock; exit 143' TERM HUP

# Lance Unity sous verrou. Retourne le code de sortie d'Unity.
# UNITY_SECONDS contient ensuite la durée de l'exécution (hors attente).
unity_run() {
  unity_lock || return $?
  local -i t0=$SECONDS code
  "$UNITY" "$@"
  code=$?
  typeset -gi UNITY_SECONDS=$(( SECONDS - t0 ))
  unity_unlock
  return $code
}

# Après coup : explique les échecs dus à une autre instance d'Unity
# (éditeur ouvert à la main sur le projet, hors verrou).
unity_explain_failure() {
  local log=$1
  if grep -qE "another Unity instance is running|Multiple Unity instances cannot open the same project" $log 2>/dev/null; then
    echo "Échec : une autre instance d'Unity a le projet ouvert (éditeur ?). La fermer puis relancer." >&2
  fi
}
