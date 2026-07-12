#!/usr/bin/env python3
"""Génère le kit Google Ads HTML5 de TEN·GO à partir de template.html.
Pour chaque format : un dossier build/<WxH>/index.html + un build/<WxH>.zip prêt à uploader.
Produit aussi build/preview.html (galerie pour QA visuel)."""
import os, shutil, zipfile

BASE = os.path.dirname(os.path.abspath(__file__))

# ⚠️ URL de destination du clic — À REMPLACER par votre App Store / landing page
CLICKTAG = "https://apps.apple.com/app/ten-go-calcul-mental/id0000000000"

# Formats imposés par le placement Google Ads (interstitiel plein écran)
SIZES = [
    (320, 480),   # Interstitiel portrait   ← REQUIS
    (480, 320),   # Interstitiel paysage    ← REQUIS
    # --- formats Display additionnels (réutilisables ailleurs) ---
    (300, 250),   # Medium Rectangle
    (336, 280),   # Large Rectangle
    (728, 90),    # Leaderboard
    (970, 250),   # Billboard
    (300, 600),   # Half Page
    (160, 600),   # Wide Skyscraper
    (320, 100),   # Large Mobile Banner
    (320, 50),    # Mobile Banner
]

TPL = open(os.path.join(BASE, "template.html"), encoding="utf-8").read()
OUT = os.path.join(BASE, "build")
if os.path.exists(OUT):
    shutil.rmtree(OUT)
os.makedirs(OUT)

gallery = [
    "<!doctype html><meta charset=utf-8><title>TEN·GO — Google Ads HTML5</title>",
    "<body style='font-family:sans-serif;background:#f4f4f7;padding:24px'>",
    "<h1>TEN·GO — Kit Google Ads HTML5</h1>",
    "<p>Aperçu des 8 formats Display. Cliquez pour tester le clickTag.</p>",
]

for w, h in SIZES:
    name = f"{w}x{h}"
    folder = os.path.join(OUT, name)
    os.makedirs(folder)
    html = (TPL.replace("{{AD_W}}", str(w))
               .replace("{{AD_H}}", str(h))
               .replace("{{CLICKTAG}}", CLICKTAG))
    index = os.path.join(folder, "index.html")
    with open(index, "w", encoding="utf-8") as f:
        f.write(html)
    # ZIP prêt pour l'upload Google Ads (un seul fichier index.html à la racine)
    with zipfile.ZipFile(os.path.join(OUT, name + ".zip"), "w", zipfile.ZIP_DEFLATED) as z:
        z.write(index, "index.html")
    size_kb = os.path.getsize(os.path.join(OUT, name + ".zip")) / 1024
    gallery.append(
        f"<div style='display:inline-block;margin:14px;vertical-align:top'>"
        f"<div style='font:12px sans-serif;color:#555;margin-bottom:4px'>{name} "
        f"&middot; {size_kb:.1f} Ko</div>"
        f"<iframe src='{name}/index.html' width='{w}' height='{h}' scrolling='no' "
        f"style='border:1px solid #ccc;background:#fff'></iframe></div>"
    )

gallery.append("</body>")
with open(os.path.join(OUT, "preview.html"), "w", encoding="utf-8") as f:
    f.write("\n".join(gallery))

print(f"OK — {len(SIZES)} formats générés dans {OUT}")
for w, h in SIZES:
    kb = os.path.getsize(os.path.join(OUT, f"{w}x{h}.zip")) / 1024
    print(f"  {w}x{h}.zip  ({kb:.1f} Ko)")
