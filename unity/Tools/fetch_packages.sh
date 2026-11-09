#!/bin/zsh
# Récupère les paquets Firebase Unity 14.1.0 dans Packages/tarballs/ (hors git :
# ~80 Mo de binaires). Appelé automatiquement par Tools/lib/unity_lock.zsh avant
# chaque lancement d'Unity si une archive manque.
#
# - Archives officielles de Google (dl.google.com/games/registry/unity), vérifiées
#   par SHA-256.
# - com.google.firebase.app est réempaquetée : seule sa dépendance change,
#   « com.google.external-dependency-manager » → « com.unity.external-dependency-manager »
#   2.1.0, l'EDM dont dépend déjà Google Mobile Ads. Deux EDM dans un projet se
#   marchent dessus (même méthode que Binero Unity).
# - analytics, auth et firestore ne dépendent que de com.google.firebase.app :
#   reprises telles quelles.
set -eu
ROOT=${0:A:h:h}
DEST=$ROOT/Packages/tarballs
VERSION=14.1.0
typeset -A SHA=(
  app       934c52d39cac8465d13f530cab4ebe2e1e336ed4b35286691a08c6854574cb6f
  analytics d2b242d7bbf1862aa226f5861d16cfdf5cd368bdd5d7977686a9933fb50450c7
  auth      1776ee6181ab7803ec2e2dfb2969d2b2299d3ff0fcd8b2bafc12333e73b9650a
  firestore adae94248ba896c77fa32ef48b6a89a3edacb70bc794238e5a71e7e896cdd6d2
)
mkdir -p $DEST
TMP=$(mktemp -d)
trap 'rm -rf $TMP' EXIT
for name in app analytics auth firestore; do
  file=com.google.firebase.$name-$VERSION.tgz
  [[ -f $DEST/$file ]] && continue
  echo "[paquets] téléchargement de $file"
  mkdir -p $TMP/$name
  curl -sSfL -o $TMP/$name/$file https://dl.google.com/games/registry/unity/com.google.firebase.$name/$file
  sum=$(shasum -a 256 $TMP/$name/$file | cut -d' ' -f1)
  if [[ $sum != $SHA[$name] ]]; then
    echo "[paquets] empreinte inattendue pour $file : $sum" >&2
    exit 1
  fi
  if [[ $name == app ]]; then
    python3 -I $ROOT/Tools/packages/repack_firebase_app.py $TMP/$name/$file $DEST/$file
  else
    cp $TMP/$name/$file $DEST/$file
  fi
done
