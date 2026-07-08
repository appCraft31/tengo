#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Génère 20 TEMPLATES HTML5 DISTINCTS pour TEN·GO (interstitiels Google Ads).
10 archétypes de design x palettes/accroches, chacun décliné en 320x480 + 480x320.
Sortie : build-templates/<id>_<WxH>/index.html + .zip + gallery.html (QA)."""
import os, shutil, zipfile

# ⚠️ URL de destination — À REMPLACER (App Store id / landing page)
CLICKTAG = "https://apps.apple.com/app/ten-go-calcul-mental/id0000000000"

SIZES = [(320, 480), (480, 320)]   # seuls formats acceptés par le placement

# ───────────────────────── palettes ─────────────────────────
PALETTES = {
 'peach':  dict(bg1='#FFF3E9', bg2='#E9F1FF', ink='#2E2A3A', accent='#6C63FF',
                b=['#FFB4A2','#5FD9C0','#FFD56B','#8FD0FF','#C8A8FF']),
 'mint':   dict(bg1='#E8FBF3', bg2='#E6F2FF', ink='#1E3A34', accent='#13B187',
                b=['#5FD9C0','#8FD0FF','#FFD56B','#FFB4A2','#A8E6A0']),
 'sunset': dict(bg1='#FFE8D6', bg2='#FFD9E6', ink='#3A2030', accent='#FF5E8A',
                b=['#FF8FA3','#FFB36B','#FFD56B','#C8A8FF','#8FD0FF']),
 'night':  dict(bg1='#272246', bg2='#1A2046', ink='#F4F2FF', accent='#7CF5C8', oa='#10242B',
                b=['#FF8FA3','#7CF5C8','#FFD56B','#8FB8FF','#C8A8FF']),
 'sky':    dict(bg1='#E6F2FF', bg2='#ECE9FF', ink='#22304A', accent='#3B82F6',
                b=['#8FD0FF','#5FD9C0','#FFD56B','#FFB4A2','#C8A8FF']),
 'coral':  dict(bg1='#FFEDE6', bg2='#FFE0EC', ink='#3A2228', accent='#FF6B5E',
                b=['#FFB4A2','#FFC56B','#FF8FA3','#8FD0FF','#5FD9C0']),
}

# ───────────────────────── accroches (FR) ─────────────────────────
COPY = {
 'zen':    dict(h1='Calcul mental',  h2='zen',            sub='Sans chrono, sans pression',      cta='Jouer gratuitement'),
 'brain':  dict(h1='Réveillez',      h2='votre cerveau',  sub='5 minutes par jour suffisent',    cta='Entraîner mon cerveau'),
 'daily':  dict(h1='Un défi',        h2='chaque jour',    sub='Grimpez au classement mondial',   cta='Relever le défi'),
 'simple': dict(h1='Reliez et',      h2='faites 10',      sub='Facile à apprendre, dur à lâcher',cta='Essayer maintenant'),
 'free':   dict(h1='Gratuit',        h2='et hors-ligne',  sub='Jouez où vous voulez',            cta='Télécharger'),
 'music':  dict(h1='Vos chiffres',   h2='font la musique',sub='Chaque chaîne devient mélodie',   cta='Jouer gratuitement'),
}

# ───────────────────────── CSS de base (partagé) ─────────────────────────
BASE = """
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:100%;height:100%;overflow:hidden}
body{font-family:-apple-system,"Segoe UI",system-ui,Roboto,"Helvetica Neue",sans-serif}
.stage{position:relative;display:flex;width:100%;height:100%;overflow:hidden;
  text-decoration:none;cursor:pointer;color:var(--ink);
  background:linear-gradient(150deg,var(--bg1),var(--bg2));
  box-shadow:inset 0 0 0 1px rgba(0,0,0,.06)}
.logo{font-weight:800;letter-spacing:.5px;line-height:1}
.logo .d{color:var(--accent)}
.bub{border-radius:50%;display:flex;align-items:center;justify-content:center;flex:none;
  color:#fff;font-weight:800;box-shadow:0 4px 10px rgba(0,0,0,.14);
  text-shadow:0 1px 2px rgba(0,0,0,.18)}
