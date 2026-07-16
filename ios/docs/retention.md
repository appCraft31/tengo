# Rétention tenGO — features & mesure

Branche : `feature/daily-challenge`. Trois leviers de rétention sans backend
(offline-first, privacy-first), plus la mesure via Apple App Analytics.

## Features livrées

### 1. Défi du jour (Daily Challenge)
- Grille **identique pour tous** chaque jour, générée de façon déterministe
  (graine = date en UTC), **sans backend**.
- Un **twist** perturbateur tourne chaque jour (`jour % 3`) :
  - 🪨 **Obstacles** — pierres inertes qui cassent l'adjacence et bloquent la gravité.
  - ⚓ **Bulles ancrées** — ne tombent pas avec la gravité.
  - ❄️ **Bulles gelées** — inutilisables jusqu'à ce qu'un chemin adjacent fasse fondre le givre.
- Grille **garantie jouable** : génération-puis-validation avec dérivation de graine.
- Score soumis au **leaderboard Game Center dédié** ; rejeu de la même grille autorisé.

### 2. Série (Streak)
- Compteur de jours consécutifs, **zen** : jour manqué → retour à 1, sans pénalité.
- Affiché discrètement au menu (chip 🌿). Persistance locale.

### 3. Notifications locales
- Rappel quotidien (19h locale), **100 % local** (aucun serveur).
- Permission **différée** (après 3 parties) pour un meilleur taux d'acceptation.

## ⚙️ À configurer dans App Store Connect (hors code)

1. **Game Center → Classements** : créer le classement
   `com.tengo.leaderboard.daily` (idéalement en **réinitialisation quotidienne**).
   Cf. `AppConfig.gameCenterDailyLeaderboardID`.
2. Les notifications locales **ne nécessitent pas** d'entrée Info.plist
   ni de capability push (contrairement aux notifications distantes).

## 📊 Mesure — Apple App Analytics

Choix retenu : **zéro SDK, zéro code, privacy-first**. La mesure se fait via
les métriques natives d'App Store Connect (App Analytics). Limite assumée :
pas d'événements custom fins (ex. « % de complétion du défi »).

### Métriques à suivre (App Store Connect → Analytics)
- **Rétention** J1 / J7 / J28 — indicateur n°1 de l'effet des features.
- **Sessions par appareil actif** et **durée de session** — engagement.
- **Appareils actifs** (quotidiens / mensuels) — tendance globale.
- **Conversions / réinstallations** — effet des notifications de rappel.

### Méthode de lecture
- Comparer la rétention **avant / après** la mise en production de cette branche.
- Surveiller la **série** et les **notifications** : une hausse de la rétention J7
  est le signal attendu.

### Si une mesure plus fine devient nécessaire
Passer à un événementiel (Firebase Analytics ou table d'events Supabase) pour
tracer précisément : `dailyChallengePlayed`, `dailyChallengeCompleted`,
`streakReached`, `notifPermissionGranted`, `notifOpened`. À décider plus tard.
