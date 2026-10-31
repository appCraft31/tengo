# Premier lancement — audit et correctif (TEN-4)

Constat de départ (journal du 2026-10-30) : **9,3 %** des nouveaux joueurs
reviennent à J7, alors que ceux qui restent jouent ~20 min par session. Le jeu
retient ; c'est l'entrée qui perd du monde.

## 1. Parcours chronométré (version 3.1.0)

Méthode : lecture du code (délais, transitions, demandes système), puis
contrôle au simulateur sur une installation neuve. Pas d'appareil vierge
disponible ; les durées de lecture sont des estimations pour un joueur qui lit
vite.

| t (s)   | Écran | Action du joueur |
|---------|-------|------------------|
| 0 → 1,5 | Écran de lancement | — |
| 1,5     | Menu (4 onglets) | — |
| 1,5     | **Feuille Game Center** « Se connecter » (joueur non connecté) ou bannière | fermer |
| 2       | **Demande ATT** (`viewDidAppear` + 0,5 s) | choisir |
| ~8      | Menu | trouver et toucher « Jouer » |
| ~10     | **Tutoriel, page 1/3** (relier) | lire, « Suivant » |
| ~17     | Tutoriel, page 2/3 (faire 10, animation) | lire, « Suivant » |
| ~25     | Tutoriel, page 3/3 (barème des points) | lire, « Jouer ! » |
| ~30     | **Première bulle touchable** | — |
| + partie | Fin de la 1re partie | panneau de fin |
| + 2,6   | (victoire) **demande d'avis App Store** | fermer |
| Rejouer | **interstitielle possible** dès la 1re partie terminée | attendre / fermer |

Soit **~30 s et 4 à 6 taps, dont 2 boîtes système**, avant de toucher une
bulle ; davantage pour un joueur qui lit chaque page.

## 2. Points de blocage relevés

1. **Deux demandes système d'entrée** (Game Center + ATT) avant même d'avoir
   vu le jeu. La demande ATT sans contexte est aussi la plus refusée.
2. **Menu à quatre onglets** comme premier écran : Jouer, Défi, Rush, Puzzles,
   Progression, Social, Boutique… alors que le joueur ne connaît pas la règle.
3. **Tutoriel en trois pages de texte**, dont une (le barème) inutile pour
   jouer la première partie.
4. **Le « × » du tutoriel ramène au menu sans rien retenir** : le tutoriel
   revient au « Jouer » suivant. Le joueur pressé tourne en rond.
5. **Demande d'avis sur la toute première victoire**, et **interstitielle
   possible au premier « Rejouer »** : la pub et les sollicitations arrivent
   avant que le joueur se soit attaché au jeu.

Le blocage principal est l'entrée : 1 + 2 + 4.

## 3. Correctif

Tant qu'aucune partie n'est terminée (`FirstRun.isPending`) :

- l'app s'ouvre **directement sur le tutoriel, ramené à 2 pages** (relier,
  faire 10) ; « Jouer ! » ou « × » lancent la partie ;
- **ni Game Center ni ATT** au lancement : ils arrivent après la fin de la
  première partie, sur le panneau de fin (temps mort naturel) ;
- **aucune requête pub avant le consentement** (l'interstitielle ne se
  précharge plus tant que la session pub n'a pas démarré — garde-fou 5.1.1(iv)) ;
- **pas de demande d'avis** sur la première partie ;
- pendant la **première session**, l'interstitielle exige 3 parties terminées
  (au lieu d'une ; 4 pour le déclencheur fin de partie automatique).

Nouveau parcours : écran de lancement → tutoriel (2 pages, ou « × ») →
**partie en ~8 à 15 s, 1 à 2 taps, aucune boîte système**.

Les joueurs déjà installés ne sont pas concernés : `FirstRun.bootstrap()` les
reconnaît à leurs traces (tutoriel vu, parties comptées, scores) et les classe
d'emblée « done ».

Limite assumée : le score de la toute première partie n'est pas soumis à Game
Center (l'authentification arrive juste après).

## 4. Mesure

Événements Firebase, **une seule fois par installation** :

| Événement | Paramètres |
|-----------|------------|
| `first_game_start` | `mode`, `seconds_since_first_open` |
| `first_game_complete` | `mode`, `score`, `success` (0/1), `seconds_since_first_open` |

Propriété utilisateur `first_game_done = yes` posée à la fin de la première
partie.

Lecture après la prochaine version :
- **Taux d'activation** = utilisateurs `first_game_complete` / `first_open`,
  par cohorte de version.
- **Temps d'entrée** = médiane de `seconds_since_first_open` sur
  `first_game_start` (objectif < 60 s).
- **Rétention J7** : rapport Rétention Firebase (ou Explorer › Cohortes) en
  comparant la cohorte de la 3.1.x à celle de la version suivante, et en
  segmentant par `first_game_done`.

À faire dans la console Firebase (`tengo-b62b1`) : marquer
`first_game_complete` comme **événement clé**, et déclarer les paramètres
`seconds_since_first_open` et `mode` en dimensions/métriques personnalisées
pour pouvoir les lire dans les rapports.

Android : mêmes noms d'événements et de paramètres à reprendre dans le
portage (`android/`, dépôt séparé), sinon les rapports ne sont pas
comparables.