.deco{position:absolute;border-radius:50%;opacity:.3;filter:blur(2px);pointer-events:none}
.cta{display:inline-block;background:var(--accent);color:var(--oa,#fff);font-weight:800;
  border-radius:999px;white-space:nowrap;text-align:center;box-shadow:0 8px 20px rgba(0,0,0,.2)}
@keyframes fin{from{opacity:0;transform:translateY(14px)}to{opacity:1;transform:none}}
@keyframes pop{from{transform:scale(0)}to{transform:scale(1)}}
@keyframes pulse{0%,100%{transform:scale(1)}50%{transform:scale(1.05)}}
@keyframes drift{0%,100%{transform:translateY(0)}50%{transform:translateY(-10%)}}
@keyframes draw{from{transform:scaleX(0)}to{transform:scaleX(1)}}
@keyframes shine{0%{transform:translateX(-120%)}60%,100%{transform:translateX(220%)}}
@media(prefers-reduced-motion:reduce){*{animation-duration:.001s!important;animation-delay:0s!important}}
"""

SHELL = """<!doctype html><html lang="fr"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="ad.size" content="width={{W}},height={{H}}">
<title>TEN·GO</title>
<script>var clickTag="{{CT}}";</script>
<style>{{BASE}}
{{ACSS}}</style></head>
<body><a class="stage {{NAME}}" id="exit" target="_blank" rel="noopener" href="#" style="{{STYLE}}">
{{HTML}}
</a><script>document.getElementById('exit').href=window.clickTag||'#';</script></body></html>"""

LOGO = '<span class="logo">TEN<span class="d">·</span>GO</span>'

def chain():  # mini-démo gameplay 6 — 4 = 10
    return ('<span class="bub b6">6</span><span class="lnk"></span>'
            '<span class="bub b4">4</span><span class="eq">=10</span>')

# ───────────────────────── archétypes (CSS, HTML(copy,pal)) ─────────────────────────
ARCH = {}

# A — CHAIN : démo centrée, logo / bulles / titre / cta
ARCH['chain'] = ("""
.chain{align-items:center;justify-content:center}
.chain .wrap{display:flex;flex-direction:column;align-items:center;text-align:center;gap:3vmin;padding:7vmin}
.chain .logo{font-size:7vmin;animation:fin .6s both}
.chain .scene{display:flex;align-items:center;gap:2.3vmin;animation:fin .6s .15s both}
.chain .bub{width:15vmin;height:15vmin;font-size:7vmin}
.chain .b6{background:var(--b0)}.chain .b4{background:var(--b1)}
.chain .lnk{width:5vmin;height:1.4vmin;border-radius:4px;background:var(--accent);transform-origin:left;animation:draw .4s .5s both}
.chain .eq{background:var(--accent);color:var(--oa,#fff);font-weight:800;font-size:4.6vmin;padding:1.2vmin 2.6vmin;border-radius:999px}
.chain h1{font-size:8.4vmin;line-height:1.04;font-weight:800;animation:fin .6s .3s both}
.chain .hi{color:var(--accent)}
.chain .sub{font-size:3.8vmin;opacity:.72;animation:fin .6s .45s both}
.chain .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;margin-top:1vmin;animation:fin .6s .65s both,pulse 1.8s 1.8s ease-in-out 6}
.chain .d1{width:55vmin;height:55vmin;background:var(--b0);top:-18vmin;left:-16vmin}
.chain .d2{width:42vmin;height:42vmin;background:var(--b3);bottom:-15vmin;right:-13vmin}
""", lambda c,p: f'''<i class="deco d1"></i><i class="deco d2"></i><div class="wrap">{LOGO}
<div class="scene">{chain()}</div>
<h1>{c['h1']}<br><span class="hi">{c['h2']}</span></h1>
<p class="sub">{c['sub']}</p><span class="cta">{c['cta']}</span></div>''')

# B — HERO 10 : gros disque "10" + bulles en orbite
ARCH['hero'] = ("""
.hero{align-items:center;justify-content:center}
.hero .wrap{display:flex;flex-direction:column;align-items:center;text-align:center;gap:4vmin;padding:7vmin}
.hero .logo{font-size:6.5vmin;animation:fin .6s both}
.hero .core{position:relative;width:46vmin;height:46vmin;display:flex;align-items:center;justify-content:center;animation:fin .6s .15s both}
.hero .ten{width:38vmin;height:38vmin;border-radius:50%;display:flex;align-items:center;justify-content:center;
  font-size:18vmin;font-weight:800;color:var(--oa,#fff);background:var(--accent);box-shadow:0 14px 30px rgba(0,0,0,.22)}
.hero .orb{position:absolute;width:12vmin;height:12vmin;font-size:5.6vmin;animation:drift 3s ease-in-out infinite}
.hero .o1{background:var(--b0);top:-3vmin;left:2vmin;animation-delay:.0s}
.hero .o2{background:var(--b1);top:0;right:-2vmin;animation-delay:.4s}
.hero .o3{background:var(--b2);bottom:-2vmin;left:-1vmin;animation-delay:.8s}
.hero .o4{background:var(--b3);bottom:0;right:1vmin;animation-delay:1.2s}
.hero h1{font-size:7.5vmin;line-height:1.05;font-weight:800;animation:fin .6s .35s both}
.hero .hi{color:var(--accent)}
.hero .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:fin .6s .55s both,pulse 1.8s 1.8s ease-in-out 6}
""", lambda c,p: f'''<div class="wrap">{LOGO}
<div class="core"><div class="ten">10</div>
<span class="bub orb o1">6</span><span class="bub orb o2">4</span><span class="bub orb o3">7</span><span class="bub orb o4">3</span></div>
<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1><span class="cta">{c['cta']}</span></div>''')

# C — GRID : fond plein de bulles numérotées + carte centrale
def grid_cells(p):
    nums = [6,4,7,3,8,2,5,5,1,9,6,4,3,7,2,8,4,6,9,1,5,5,8,2,7,3,6,4]
    out=[]
    for i,n in enumerate(nums):
        col = p['b'][i % len(p['b'])]
        out.append(f'<span class="bub gc" style="background:{col};animation-delay:{(i%9)*0.06:.2f}s">{n}</span>')
    return ''.join(out)
ARCH['grid'] = ("""
.grid{align-items:center;justify-content:center}
.grid .bg{position:absolute;inset:0;display:grid;grid-template-columns:repeat(4,1fr);
  align-content:center;justify-items:center;gap:3vmin;padding:5vmin;opacity:.85}
.grid .gc{width:14vmin;height:14vmin;font-size:6vmin;transform:scale(0);animation:pop .5s both}
.grid .card{position:relative;z-index:2;background:rgba(255,255,255,.92);color:#2E2A3A;border-radius:6vmin;
  padding:6vmin 6vmin 6.5vmin;display:flex;flex-direction:column;align-items:center;text-align:center;gap:2.4vmin;
  box-shadow:0 18px 40px rgba(0,0,0,.22);animation:fin .6s .35s both;max-width:80%}
.grid .card .logo{font-size:5.6vmin}
.grid .card h1{font-size:6.6vmin;line-height:1.05;font-weight:800}
.grid .card .hi{color:var(--accent)}
.grid .card .sub{font-size:3.6vmin;opacity:.7}
.grid .card .cta{font-size:4.2vmin;padding:2.8vmin 6vmin;margin-top:1vmin;animation:pulse 1.8s 1.6s ease-in-out 6}
""", lambda c,p: f'''<div class="bg">{grid_cells(p)}</div>
<div class="card">{LOGO}<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1>
<p class="sub">{c['sub']}</p><span class="cta">{c['cta']}</span></div>''')

# D — PHONE : maquette téléphone + mini grille, texte à côté
def phone_grid(p):
    nums=[6,4,7,3,2,8,5,5,9,1,4,6]
    return ''.join(f'<span class="bub pg" style="background:{p["b"][i%5]};animation-delay:{i*0.05:.2f}s">{n}</span>'
                   for i,n in enumerate(nums))
ARCH['phone'] = ("""
.phone{align-items:center;justify-content:center;gap:5vmin;padding:6vmin;flex-direction:column}
.phone .device{position:relative;width:42vmin;height:74vmin;background:#fff;border-radius:7vmin;
  border:1.4vmin solid #2E2A3A;box-shadow:0 18px 40px rgba(0,0,0,.25);overflow:hidden;flex:none;animation:fin .6s both}
.phone .device::before{content:"";position:absolute;top:1.6vmin;left:50%;transform:translateX(-50%);
  width:14vmin;height:2.4vmin;background:#2E2A3A;border-radius:2vmin}
.phone .screen{position:absolute;inset:0;background:linear-gradient(150deg,var(--bg1),var(--bg2));
  display:grid;grid-template-columns:repeat(3,1fr);align-content:center;justify-items:center;gap:2.6vmin;padding:6vmin 3vmin}
.phone .pg{width:9.5vmin;height:9.5vmin;font-size:4.4vmin;transform:scale(0);animation:pop .5s both}
.phone .txt{display:flex;flex-direction:column;align-items:center;text-align:center;gap:2.6vmin;animation:fin .6s .3s both}
.phone .txt .logo{font-size:6vmin}
.phone .txt h1{font-size:7vmin;line-height:1.05;font-weight:800}
.phone .txt .hi{color:var(--accent)}
.phone .txt .sub{font-size:3.6vmin;opacity:.72}
.phone .txt .cta{font-size:4.2vmin;padding:2.8vmin 6vmin;animation:pulse 1.8s 1.6s ease-in-out 6}
@media(orientation:landscape){.phone{flex-direction:row;gap:7vmin}.phone .device{height:80vmin;width:45vmin}}
""", lambda c,p: f'''<div class="device"><div class="screen">{phone_grid(p)}</div></div>
<div class="txt">{LOGO}<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1>
<p class="sub">{c['sub']}</p><span class="cta">{c['cta']}</span></div>''')

# E — SPLIT : bloc accent (logo+titre) / bloc clair (démo+cta)
ARCH['split'] = ("""
.split{flex-direction:column}
.split .top{flex:1;background:var(--accent);color:#fff;display:flex;flex-direction:column;
  align-items:center;justify-content:center;text-align:center;gap:3vmin;padding:7vmin;
  clip-path:polygon(0 0,100% 0,100% 86%,0 100%);animation:fin .6s both}
.split .top .logo{font-size:6vmin}.split .top .logo .d{color:#fff;opacity:.7}
.split .top h1{font-size:8.2vmin;line-height:1.05;font-weight:800}
.split .bot{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3.4vmin;padding:6vmin;animation:fin .6s .2s both}
.split .scene{display:flex;align-items:center;gap:2.3vmin}
.split .bub{width:13vmin;height:13vmin;font-size:6vmin}
.split .b6{background:var(--b0)}.split .b4{background:var(--b1)}
.split .lnk{width:4.5vmin;height:1.3vmin;border-radius:4px;background:var(--accent)}
.split .eq{background:var(--accent);color:var(--oa,#fff);font-weight:800;font-size:4.2vmin;padding:1vmin 2.4vmin;border-radius:999px}
.split .sub{font-size:3.7vmin;opacity:.72}
.split .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:pulse 1.8s 1.6s ease-in-out 6}
@media(orientation:landscape){.split{flex-direction:row}
.split .top{clip-path:polygon(0 0,100% 0,86% 100%,0 100%)}}
""", lambda c,p: f'''<div class="top">{LOGO}<h1>{c['h1']}<br>{c['h2']}</h1></div>
<div class="bot"><div class="scene">{chain()}</div><p class="sub">{c['sub']}</p><span class="cta">{c['cta']}</span></div>''')

# F — PIANO : bulles-notes + clavier en bas (axe musique)
ARCH['piano'] = ("""
.piano{flex-direction:column;align-items:center;justify-content:flex-start;padding:7vmin 6vmin 0;gap:3vmin}
.piano .logo{font-size:6vmin;animation:fin .6s both}
.piano h1{font-size:7.4vmin;line-height:1.05;font-weight:800;text-align:center;animation:fin .6s .15s both}
.piano .hi{color:var(--accent)}
.piano .notes{display:flex;align-items:flex-end;gap:2.4vmin;height:24vmin;animation:fin .6s .3s both}
.piano .nt{width:12vmin;height:12vmin;font-size:5.6vmin;animation:drift 2.4s ease-in-out infinite}
.piano .nt:nth-child(1){background:var(--b0);animation-delay:0s}
.piano .nt:nth-child(2){background:var(--b1);animation-delay:.3s}
.piano .nt:nth-child(3){background:var(--b2);animation-delay:.6s}
.piano .nt:nth-child(4){background:var(--b3);animation-delay:.9s}
.piano .sub{font-size:3.7vmin;opacity:.72;text-align:center;animation:fin .6s .4s both}
.piano .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:fin .6s .55s both,pulse 1.8s 1.8s ease-in-out 6}
.piano .keys{position:absolute;bottom:0;left:0;right:0;height:16vmin;display:flex;gap:.6vmin;padding:0 1vmin}
.piano .wk{flex:1;background:#fff;border-radius:0 0 1.6vmin 1.6vmin;box-shadow:inset 0 0 0 1px rgba(0,0,0,.08)}
.piano .keys{opacity:.9}
""", lambda c,p: f'''{LOGO}<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1>
<div class="notes"><span class="bub nt">6</span><span class="bub nt">4</span><span class="bub nt">7</span><span class="bub nt">3</span></div>
<p class="sub">{c['sub']}</p><span class="cta">{c['cta']}</span>
<div class="keys">{''.join('<span class="wk"></span>' for _ in range(8))}</div>''')

# G — SCORE : gros score + chips +10 qui pop + badge record
ARCH['score'] = ("""
.score{align-items:center;justify-content:center}
.score .wrap{display:flex;flex-direction:column;align-items:center;text-align:center;gap:3vmin;padding:7vmin}
.score .logo{font-size:6vmin;animation:fin .6s both}
.score .num{font-size:20vmin;font-weight:800;line-height:1;color:var(--accent);
  position:relative;animation:fin .6s .1s both}
.score .num .sh{position:absolute;inset:0;overflow:hidden}
.score .chips{display:flex;gap:2vmin}
.score .chip{background:var(--b1);color:#fff;font-weight:800;font-size:4.4vmin;
  padding:1.4vmin 3vmin;border-radius:999px;transform:scale(0)}
.score .chip:nth-child(1){animation:pop .4s .5s both}
.score .chip:nth-child(2){animation:pop .4s .8s both}
.score .chip:nth-child(3){animation:pop .4s 1.1s both}
.score .rec{font-size:3.8vmin;font-weight:700;opacity:.75;animation:fin .6s 1.3s both}
.score h1{font-size:7vmin;line-height:1.05;font-weight:800;animation:fin .6s .3s both}
.score .hi{color:var(--accent)}
.score .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:fin .6s 1.5s both,pulse 1.8s 2.4s ease-in-out 5}
.score .d1{width:50vmin;height:50vmin;background:var(--b2);top:-16vmin;right:-14vmin}
""", lambda c,p: f'''<i class="deco d1"></i><div class="wrap">{LOGO}
<div class="num">9 540</div>
<div class="chips"><span class="chip">+10</span><span class="chip">+30</span><span class="chip">+50</span></div>
<div class="rec">★ Nouveau record</div>
<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1><span class="cta">{c['cta']}</span></div>''')

# H — DAILY : carte calendrier + série, axe défi quotidien
ARCH['daily'] = ("""
.daily{align-items:center;justify-content:center}
.daily .wrap{display:flex;flex-direction:column;align-items:center;text-align:center;gap:3.4vmin;padding:7vmin}
.daily .logo{font-size:6vmin;animation:fin .6s both}
.daily .cal{width:34vmin;height:36vmin;background:#fff;color:#2E2A3A;border-radius:4vmin;
  box-shadow:0 14px 32px rgba(0,0,0,.2);overflow:hidden;animation:fin .6s .15s both;display:flex;flex-direction:column}
.daily .cal .hd{background:var(--accent);color:#fff;font-size:3.6vmin;font-weight:800;padding:2vmin;letter-spacing:1px}
.daily .cal .dy{flex:1;display:flex;align-items:center;justify-content:center;font-size:16vmin;font-weight:800}
.daily .streak{display:flex;align-items:center;gap:1.6vmin;font-size:4.6vmin;font-weight:800;animation:fin .6s .3s both}
.daily .flame{font-size:5.6vmin}
.daily h1{font-size:7vmin;line-height:1.05;font-weight:800;animation:fin .6s .4s both}
.daily .hi{color:var(--accent)}
.daily .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:fin .6s .55s both,pulse 1.8s 1.8s ease-in-out 6}
""", lambda c,p: f'''<div class="wrap">{LOGO}
<div class="cal"><div class="hd">DÉFI DU JOUR</div><div class="dy">10</div></div>
<div class="streak"><span class="flame">🔥</span><span>Série : 7 jours</span></div>
<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1><span class="cta">{c['cta']}</span></div>''')

# I — TYPOGRAPHIE : gros titre, mini démo, cta (très épuré)
ARCH['type'] = ("""
.type{flex-direction:column;align-items:flex-start;justify-content:center;padding:9vmin;gap:4vmin}
.type .logo{font-size:5.4vmin;animation:fin .6s both}
.type h1{font-size:13vmin;line-height:.98;font-weight:800;letter-spacing:-.5px;animation:fin .6s .15s both}
.type .hi{color:var(--accent)}
.type .mini{display:flex;align-items:center;gap:1.8vmin;animation:fin .6s .3s both}
.type .bub{width:10vmin;height:10vmin;font-size:4.8vmin}
.type .b6{background:var(--b0)}.type .b4{background:var(--b1)}
.type .lnk{width:4vmin;height:1.2vmin;border-radius:4px;background:var(--accent)}
.type .eq{font-size:4.4vmin;font-weight:800;color:var(--accent)}
.type .cta{font-size:4.6vmin;padding:3.2vmin 7vmin;animation:fin .6s .45s both,pulse 1.8s 1.6s ease-in-out 6}
""", lambda c,p: f'''{LOGO}<h1>{c['h1']}<br><span class="hi">{c['h2']}</span></h1>
<div class="mini"><span class="bub b6">6</span><span class="lnk"></span><span class="bub b4">4</span><span class="eq">= 10</span></div>
<span class="cta">{c['cta']}</span>''')

# J — CELEBRATION : confettis-bulles + "PARFAIT +100"
def confetti(p):
    pos=[(12,18),(82,14),(20,70),(78,74),(50,10),(8,46),(90,48),(40,82),(62,30),(30,38)]
    out=[]
    for i,(x,y) in enumerate(pos):
        out.append(f'<span class="bub cf" style="left:{x}%;top:{y}%;background:{p["b"][i%5]};'
                   f'animation-delay:{i*0.08:.2f}s">{(i%9)+1}</span>')
    return ''.join(out)
ARCH['celebrate'] = ("""
.celebrate{align-items:center;justify-content:center}
.celebrate .cf{position:absolute;width:10vmin;height:10vmin;font-size:4.6vmin;transform:scale(0);
  animation:pop .5s both,drift 3s ease-in-out infinite}
.celebrate .wrap{position:relative;z-index:2;display:flex;flex-direction:column;align-items:center;text-align:center;gap:3vmin;padding:7vmin}
.celebrate .logo{font-size:6vmin;animation:fin .6s both}
.celebrate .big{font-size:11vmin;font-weight:800;color:var(--accent);line-height:1;animation:pop .5s .3s both}
.celebrate .plus{font-size:7vmin;font-weight:800;animation:fin .6s .5s both}
.celebrate h1{font-size:6.6vmin;line-height:1.05;font-weight:800;animation:fin .6s .6s both}
.celebrate .hi{color:var(--accent)}
.celebrate .cta{font-size:4.4vmin;padding:3vmin 6.5vmin;animation:fin .6s .8s both,pulse 1.8s 2s ease-in-out 5}
""", lambda c,p: f'''{confetti(p)}<div class="wrap">{LOGO}
<div class="big">PARFAIT</div><div class="plus">+100 ✨</div>
<h1>{c['h1']} <span class="hi">{c['h2']}</span></h1><span class="cta">{c['cta']}</span></div>''')

# ───────────────────────── concepts (20 templates distincts) ─────────────────────────
CONCEPTS = [
 ('chain','peach','zen'),     ('chain','night','simple'),
 ('hero','sky','simple'),     ('hero','coral','brain'),
 ('grid','peach','daily'),    ('grid','mint','free'),
 ('phone','sky','zen'),       ('phone','sunset','brain'),
 ('split','coral','daily'),   ('split','mint','simple'),
 ('piano','night','music'),   ('piano','peach','music'),
 ('score','sunset','brain'),  ('score','sky','daily'),
 ('daily','mint','daily'),    ('daily','coral','free'),
 ('type','night','zen'),      ('type','peach','simple'),
 ('celebrate','sunset','free'),('celebrate','sky','brain'),
]

def palette_style(p):
    return (f"--bg1:{p['bg1']};--bg2:{p['bg2']};--ink:{p['ink']};--accent:{p['accent']};"
            f"--oa:{p.get('oa','#fff')};"
            f"--b0:{p['b'][0]};--b1:{p['b'][1]};--b2:{p['b'][2]};--b3:{p['b'][3]};--b4:{p['b'][4]}")

def render(arch, pal, copy, w, h):
    css, htmlfn = ARCH[arch]
    inner = htmlfn(copy, pal)
    page = SHELL
    for k, v in [('{{W}}',str(w)),('{{H}}',str(h)),('{{CT}}',CLICKTAG),
                 ('{{BASE}}',BASE),('{{ACSS}}',css),('{{NAME}}',arch),
                 ('{{STYLE}}',palette_style(pal)),('{{HTML}}',inner)]:
        page = page.replace(k, v)
    return page

# ───────────────────────── build ─────────────────────────
BASEDIR = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(BASEDIR, 'build-templates')
if os.path.exists(OUT):
    shutil.rmtree(OUT)
os.makedirs(OUT)

gal = ['<!doctype html><meta charset=utf-8><title>TEN·GO — 20 templates</title>',
       "<body style='font-family:sans-serif;background:#33343a;color:#eee;padding:20px'>",
       "<h1>TEN·GO — 20 templates HTML5 (aperçu portrait 320×480)</h1>",
       "<div style='display:flex;flex-wrap:wrap;gap:18px'>"]

count = 0
for i, (arch, palk, copyk) in enumerate(CONCEPTS, 1):
    pal, copy = PALETTES[palk], COPY[copyk]
    cid = f"t{i:02d}-{arch}-{palk}"
    for (w, h) in SIZES:
        folder = os.path.join(OUT, f"{cid}_{w}x{h}")
        os.makedirs(folder)
        idx = os.path.join(folder, "index.html")
        with open(idx, "w", encoding="utf-8") as f:
            f.write(render(arch, pal, copy, w, h))
        with zipfile.ZipFile(os.path.join(OUT, f"{cid}_{w}x{h}.zip"), "w", zipfile.ZIP_DEFLATED) as z:
            z.write(idx, "index.html")
        count += 1
    gal.append(
        f"<div><div style='font:12px sans-serif;margin-bottom:4px'>{cid}</div>"
        f"<iframe src='{cid}_320x480/index.html' width=320 height=480 scrolling=no "
        f"style='border:0;border-radius:8px'></iframe></div>")

gal += ["</div></body>"]
with open(os.path.join(OUT, "gallery.html"), "w", encoding="utf-8") as f:
    f.write("\n".join(gal))

print(f"OK — {len(CONCEPTS)} templates distincts, {count} fichiers ZIP ({len(SIZES)} orientations chacun)")
print(f"Sortie : {OUT}")
print("QA visuel : ouvrir build-templates/gallery.html")
