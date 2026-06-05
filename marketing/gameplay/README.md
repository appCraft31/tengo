# Vidéo de gameplay automatique (marketing)

Génère une **vidéo de gameplay courte et énergique** (TikTok / Reels / Shorts)
sans jouer à la main : l'app embarque un **mode démo** où un solveur joue tout
seul une grille déterministe, et on capture l'écran du simulateur.

## Pipeline

```
record-gameplay.sh   →  raw-gameplay.mov   →  edit-social.sh  →  gameplay-social.mp4
(build + démo + capture)   (1320×2868, ~14s)    (recadrage/accél.)   (1080×1920, 5s)
```

### 1. Capturer

```bash
marketing/gameplay/record-gameplay.sh
```

Variables utiles :

| Var          | Déf.                  | Rôle                                   |
|--------------|-----------------------|----------------------------------------|
| `SIM_NAME`   | `iPhone 16 Pro Max`   | Simulateur cible                       |
| `SIM_ID`     | (résolu auto)         | Forcer un UDID précis (nom ambigu)     |
| `SEED`       | `7`                   | Graine de la grille démo (reproductible)|
| `DEMO_SPEED` | `1.0`                 | Vitesse du tracé in-app                |
| `DURATION`   | `14`                  | Durée de capture brute (s)             |

### 2. Monter (5s, vertical, énergique)

```bash
marketing/gameplay/edit-social.sh marketing/gameplay/raw-gameplay.mov
```

| Var         | Déf.   | Rôle                                          |
|-------------|--------|-----------------------------------------------|
| `START`     | `2.0`  | Début de la fenêtre source (saute l'intro)    |
| `SPEED`     | `1.5`  | Accélération (rythme)                          |
| `FINAL_DUR` | `5`    | Durée finale (s)                               |
| `CROP_Y`    | centré | Décalage vertical du recadrage 9:16           |
| `W` / `H`   | 1080/1920 | Résolution de sortie                       |

Le recadrage 9:16 retire la Dynamic Island en haut et le home-indicator en bas.

## Comment ça marche

- **Mode démo** : `GameScene` initialisé via `GameScene(size:demoSeed:demoSpeed:)`,
  déclenché par les variables d'environnement `DEMO_MODE=1` / `DEMO_SEED` /
  `DEMO_SPEED` (lues dans `GameViewController`). En démo : grille seedée,
  boutons/pubs/Game Center désactivés, score qui grimpe en continu.
- **Solveur** : `GridModel.showcasePath(maxLen:)` (`PathSolver.swift`) cherche le
  chemin somme-10 le plus long (combos spectaculaires) ; l'auto-player l'anime
  « comme un doigt » puis enchaîne. Quand il n'y a plus de coup, une nouvelle
  grille (graine décalée) repart — boucle infinie pour la capture.

## Notes

- Les médias générés (`*.mov`, `*.mp4`, `frames/`) sont git-ignorés.
- Idée énergie+ : ajouter une musique rythmée au montage (`ffmpeg -i video -i
  music -shortest -c:v copy -c:a aac out.mp4`).
