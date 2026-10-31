# Pubs : vidéo récompensée, espacement des plein écran et suivi du revenu pour 1 000 affichages (TEN-3)

Origine : audit de la semaine du 23 octobre 2026. Les plein écran rapportaient
**environ 4 € pour 1 000 affichages** (contre ~12 € sur les autres apps du
compte), et la vidéo récompensée ne servait qu'à gagner des pièces depuis le menu.

## Ce qui change (iOS, version suivant la 3.1.0)

### Vidéo récompensée en partie (mode normal uniquement)

| Emplacement | Quand | Récompense | Limite |
|---|---|---|---|
| `second_chance` | la grille n'a plus aucun coup | la grille est mélangée, la partie continue avec le score | 1 par partie |
| `hint` | 20 s sans geste, aucun indice en poche, une vidéo prête | un indice (chemin gagnant qui clignote) | 2 par partie |
| `menu_coins` (existant) | bouton du menu | +10 pièces | 5 par jour |

- La seconde chance n'est proposée que si le mélange débloque vraiment la
  grille (le mélange est tiré avant d'afficher l'offre).
- Défi du jour, puzzles, duels et Rush sont exclus (grilles partagées ou
  chrono : une vidéo fausserait classement et étoiles).
- La récompense est versée à la **fermeture** de la vidéo, pour que l'effet se
  voie. Si le joueur coupe la vidéo avant la fin, la partie se termine normalement.
- Même bloc d'annonces qu'avant (récompensé interstitiel
  `ca-app-pub-4352408747876735/2440159179`) : rien à créer dans AdMob.

### Plein écran plus espacés (`InterstitialAdManager`)

| Règle | Avant | Après |
|---|---|---|
| Délai minimal entre deux plein écran | 60 s | **150 s**, vidéo récompensée comprise |
| Premier plein écran possible (⌂, Rejouer) | après la 1re partie | après la **2e** partie |
| Plein écran automatique en fin de partie | 1 sur 3, dès la 2e partie | **1 sur 4**, dès la **3e** partie |

## Suivi du revenu pour 1 000 affichages

### Données envoyées

- `ad_impression` (Firebase, valeur payée par AdMob) : un événement par
  affichage, avec `ad_format` = `interstitial` ou `rewarded_interstitial`, plus
  `value` et `currency`. La vidéo récompensée n'en envoyait aucun jusqu'ici.
- Entonnoir de la vidéo récompensée, avec le paramètre `placement` :
  `rewarded_offer_view` → `rewarded_started` → `rewarded_completed`.

### Où lire le chiffre

1. **AdMob** (référence, sans attendre) : Rapports → regrouper par *Format* puis
   par *Bloc d'annonces*, métrique **eCPM observé**, sur 7 jours glissants.
   Point de départ : interstitiel ≈ 4 €.
2. **Firebase Analytics** : Événements → `ad_impression` → ventiler par
   `ad_format` ; revenu pour 1 000 = somme de `value` / nombre d'événements × 1000.
   Déclarer `placement` comme dimension personnalisée (portée événement) pour
   comparer les emplacements.
3. **Revenu par joueur** : comparer le revenu pub par utilisateur actif
   quotidien avant/après la mise à jour. Moins de plein écran peut faire monter
   le revenu pour 1 000 sans faire monter le total : c'est le revenu par joueur
   actif qui dit si la décision était bonne.

### Seuils pour décider

- 2 semaines après la sortie : revenu pour 1 000 en plein écran ≥ 6 € **et**
  revenu pub par joueur actif ≥ celui d'avant → on garde.
- Taux `rewarded_completed / rewarded_offer_view` sous 15 % pour un
  emplacement → revoir le texte ou le moment de l'offre.
- Revenu pub par joueur actif en baisse de plus de 10 % → revenir à 1 plein
  écran automatique sur 3 en gardant les vidéos récompensées.
