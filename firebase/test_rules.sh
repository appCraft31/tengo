#!/usr/bin/env bash
#
# Test de non-régression des règles Firestore — tenGO
#
# Des règles de sécurité non testées sont des règles dont on ignore l'effet :
# une règle trop permissive laisse passer tous les tests « chemin heureux ».
# Ce script vérifie donc surtout ce qui doit être REFUSÉ.
#
# Usage :  ./firebase/test_rules.sh
# Sortie non nulle si une règle ne protège plus ce qu'elle doit protéger.
#
# ⚠️ Il s'exécute contre le VRAI projet (tengo-b62b1) et y crée des documents
# de test, qu'il supprime ensuite avec un jeton admin (les règles interdisent
# la suppression côté client, à dessein). À terme, l'émulateur Firestore
# (`firebase emulators:start`) serait plus propre : il demande le CLI et Java.

set -uo pipefail

PROJECT="tengo-b62b1"
ADMIN_ACCOUNT="appcraft31@gmail.com"
PLIST="$(dirname "$0")/../ios/tenGO/GoogleService-Info.plist"
BASE="https://firestore.googleapis.com/v1/projects/$PROJECT/databases/(default)/documents"
SUFFIX="$(date +%s)"
failures=0

API_KEY=$(python3 -c "import plistlib,sys; print(plistlib.load(open('$PLIST','rb'))['API_KEY'])")

signup() {  # → "uid idToken"
  curl -s -X POST -H "Content-Type: application/json" --data-binary '{"returnSecureToken":true}' \
    "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY" \
    | python3 -c "import json,sys; d=json.load(sys.stdin); print(d['localId'], d['idToken'])"
}

check() {  # libellé, ALLOW|DENY, code http
  local label="$1" expect="$2" code="$3" verdict
  if [ "$expect" = "ALLOW" ]; then
    [ "$code" = "200" ] && verdict="✅" || { verdict="❌ refusé à tort"; failures=$((failures+1)); }
  else
    [ "$code" != "200" ] && verdict="✅" || { verdict="❌ FAILLE : autorisé"; failures=$((failures+1)); }
  fi
  printf "%-58s %-5s → %s  %s\n" "$label" "$expect" "$code" "$verdict"
}

read -r UID1 TOK1 <<<"$(signup)"
read -r UID2 TOK2 <<<"$(signup)"
DUEL="TEST_$SUFFIX"
echo "joueurs de test : ${UID1:0:8}… et ${UID2:0:8}…"
echo

put() {  # token, chemin, corps → code http
  curl -s -X PATCH -H "Authorization: Bearer $1" -H "Content-Type: application/json" \
    --data-binary "$3" "$BASE/$2" -o /dev/null -w "%{http_code}"
}

EXPIRY="2099-01-01T00:00:00Z"
duel_body() {  # uid challenger, score
  printf '{"fields":{"seed":{"integerValue":"42"},"challengerUid":{"stringValue":"%s"},"challengerName":{"stringValue":"A"},"challengerScore":{"integerValue":"%s"},"expiresAt":{"timestampValue":"%s"},"opponentUid":{"nullValue":null}}}' "$1" "$2" "$EXPIRY"
}
answer_body() {  # score challenger, uid adversaire, score adversaire
  printf '{"fields":{"seed":{"integerValue":"42"},"challengerUid":{"stringValue":"%s"},"challengerName":{"stringValue":"A"},"challengerScore":{"integerValue":"%s"},"expiresAt":{"timestampValue":"%s"},"opponentUid":{"stringValue":"%s"},"opponentName":{"stringValue":"B"},"opponentScore":{"integerValue":"%s"}}}' "$UID1" "$1" "$EXPIRY" "$2" "$3"
}

check "écrire son propre profil"                    ALLOW "$(put "$TOK1" "players/$UID1" '{"fields":{"name":{"stringValue":"moi"}}}')"
check "écrire le profil d'un autre joueur"          DENY  "$(put "$TOK1" "players/$UID2" '{"fields":{"name":{"stringValue":"pirate"}}}')"
check "écrire sans être authentifié"                DENY  "$(curl -s -X PATCH -H 'Content-Type: application/json' --data-binary '{"fields":{"name":{"stringValue":"anon"}}}' "$BASE/players/$UID1" -o /dev/null -w '%{http_code}')"
check "écrire dans une collection non prévue"       DENY  "$(put "$TOK1" "secrets/x$SUFFIX" '{"fields":{"x":{"stringValue":"y"}}}')"
check "nom de profil trop long (>40 caractères)"    DENY  "$(put "$TOK1" "players/$UID1" '{"fields":{"name":{"stringValue":"'"$(printf 'a%.0s' {1..50})"'"}}}')"

check "challenger crée son duel"                    ALLOW "$(put "$TOK1" "duels/$DUEL" "$(duel_body "$UID1" 1500)")"
check "créer un duel au nom d'un autre"             DENY  "$(put "$TOK2" "duels/${DUEL}_b" "$(duel_body "$UID1" 9999)")"
check "créer un duel au score aberrant"             DENY  "$(put "$TOK1" "duels/${DUEL}_c" "$(duel_body "$UID1" 999999)")"
check "adversaire renseigne son score"              ALLOW "$(put "$TOK2" "duels/$DUEL" "$(answer_body 1500 "$UID2" 1200)")"
check "challenger réécrit son score après coup"     DENY  "$(put "$TOK1" "duels/$DUEL" "$(answer_body 99000 "$UID2" 1200)")"
check "rejouer un duel déjà tranché"                DENY  "$(put "$TOK2" "duels/$DUEL" "$(answer_body 1500 "$UID2" 3000)")"
check "LISTER tous les duels (le code doit rester le secret)" DENY "$(curl -s -H "Authorization: Bearer $TOK1" "$BASE/duels" -o /dev/null -w '%{http_code}')"
check "supprimer un duel"                           DENY  "$(curl -s -X DELETE -H "Authorization: Bearer $TOK1" "$BASE/duels/$DUEL" -o /dev/null -w '%{http_code}')"

# Ménage : les règles interdisent la suppression côté client, il faut donc un
# jeton admin (qui les contourne, par conception).
echo
ADMIN=$(gcloud auth print-access-token --account="$ADMIN_ACCOUNT" 2>/dev/null)
if [ -n "$ADMIN" ]; then
  for path in "duels/$DUEL" "players/$UID1" "players/$UID2"; do
    curl -s -X DELETE -H "Authorization: Bearer $ADMIN" -H "x-goog-user-project: $PROJECT" "$BASE/$path" -o /dev/null
  done
  for uid in "$UID1" "$UID2"; do
    curl -s -X POST -H "Authorization: Bearer $ADMIN" -H "x-goog-user-project: $PROJECT" \
      -H "Content-Type: application/json" --data-binary "{\"localId\":\"$uid\"}" \
      "https://identitytoolkit.googleapis.com/v1/projects/$PROJECT/accounts:delete" -o /dev/null
  done
  echo "documents et comptes de test supprimés."
else
  echo "⚠️ pas de jeton admin : documents de test laissés en base."
fi

echo
if [ "$failures" -eq 0 ]; then
  echo "✅ toutes les règles se comportent comme prévu."
else
  echo "❌ $failures règle(s) en défaut."
  exit 1
fi
