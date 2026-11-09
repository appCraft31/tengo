#!/usr/bin/env python3
"""Fabrique les polices de repli CJK d'Assets/Fonts/Fallback à partir des
fichiers variables du dépôt google/fonts (licence OFL 1.1).

Usage :
  python3 -I build_cjk_fallbacks.py <dossier_sources> <unity>/Assets/Fonts/Fallback <ios>/tenGO/Localizable.xcstrings

<dossier_sources> contient NotoSansJP[wght].ttf, NotoSansKR[wght].ttf et
NotoSansSC[wght].ttf, téléchargés de https://github.com/google/fonts/tree/main/ofl
(notosansjp, notosanskr, notosanssc). Il faut fontTools (pip install fonttools).

- Instance figée en Bold (wght 700) : la graisse par défaut des textes
  tenGO (Nunito Bold) ; TMP ne lit que l'instance par défaut d'un fichier
  variable (Thin pour ces polices).
- Réduites aux caractères des traductions ja, ko et zh-Hans du catalogue iOS,
  plus l'ASCII, la ponctuation CJK, les formes pleine chasse et tous les
  kana : ~150 Ko au lieu de 10 à 18 Mo. À relancer quand les textes changent.
"""
import json, pathlib, sys
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

src, out, xcstrings = map(pathlib.Path, sys.argv[1:4])
out.mkdir(parents=True, exist_ok=True)
data = json.loads(xcstrings.read_text("utf-8"))
chars = {"ja": set(), "ko": set(), "zh-Hans": set()}

def walk(o, lang):
    if isinstance(o, dict):
        for k, v in o.items():
            if k == "value" and isinstance(v, str):
                chars[lang].update(v)
            else:
                walk(v, lang)
    elif isinstance(o, list):
        for v in o:
            walk(v, lang)

for entry in data["strings"].values():
    for lang, loc in entry.get("localizations", {}).items():
        if lang in chars:
            walk(loc, lang)

base = set(range(0x20, 0x7F)) | set(range(0x3000, 0x3100)) | set(range(0xFF00, 0xFFF0)) | {0x00D7, 0x2026, 0x00B7, 0x2014, 0x2013}
cjk = {ord(c) for s in chars.values() for c in s} | base

FONTS = [
    ("NotoSansJP[wght].ttf", "NotoSansJP-Bold.ttf"),
    ("NotoSansKR[wght].ttf", "NotoSansKR-Bold.ttf"),
    ("NotoSansSC[wght].ttf", "NotoSansSC-Bold.ttf"),
]
for rel, name in FONTS:
    font = TTFont(src / rel)
    font = instancer.instantiateVariableFont(font, {"wght": 700}, updateFontNames=True)
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.notdef_outline = True
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=sorted(cjk))
    sub.subset(font)
    path = out / name
    font.save(path)
    print(f"{name}: {len(font.getBestCmap())} glyphes, {path.stat().st_size // 1024} Ko")
