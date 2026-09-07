# Publicités TikTok — gameplay, son du jeu, sans acteur

Quatre créations verticales de 12 à 13 s, en anglais, **sans voix off ni
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

## Les quatre angles

| Fichier | Angle | Accroche | Durée |
|---|---|---|---|
| `tengo-impossible-*.mp4` | défi, appât d'ego | « Only 1% chain six bubbles. » | 12,8 s |
| `tengo-rule-*.mp4` | démonstration | « Make 10. That's the whole game. » | 12,4 s |
| `tengo-asmr-*.mp4` | ASMR | « Sound on. » | 13,0 s |
| `tengo-duel-*.mp4` | nouveauté 3.0 | « Same grid. Two players. » | 13,2 s |

`asmr` est monté à vitesse réelle : chaque bulle garde son pop, c'est l'argument.

## Refaire les vidéos

```bash
./record-demo.sh                       # rush de 32 s + partition (seed 7)
./cta/render.sh                        # carte finale (Chrome headless)
./build-ad.sh impossible               # une création
for c in impossible rule asmr duel; do ./build-ad.sh $c; done
```

`record-demo.sh` : `SEED`, `DURATION`, `DEMO_SPEED`, `SKIP_BUILD=1`.
`build-ad.sh` lit `creatives/<nom>.conf` (fenêtre source, vitesse, accroches,
insert). Les rushes et les journaux sont ignorés par git — tout est
reproductible : même graine, même gameplay, même partition.

## Contraintes respectées

- **1080×1920, 30 fps, H.264 + AAC**, 2,6 à 3,4 Mb/s (TikTok demande ≥ 2 Mb/s).
- **Zones sûres** : accroche haute à 340 px du bord supérieur (l'interface en
  occupe 130), accroche basse à 620 px du bas (elle en occupe 484), marge droite
  de 250 px pour la colonne d'icônes.
- **Audio à −14,6 LUFS**, crête à −2,4 dB : pas d'écrêtage, niveau des réseaux.
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
