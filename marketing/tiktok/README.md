# Publicités TikTok — gameplay, son du jeu, sans acteur

Onze créations verticales de 11 à 20 s, en anglais, **sans voix off ni
musique** : uniquement du gameplay et les bruitages du jeu, avec une accroche
incrustée et une carte finale.

```
record-demo.sh   →  capture/<rush>.mov  +  capture/<rush>.sfx.log
                          ↓                        ↓
                     recadrage 9:16          sfx_render (DSP du jeu)
                          └──────── build-ad.sh ────────┘
                                        ↓
                              out/tengo-<angle>-<date>.mp4
```

## Le problème du son, et comment il est résolu

Le jeu n'embarque **aucun fichier audio** : tout est synthétisé à l'exécution.
Et `xcrun simctl io recordVideo` **n'enregistre pas l'audio** — la seule vidéo
d'iPhone du dépôt (`marketing/preview/`) a d'ailleurs une piste audio
numériquement muette (−91 dB).

La bande-son n'est donc ni capturée ni imitée : elle est **rendue**. Pendant la
capture, l'app tourne avec `SOUND_LOG=1` et `SoundManager` écrit la partition
exacte de la session — chaque note, son instant, sa nuance. `sfx_render` la
rejoue ensuite avec le DSP du jeu lui-même (`Voice.kt`, `Dsp.kt`, **copiés sans
modification** depuis l'app Android, qui n'ont aucune dépendance Android).

Conséquence utile : les temps sont divisés par l'accélération **au moment du
rendu**, donc aucun `atempo` — les attaques restent nettes même sur un montage
accéléré.

## Les onze angles

| Fichier | Angle | Accroche | Durée |
|---|---|---|---|
| `tengo-hidden-move-*.mp4` | énigme, gel de 4 s | « 99% miss this move. » | 12,5 s |
| `tengo-ten-seconds-*.mp4` | compte à rebours 10 → 1 | « You have 10 seconds. » | 19,7 s |
| `tengo-brain-*.mp4` | POV, montée en difficulté | « POV: you downloaded an "easy" game. » | 11,0 s |
| `tengo-satisfying-*.mp4` | satisfaisant, sans texte 8 s | « This is weirdly satisfying. » | 14,0 s |
| `tengo-beat-score-*.mp4` | défi de score, appel aux commentaires | « Can you beat 3,580? » | 10,8 s |
| `tengo-positioning-*.mp4` | positionnement | « No timer. No lives. No stress. » | 11,0 s |
| `tengo-puzzle-ep1-*.mp4` | série de puzzles, épisode 1 | « I thought this level was impossible. » | 16,5 s |
| `tengo-impossible-*.mp4` | défi, appât d'ego | « Only 1% chain six bubbles. » | 12,8 s |
| `tengo-rule-*.mp4` | démonstration | « Make 10. That's the whole game. » | 12,4 s |
| `tengo-asmr-*.mp4` | ASMR | « Sound on. » | 13,0 s |
| `tengo-duel-*.mp4` | nouveauté 3.0 | « Same grid. Two players. » | 13,2 s |

`asmr` et `satisfying` sont montés à vitesse réelle : chaque bulle garde son
pop, c'est l'argument.

## Ce que la démo sait faire pour la publicité

Quatre variables d'environnement, toutes sans effet sur le jeu livré :

| Variable | Effet | Pourquoi elle existe |
|---|---|---|
| `DEMO_MAXLEN` | plafond de longueur du solveur (5 par défaut) | à 7, la démo trace des chaînes à 350 et 550 points ; à 5, l'accroche « chain six bubbles » ne décrivait rien de ce que la vidéo montrait |
| `DEMO_PUZZLE` | joue une grille du catalogue Puzzles | seule façon de filmer le mode Puzzles |
| `DEMO_MOVES` | impose la suite des coups | le solveur glouton ne vide qu'un niveau sur vingt : sans script, pas de vidéo de puzzle résolu |
| `DEMO_SCORE_LOG` | journalise le score **affiché** | l'accroche « Can you beat 3,580? » doit citer un nombre lisible à l'image, pas le score interne |

`ios/tools/puzzle_solve` calcule les `DEMO_MOVES` d'un niveau par exploration
exhaustive mémoïsée, avec le vrai code du jeu, et refuse une solution qui ne
vide pas le plateau ou n'atteint pas les trois étoiles.

## Ce que le montage sait faire

