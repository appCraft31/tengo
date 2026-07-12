# Kit Google Ads HTML5 — TEN·GO

Bannières **HTML5 responsives** prêtes à uploader sur Google Ads (réseau Display).
Un seul design adaptatif (`template.html`) décliné en 8 formats par `generate.py`.

## ⚠️ À lire avant : Display ≠ App Campaign

- Le **HTML5 n'est accepté que par les campagnes Display** (Google Display Network).
- Les **App Campaigns** (celles qui optimisent l'install iOS) **n'acceptent pas le HTML5** :
  seulement images, vidéos et textes. Pour pousser des installs, utilisez une App Campaign
  avec les **titres/descriptions** déjà fournis + des visuels/vidéos.
- Ces bannières servent donc pour : **notoriété**, **remarketing web**, trafic vers une
  landing page ou la fiche App Store via le web.

## Formats générés (`build/`)

| Fichier | Dimensions | Nom Google |
|---|---|---|
| `300x250.zip` | 300×250 | Medium Rectangle ★ (le plus diffusé) |
| `336x280.zip` | 336×280 | Large Rectangle |
| `728x90.zip`  | 728×90  | Leaderboard |
| `970x250.zip` | 970×250 | Billboard |
| `300x600.zip` | 300×600 | Half-Page |
| `160x600.zip` | 160×600 | Wide Skyscraper |
| `320x100.zip` | 320×100 | Large Mobile Banner |
| `320x50.zip`  | 320×50  | Mobile Banner |

Chaque ZIP ≈ 2 Ko (limite Google : 150 Ko), 1 seul fichier `index.html` à la racine.

## Conformité Google Ads ✓

- Balise `<meta name="ad.size">` présente et conforme aux dimensions.
- `clickTag` standard (variable JS) — Google injecte sa propre URL de tracking.
- Aucun appel réseau externe (tout est inline) → validation sans erreur.
- Animation finie (~3,6 s de scène + pulse CTA borné), bien < 30 s.
- Bord léger intégré (`box-shadow inset`) comme recommandé sur fond clair.

## Personnaliser puis (re)générer

1. **URL de destination** : éditez `CLICKTAG` en haut de `generate.py`
   (remplacez `id0000000000` par votre vrai ID App Store, ou une landing page).
2. Textes / couleurs : éditez `template.html` (CTA « Jouer gratuitement »,
   tagline, variables CSS `--violet`, `--peach`…).
3. Régénérez :
   ```bash
   python3 generate.py
   ```
4. **QA visuel** : ouvrez `build/preview.html` dans un navigateur (galerie des 8 formats).

## Uploader sur Google Ads

Campagne **Display** → Annonces → **+** → *Importer une annonce display* →
**Importer HTML5** → glissez chaque `.zip`. Renseignez l'URL finale + titre/description
de secours. (L'option HTML5 nécessite un compte Google Ads éligible — historique de
diffusion ; sinon utilisez les annonces display responsives images.)

## 20 templates distincts (interstitiels) — `templates.py`

Pour de l'A/B test, `templates.py` génère **20 designs visuellement différents**, chacun
décliné en **320×480 et 480×320** (40 ZIP au total, ~1,8 Ko pièce), dans `build-templates/`.

10 archétypes de mise en page × 6 palettes × 6 accroches :

| # | Archétype | Idée visuelle |
|---|---|---|
| chain | démo gameplay centrée (6 — 4 = 10) |
| hero | gros « 10 » avec bulles en orbite |
| grid | fond plein de bulles + carte centrale |
| phone | maquette téléphone + mini-grille |
| split | bloc couleur en diagonale (titre / démo) |
| piano | bulles-notes + clavier (axe musique) |
| score | gros score + chips « +10 » + record |
| daily | carte calendrier + série (défi du jour) |
| type | titre typographique plein cadre |
| celebrate | confettis-bulles « PARFAIT +100 » |

```bash
python3 templates.py            # → build-templates/ + gallery.html
open build-templates/gallery.html
```

Mêmes garanties de conformité que les bannières (meta ad.size, clickTag, < 150 Ko,
aucun appel externe, animations finies, contraste AA y compris thème sombre).
L'URL `CLICKTAG` se règle en haut de `templates.py`.

## Structure

```
html5/
├── template.html     ← source unique (design responsive)
├── generate.py       ← génère build/ + zips + preview
├── README.md
└── build/            ← (généré) à NE PAS versionner si besoin
    ├── 300x250/index.html …
    ├── 300x250.zip …
    └── preview.html
```
