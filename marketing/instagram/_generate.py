#!/usr/bin/env python3
"""Génère les visuels publicitaires Instagram pour TEN•GO.
Style maison : fond crème zen, bulles pastel floues, headline gras + subline light,
maquette iPhone avec screenshot de l'app, CTA et branding TEN•GO.

Formats : portrait 1080x1350 (feed) et story 1080x1920.
Langues : fr, en. Sortie : fichiers HTML autonomes (images embarquées en base64).
"""
import base64, pathlib

ROOT = pathlib.Path(__file__).parent
ASSETS = ROOT / "_assets"

# ratio réel des screenshots app (1290 x 2796)
APP_RATIO = 2796 / 1290  # hauteur / largeur

# Cache base64 des screenshots
_cache = {}
def app_b64(n):
    if n not in _cache:
        data = (ASSETS / f"app-{n}.png").read_bytes()
        _cache[n] = base64.b64encode(data).decode()
    return _cache[n]

# 4 créations publicitaires, accroches orientées conversion
CREATIVES = [
    {
        "id": "1-fais10",
        "app": 1,
        "accent": "rgb(252,242,184)",  # jaune
        "fr": {"head": "Fais&nbsp;10.", "sub": "Encore. Et encore.", "cta": "Gratuit sur l’App&nbsp;Store"},
        "en": {"head": "Make&nbsp;10.", "sub": "Again. And again.", "cta": "Free on the App&nbsp;Store"},
    },
    {
        "id": "2-3min",
        "app": 2,
        "accent": "rgb(184,224,250)",  # bleu
        "fr": {"head": "3&nbsp;minutes.", "sub": "Le temps de vider la tête.", "cta": "Télécharge gratuitement"},
        "en": {"head": "3&nbsp;minutes.", "sub": "Just enough to clear your head.", "cta": "Download for free"},
    },
    {
        "id": "3-monde",
        "app": 4,
        "accent": "rgb(250,184,173)",  # corail
        "fr": {"head": "Ton cerveau<br>contre le monde.", "sub": "Grimpe au classement mondial.", "cta": "Joue maintenant"},
        "en": {"head": "Your brain<br>vs. the world.", "sub": "Climb the global leaderboard.", "cta": "Play now"},
    },
    {
        "id": "4-uneregle",
        "app": 5,
        "accent": "rgb(199,240,209)",  # vert
        "fr": {"head": "Une règle.", "sub": "Des milliers de parties.", "cta": "Gratuit sur l’App&nbsp;Store"},
        "en": {"head": "One rule.", "sub": "Thousands of games.", "cta": "Free on the App&nbsp;Store"},
    },
]

# Paramètres de mise en page par format (canvas en px = window-size de rendu)
FORMATS = {
    "portrait": {"w": 1080, "h": 1350, "head": 90, "sub": 48, "phone_w": 316,
                 "pad_top": 72, "gap": 44, "branding": 30, "cta": 38},
    "story":    {"w": 1080, "h": 1920, "head": 104, "sub": 54, "phone_w": 432,
                 "pad_top": 200, "gap": 80, "branding": 34, "cta": 42},
}

