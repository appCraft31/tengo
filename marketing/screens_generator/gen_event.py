#!/usr/bin/env python3
# Visuels de l'événement intégré App Store (In-App Event) de la 3.0.
#
# Deux formats, tous deux imposés par App Store Connect :
#   EVENT_CARD         1920 × 1080  — la vignette de découverte
#   EVENT_DETAILS_PAGE 1920 × 3413  — le grand visuel de la page de détail
#
# Sans aucun texte : Apple superpose lui-même le nom et la description de
# l'événement, et un visuel qui écrit par-dessus se fait refuser. Les deux
# compositions sont volontairement différentes — elles se suivent à l'écran.
#
# Usage : python3 gen_event.py [card|detail]
import os, subprocess, sys

S = os.path.dirname(os.path.abspath(__file__))
R = os.path.join(S, "render")
OUT = os.path.abspath(os.path.join(S, "..", "event"))
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
LANG = "fr"  # langue principale de la fiche

FORMATS = {
    "card":   dict(w=960, h=540,  dpr=2, out="event_card.png"),
    # 960×1706 @2 donne 3412 : la hauteur exacte attendue est 3413, complétée
    # d'une ligne de fond par sips (Chrome ne rend pas de demi-pixel).
    "detail": dict(w=960, h=1706, dpr=2, out="event_detail.png", pad=(1920, 3413)),
}

CSS = """
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:100%;height:100%;overflow:hidden}
body{position:relative;background:#F7F2EB}
.deco{position:absolute;border-radius:50%}
.phone{position:absolute;border-radius:2.2vw;overflow:hidden;border:.45vw solid #35322E;
 background:#35322E;box-shadow:0 1.6vw 3.4vw rgba(50,42,30,.34)}
.phone img{display:block;width:100%;height:auto}
"""

def phone(img, style):
    return f'<div class="phone" style="{style}"><img src="{R}/{img}"></div>'

def deco(size, color, pos, op=".5"):
    return f'<i class="deco" style="width:{size};aspect-ratio:1;background:{color};opacity:{op};{pos}"></i>'

def card_html():
    # Trois écrans en éventail : l'accueil au centre, une grille en cours et le
    # profil de part et d'autre — l'étendue de la 3.0 en un coup d'œil, sans un
    # mot. L'écran Duel, lui, est trop vide sur un compte de vitrine.
    body = "".join([
        deco("34%", "#D1F2E0", "top:-14%;left:-6%", ".75"),
        deco("22%", "#FFB8DB", "top:6%;right:2%", ".42"),
        deco("26%", "#9EDBFF", "bottom:-12%;right:22%", ".38"),
        deco("18%", "#FFF59E", "bottom:4%;left:12%", ".5"),
        phone(f"full_game_{LANG}.png",
              "width:19%;left:16%;top:16%;transform:rotate(-8deg)"),
        phone(f"full_profile_{LANG}.png",
              "width:19%;right:16%;top:16%;transform:rotate(8deg)"),
        phone(f"full_menu_{LANG}.png",
              "width:23%;left:50%;top:9%;transform:translateX(-50%);z-index:2"),
    ])
    return f'<!doctype html><html><head><meta charset="utf-8"><style>{CSS}</style></head><body>{body}</body></html>'

def detail_html():
    # 1920×3413, soit un portrait 9:16 — pas une bande étroite : la composition
    # reprend l'éventail de la carte, retourné à la verticale. L'accueil devant,
    # la grille et le profil derrière, largement décalés pour qu'aucun écran
    # n'en coupe un autre.
    body = "".join([
        deco("58%", "#D1F2E0", "top:-12%;left:-22%", ".7"),
        deco("40%", "#FFB8DB", "top:8%;right:-14%", ".38"),
        deco("34%", "#9EDBFF", "top:44%;left:-14%", ".34"),
        deco("46%", "#FFF59E", "bottom:-6%;right:-16%", ".4"),
        deco("30%", "#CCB8FF", "bottom:8%;left:-10%", ".3"),
        phone(f"full_game_{LANG}.png",
              "width:40%;left:4%;top:9%;transform:rotate(-7deg)"),
        phone(f"full_profile_{LANG}.png",
              "width:40%;right:4%;bottom:9%;transform:rotate(7deg)"),
        phone(f"full_menu_{LANG}.png",
              "width:52%;left:50%;top:50%;transform:translate(-50%,-50%);z-index:2"),
    ])
    return f'<!doctype html><html><head><meta charset="utf-8"><style>{CSS}</style></head><body>{body}</body></html>'


def render(kind):
    cfg = FORMATS[kind]
    html = card_html() if kind == "card" else detail_html()
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(R, f"tmp_event_{kind}.html")
    with open(path, "w", encoding="utf-8") as f:
        f.write(html)
    out = os.path.join(OUT, cfg["out"])
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    f"--window-size={cfg['w']},{cfg['h']}",
                    f"--force-device-scale-factor={cfg['dpr']}",
                    f"--screenshot={out}", f"file://{path}"],
                   check=True, capture_output=True)
    os.remove(path)
    if cfg.get("pad"):
        w, h = cfg["pad"]
        subprocess.run(["sips", "--padToHeightWidth", str(h), str(w),
                        "--padColor", "F7F2EB", out, "--out", out],
                       check=True, capture_output=True)
    print(out)

if __name__ == "__main__":
    for kind in (sys.argv[1:] or list(FORMATS)):
        render(kind)
