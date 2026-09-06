# Outils de contenu tenGO

Deux outils en ligne de commande, à lancer depuis la racine du dépôt.

## `grid_audit` — audit des grilles du Défi du jour (issue #21)

Compile et exécute **le code réel du jeu** (`GridModel`, `DailyChallenge`,
`GridValidator`) plutôt qu'une copie : une divergence entre l'outil et le jeu
est donc impossible par construction.

```bash
swiftc -O -o /tmp/grid_audit \
  ios/tenGO/GridModel.swift ios/tenGO/BubbleModel.swift ios/tenGO/SeededGenerator.swift \
  ios/tenGO/DailyChallenge.swift ios/tenGO/AppConfig.swift ios/tenGO/GameState.swift \
  ios/tenGO/GridValidator.swift ios/tools/grid_audit/main.swift
/tmp/grid_audit 60      # audite les 60 prochains jours
```

Sortie : une ligne par jour (twist, paires immédiates, groupes courts, coups
avant blocage, score théorique, difficulté visée et obtenue, temps de
construction), puis un résumé. **Code de sortie non nul si une grille échoue la
validation** — utilisable tel quel comme test de non-régression avant une
release.

À relancer après toute modification de `GridModel`, `GridValidator` ou de la
génération du Défi : c'est le seul garde-fou contre une grille du jour
injouable ou triviale.

Il sert aussi à **recalibrer** les bornes de `GridValidator.difficultyScore` :
des bornes devinées avaient produit un score saturé (74-95 pour toutes les
grilles), inutilisable pour viser une difficulté.

## `puzzle_gen/` — génération des niveaux du mode Puzzles (issue #19)

```bash
swiftc -O -o /tmp/puzzle_gen ios/tenGO/ScoreRules.swift ios/tools/puzzle_gen/main.swift
/tmp/puzzle_gen > ios/tenGO/PuzzleCatalog.swift
```

Génère les niveaux et **prouve** leur solvabilité complète par recherche
exhaustive avant de les retenir ; les seuils d'étoiles dérivent du meilleur
score réellement atteignable.

⚠️ Contrairement à `grid_audit`, cet outil **recopie** la gravité et l'adjacence
au lieu de les importer, parce qu'il manipule une représentation compacte propre
à la génération (masques de bits). Toute modification de ces règles dans
`GridModel` doit être répercutée ici, sinon les niveaux publiés deviennent faux.

Le **barème**, lui, est importé depuis `ScoreRules` : les seuils d'étoiles en
dérivent directement, et une copie qui dérive rendrait les 20 niveaux faux sans
que rien n'échoue.

## `score_test/` — barème du score (issue #15)

```bash
swiftc -O -o /tmp/score_test ios/tenGO/ScoreRules.swift ios/tools/score_test/main.swift
/tmp/score_test
```

Vérifie le barème sur le code réel : valeurs attendues de 0 à 10 bulles,
croissance stricte, rendement par bulle croissant (la propriété que le GDD §34
appelle « longer chains = exponentially better »), et chaîne maximale possible
sur un plateau de 63 cellules. Sort en erreur si un cas échoue — c'est le
substitut au target XCTest, que le projet n'a pas.