TEMPLATE = """<!doctype html><html lang="{lang}"><head><meta charset="utf-8">
<style>
  *{{margin:0;padding:0;box-sizing:border-box}}
  html,body{{width:{w}px;height:{h}px;overflow:hidden;background:rgb(247,242,235);
    font-family:"Avenir Next","AvenirNext-Heavy",-apple-system,sans-serif}}
  .scene{{position:relative;width:{w}px;height:{h}px;overflow:hidden;
    display:flex;flex-direction:column;align-items:center}}
  .bubble{{position:absolute;border-radius:50%;filter:blur(2px)}}
  .b1{{width:340px;height:340px;background:rgb(252,242,184);opacity:.16;top:-90px;left:-90px}}
  .b2{{width:240px;height:240px;background:rgb(250,184,173);opacity:.15;top:120px;right:-70px}}
  .b3{{width:380px;height:380px;background:rgb(184,224,250);opacity:.13;bottom:-130px;right:-110px}}
  .b4{{width:280px;height:280px;background:rgb(199,240,209);opacity:.15;bottom:180px;left:-90px}}
  .b5{{width:170px;height:170px;background:rgb(209,199,247);opacity:.20;top:46%;right:30px}}
  .text-area{{position:relative;z-index:2;text-align:center;margin-top:{pad_top}px;padding:0 80px}}
  .headline{{font-weight:900;font-size:{head}px;line-height:1.04;color:rgb(50,50,50);letter-spacing:-2px}}
  .accent-bar{{width:84px;height:10px;border-radius:5px;background:{accent};margin:28px auto 0}}
  .subline{{margin-top:28px;font-weight:300;font-size:{sub}px;line-height:1.32;color:rgb(110,110,110)}}
  .phone-wrap{{position:relative;z-index:2;margin-top:{gap}px;flex:1;display:flex;align-items:center}}
  .phone{{position:relative;width:{phone_w}px;height:{phone_h}px;background:#111;
    border-radius:{radius}px;padding:12px;box-shadow:0 40px 90px rgba(60,50,40,.22)}}
  .notch{{position:absolute;top:14px;left:50%;transform:translateX(-50%);
    width:{phone_w_third}px;height:30px;background:#0a0a0a;border-radius:15px;z-index:5}}
  .phone-screen{{width:100%;height:100%;border-radius:{radius_in}px;overflow:hidden;background:#000}}
  .phone-screen img{{width:100%;height:100%;object-fit:cover;object-position:top center;display:block}}
  .footer{{position:relative;z-index:3;display:flex;flex-direction:column;align-items:center;
    gap:26px;margin-top:{gap}px;margin-bottom:{foot_mb}px}}
  .cta{{display:inline-block;background:rgb(50,50,50);color:rgb(247,242,235);
    font-weight:900;font-size:{cta}px;letter-spacing:.3px;padding:{cta_py}px {cta_px}px;
    border-radius:999px;box-shadow:0 16px 36px rgba(60,50,40,.20)}}
  .branding{{display:flex;align-items:center;gap:10px;font-weight:900;
    font-size:{branding}px;letter-spacing:2px;color:rgb(120,120,120);opacity:.65}}
  .bdot{{width:11px;height:11px;border-radius:50%;background:{accent}}}
</style></head><body>
<div class="scene">
  <div class="bubble b1"></div><div class="bubble b2"></div><div class="bubble b3"></div>
  <div class="bubble b4"></div><div class="bubble b5"></div>
  <div class="text-area">
    <div class="headline">{head_txt}</div>
    <div class="accent-bar"></div>
    <div class="subline">{sub_txt}</div>
  </div>
  <div class="phone-wrap">
    <div class="phone"><div class="notch"></div>
      <div class="phone-screen"><img src="data:image/png;base64,{img}"></div>
    </div>
  </div>
  <div class="footer">
    <div class="cta">{cta_txt}</div>
    <div class="branding"><span>TEN</span><span class="bdot"></span><span>GO</span></div>
  </div>
</div></body></html>"""

def build():
    count = 0
    for fmt_name, F in FORMATS.items():
        phone_w = F["phone_w"]
        phone_h = round(phone_w * APP_RATIO)
        for cr in CREATIVES:
            for lang in ("fr", "en"):
                t = cr[lang]
                html = TEMPLATE.format(
                    lang=lang, w=F["w"], h=F["h"],
                    head=F["head"], sub=F["sub"], pad_top=F["pad_top"], gap=F["gap"],
                    branding=F["branding"], cta=F["cta"],
                    cta_py=round(F["cta"]*0.55), cta_px=round(F["cta"]*1.35),
                    accent=cr["accent"],
                    phone_w=phone_w, phone_h=phone_h,
                    phone_w_third=round(phone_w*0.34),
                    radius=round(phone_w*0.13), radius_in=round(phone_w*0.10),
                    foot_mb=54 if fmt_name == "portrait" else 120,
                    head_txt=t["head"], sub_txt=t["sub"], cta_txt=t["cta"],
                    img=app_b64(cr["app"]),
                )
                out = ROOT / fmt_name / lang / f"{cr['id']}.html"
                out.write_text(html)
                count += 1
    print(f"{count} fichiers HTML générés.")

if __name__ == "__main__":
    build()