- **Gel d'image** (`FREEZE_AT`, `FREEZE_DUR`) : le corps devient trois morceaux,
  avant / image tenue / après. C'est le temps de réflexion des concepts
  « saurez-vous voir le coup ? ». `sfx_render --hold-at` décale les notes
  postérieures, sinon le son jouerait sur une image figée.
- **Compte à rebours** (`COUNTDOWN`) : un chiffre par seconde pendant le gel,
  dans un gabarit dédié — 340 px, translucide, sans bandeau. Au corps des
  accroches il se confondait avec les bulles numérotées de la grille.
- **Insert** (`INSERT`, `INSERT_DUR`, `INSERT_CROP_Y`) : une capture d'interface
  glissée avant la carte finale, avec un zoom lent.

## Refaire les vidéos

```bash
MAXLEN=7 DURATION=35 NAME=rush7 ./record-demo.sh          # rush principal
PUZZLE=19 MOVES="$(cd ../../ios/tools/puzzle_solve && swift run 2>/dev/null)" \
  DURATION=18 NAME=puzzle19 DEMO_SPEED=0.6 PRE_ROLL=1.1 ./record-demo.sh
./cta/render.sh                                           # carte finale
for c in hidden-move ten-seconds brain satisfying beat-score positioning \
         puzzle-ep1 impossible rule asmr duel; do ./build-ad.sh $c; done
```

`record-demo.sh` : `SEED`, `DURATION`, `DEMO_SPEED`, `MAXLEN`, `PUZZLE`,
`MOVES`, `PRE_ROLL`, `LANG_CODE`, `SKIP_BUILD=1`.
`build-ad.sh` lit `creatives/<nom>.conf`. Les rushes et les journaux sont
ignorés par git — tout est reproductible : même graine, même gameplay, même
partition.

## Contraintes respectées

- **1080×1920, 30 fps, H.264 + AAC**, 2,7 à 3,5 Mb/s (TikTok demande ≥ 2 Mb/s).
- **Zones sûres** : accroche haute à 340 px du bord supérieur (l'interface en
  occupe 130), accroche basse à 620 px du bas (elle en occupe 484), marge droite
  de 250 px pour la colonne d'icônes.
- **Audio de −15,0 à −12,0 LUFS**, crête entre −4,6 et −1,9 dBFS : pas
  d'écrêtage, niveau des réseaux. Les gels sont numériquement muets (−91 dB).
- **Aucune musique** : la régie interdit d'en incruster, elle s'ajoute dans
  TikTok Ads Manager depuis la bibliothèque commerciale si besoin.
- Pas de logo en ouverture : les trois premières secondes décident du hook rate,
  elles montrent le jeu en action.

## Protocole d'A/B

Trois à cinq accroches sur un même corps, 48 à 72 h, budget de 20 à 50 fois le
coût d'installation visé. L'usure créative sur TikTok se compte en jours :
rafraîchir toutes les 1 à 2 semaines. Pour tester une accroche, dupliquer un
`.conf`, changer `HOOKS` et rien d'autre.

## Détails qui ont coûté du temps

- **Un dialogue système** (« Ouvrir dans tenGO ? ») s'était invité dans un
  premier rush : redémarrer le simulateur avant de capturer.
- **Le décalage vidéo/son** a été mesuré par corrélation entre l'activité
  visuelle et l'énergie sonore : la latence de `recordVideo` est négligeable
  (`AUDIO_OFFSET=-0.05` par défaut). À revérifier si l'on change de machine.
- **Le gain** : à 0,5 (celui du jeu) huit voix superposées écrêtent. On rend à
  0,40 et `loudnorm` remonte le niveau proprement.
- **Un écran d'interface ne se recadre pas comme une grille** : `INSERT_CROP_Y`
  cale la fenêtre 9:16 sous l'encoche, sinon le titre disparaît.
- **`FREEZE_AT` se cale AVANT la première note du coup**, pas dessus :
  `sfx_render` ne décale que les notes postérieures au gel, si bien qu'un point
  de gel posé sur la note laissait le premier « pop » sonner sur l'image figée.
- **Le départ d'une création à gel colle au gel** (0,10 s avant). Plus tôt,
  l'ouverture tombait sur une cascade, donc sur une grille à moitié vide : la
  pire image possible pour la seconde qui décide du visionnage.
- **Une chaîne annoncée doit exister dans le rush** : les longueurs se comptent
  dans le journal de notes (une note par bulle), et les points annoncés se
  relisent dans le journal de score.
