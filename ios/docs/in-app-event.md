# Événement intégré App Store — tenGO 3.0

Badge : **Mise à jour majeure** (`MAJOR_UPDATE`), pour la sortie de la 3.0.
Nom de référence interne : `tenGO 3.0 — mise a jour majeure` (sans accent : l'API
les accepte, la recherche d'App Store Connect non).

> Un précédent événement existe sur la fiche : « defis du jour »
> (badge CHALLENGE), **archivé**, avec pour lien profond `tengo://daily` — un
> schéma personnalisé, qu'App Store Connect accepte à la saisie mais qui n'ouvre
> jamais l'app. C'est exactement le piège que la 3.0 corrige.

## Le lien profond

`https://appcraft31.app/tengo/event` — un **universal link**, obligatoire : le
schéma `tengo://` ne fonctionne pas pour un événement intégré.

Trois pièces, toutes en place :

1. **Route** — `tenGO/DeepLink.swift` reconnaît `event` (et `progress`) et
   ouvre la **Progression** (`GameViewController.openProgress()`), la vitrine
   des nouveautés : niveaux, missions, série, succès. La scène porte la barre
   d'onglets, le joueur repart d'une tape vers Jouer, Social ou Boutique.
   Le chemin `/tengo` est retiré avant routage : `https://appcraft31.app/tengo/daily`
   et `tengo://daily` empruntent la même branche.
2. **Entitlement** — `tenGO/tenGO.entitlements` déclare
   `applinks:appcraft31.app` et `applinks:appcraft31.vercel.app`.
   ⚠️ Un entitlement est scellé à la signature : le lien n'ouvrira l'app que
   pour les joueurs passés sur **la build qui le porte**. Les autres verront la
   page web — d'où son soin.
3. **Association du domaine** — `public/.well-known/apple-app-site-association`
   du site `appcraft31.app` (repo `Appcraft_Website`), entrée
   `JDKABK93UH.AppCraft31.tenGO` → `/tengo/*`, plus la page de repli
   `public/tengo/event/index.html`.

L'AASA ne couvrant que `/tengo/*`, aucun lien racine (`/daily`, `/event`) ne
sera jamais remis à l'app : toujours préfixer par `/tengo`.

## Ce qui se lance en ligne de commande

Depuis `ios/` (`bundle exec` est cassé — appeler `fastlane` directement) :

```bash
fastlane event_doctor      # pré-vol lecture seule : auth, visuels, textes, état
fastlane in_app_event      # crée/met à jour l'événement (start:/end: en option)
fastlane event_asset_probe path:… [kind:…] [locale:…]   # teste un format
```

`deliver` et Spaceship ne connaissent pas les événements : les lanes tapent
directement l'API REST, avec les helpers écrits pour les IAP (`asc_jwt`,
`asc_req`, `asc_get_all`, `asc_upload`).

`in_app_event` ne **soumet** pas : l'événement se soumet depuis App Store
Connect, et passe une revue **séparée** de celle de l'app (24-48 h).

## Textes — `fastlane/event_copy.json`

15 locales App Store pour 10 langues d'interface (en-GB/AU/CA recopient en-US,
es-MX = es-ES, pt-PT = pt-BR — même mapping que `deploy_screens.py`).

Limites Apple, vérifiées par la lane avant tout envoi : nom ≤ 30 caractères,
description courte ≤ 50, longue ≤ 120. **Pas d'emoji** (refusés, comme dans les
notes de version).

Le texte reprend le vocabulaire des notes de version 3.0
(`fastlane/metadata/<locale>/release_notes.txt`) : Duel, Rush 60 secondes,
vingt puzzles, cent niveaux. Ne rien y promettre que l'app ne tienne — la fiche
a déjà été corrigée deux fois là-dessus.

## Visuels — `marketing/event/`

| Rôle | Dimensions | Fichier |
|---|---|---|
| `EVENT_CARD` (vignette de découverte) | 1920 × 1080 | `event_card.png` |
| `EVENT_DETAILS_PAGE` (grand visuel) | 1920 × 3413 | `event_detail.png` |

Générés par `marketing/screens_generator/gen_event.py` (Chrome headless, mêmes
captures que la fiche stores) :

```bash
cd marketing/screens_generator && python3 gen_event.py        # les deux
python3 gen_event.py card                                      # un seul
```

**Sans aucun texte** : Apple superpose lui-même le nom et la description de
l'événement. Les deux visuels sont volontairement différents — ils se suivent à
l'écran.

## Ordre à respecter

1. Site déployé (AASA + page de repli) — **avant** l'installation de la build
   qui porte l'entitlement : iOS récupère l'association à l'installation, pas
   après coup.
2. Build de la 3.0 avec l'entitlement → TestFlight → test du lien sur un
   appareil réel → soumission de l'app.
3. `fastlane in_app_event`, puis `fastlane event_doctor` pour vérifier que tous
   les visuels sont `COMPLETE`, puis soumission de l'événement dans ASC.
   L'`eventStart` doit tomber après la mise en vente de la 3.0.

## Vérifications

```bash
curl -sS https://appcraft31.app/.well-known/apple-app-site-association | python3 -m json.tool
curl -sSI https://appcraft31.app/tengo/event                     # 200 attendu
curl -sS "https://app-site-association.cdn-apple.com/a/v1/appcraft31.app"   # cache Apple, ~24 h de retard
xcrun simctl openurl booted "tengo://event"                      # routage seul
```

Le simulateur ne valide pas les universal links : sur appareil, envoyer le lien
dans Notes ou Messages et le **taper** (une URL saisie dans Safari n'est jamais
interceptée). Si Safari s'ouvre malgré tout, tirer la page vers le bas fait
réapparaître la bannière « Ouvrir dans tenGO ».

Un asset en échec laisse son emplacement occupé et le log d'upload annonce
quand même « succès » : toujours conclure par `event_doctor`.
