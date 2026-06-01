# Événement intégré App Store — tenGO

Type recommandé : **Défi (Challenge)** — centré sur le Défi du jour.
Visuel : `~/Desktop/tenGO_event_card.png` (1920×1080). Lien profond : voir plus bas.

> Limites Apple : Nom ≤ 30 caractères · Brève description ≤ 50 · Description longue ≤ 120.
> Pas d'emoji (refusés, comme pour les notes de version).

## Contenu par langue

| Locale | Nom (≤30) | Brève (≤50) | Longue (≤120) |
|--------|-----------|-------------|----------------|
| fr-FR | Défi du jour & nouveautés | Un casse-tête inédit chaque jour, et plus ! | Nouveau : Défi du jour avec twists, thèmes aux fonds animés, boutique de pièces et menu repensé. |
| en-US | Daily Challenge & more | A fresh brain-teaser every day, and more! | New: Daily Challenge with twists, animated themed backgrounds, a coin shop and a redesigned menu. |
| es-ES | Reto diario y novedades | Un nuevo rompecabezas cada día, ¡y más! | Nuevo: Reto diario con giros, fondos temáticos animados, tienda de monedas y menú renovado. |
| de-DE | Tägliche Challenge & mehr | Jeden Tag ein neues Rätsel, und mehr! | Neu: Tägliche Challenge mit Twists, animierte Themen-Hintergründe, Münz-Shop und neues Menü. |
| it | Sfida del giorno e novità | Un nuovo rompicapo ogni giorno, e altro! | Novità: Sfida del giorno con twist, sfondi animati a tema, negozio di monete e menu rinnovato. |
| pt-BR | Desafio do dia e novidades | Um novo quebra-cabeça todo dia, e mais! | Novo: Desafio do dia com reviravoltas, fundos animados, loja de moedas e menu renovado. |
| nl-NL | Dagelijkse uitdaging & meer | Elke dag een nieuwe puzzel, en meer! | Nieuw: dagelijkse uitdaging met twists, thema-achtergronden, muntenwinkel en nieuw menu. |
| ja | 今日のチャレンジと新機能 | 毎日新しいパズル、ほかにも！ | 新機能：仕掛け付きの今日のチャレンジ、動くテーマ背景、コインショップ、刷新メニュー。 |
| ko | 오늘의 도전과 새 기능 | 매일 새로운 퍼즐, 그리고 더! | 신규: 변형이 있는 오늘의 도전, 움직이는 테마 배경, 코인 상점, 새 메뉴. |
| zh-Hans | 每日挑战与新功能 | 每天一道新谜题，还有更多！ | 全新：带机关的每日挑战、动态主题背景、金币商店和全新菜单。 |

## Étapes dans App Store Connect
1. Mes apps → tenGO → **Événements intégrés** → ＋
2. Badge : **Défi (Challenge)**. Nom de référence (interne) : « Défi du jour ».
3. Renseigner Nom / Brève / Longue par langue (tableau ci-dessus).
4. **Visuel** : importer `tenGO_event_card.png` (1920×1080).
5. **Dates** : début / fin + fuseau + date de publication.
6. **Lien profond** (voir ci-dessous).
7. Soumettre l'événement pour revue (séparée de l'app, ~24-48 h).

## Lien profond
Le code route déjà les liens vers le Défi du jour (`DeepLink.swift`) :
- **Schéma personnalisé** : `tengo://daily` — fonctionne (test : `xcrun simctl openurl <udid> "tengo://daily"`).
- **Universal link** : `https://<ton-domaine>/daily` — géré côté code (`continue userActivity`).

⚠️ **Les événements intégrés exigent un universal link (https), pas un schéma personnalisé.** Pour l'activer il faut :
1. Un **domaine** que tu contrôles (ex. `tengo.app`).
2. Le fichier **AASA** `https://<domaine>/.well-known/apple-app-site-association` :
   ```json
   { "applinks": { "details": [ { "appIDs": ["JDKABK93UH.AppCraft31.tenGO"], "components": [ { "/": "/daily*" } ] } ] } }
   ```
3. L'**entitlement Associated Domains** : `applinks:<domaine>` (à ajouter dans tenGO.entitlements + profil de provisioning).

Une fois ces 3 points en place, le lien `https://<domaine>/daily` ouvrira directement le Défi du jour, et pourra être utilisé comme lien profond de l'événement.
