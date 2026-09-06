#!/usr/bin/env python3
# Génère les screenshots App Store / Play Store de tenGO :
# 8 panneaux × 10 langues × 3 formats, rendus par Chrome headless.
import os, subprocess, sys, shutil

S = os.path.dirname(os.path.abspath(__file__))
R = os.path.join(S, "render")
OUT = os.path.join(R, "out")
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

# ---------------------------------------------------------------- formats
# (nom, largeur CSS, hauteur CSS, facteur d'échelle, classe layout)
FORMATS = {
    "phone": (621, 1344, 2, ""),      # ASC iPhone 6,5" → 1242×2688
    "ipad":  (1024, 1366, 2, "pad"),  # ASC iPad 12,9"  → 2048×2732
    "play":  (540, 1080, 2, "play"),  # Play ≤ 2:1      → 1080×2160
}

# ---------------------------------------------------------------- textes
# t: titre (lignes) ; s: sous-titre
T = {
 "fr": dict(
   p1=dict(t=['Relie.', 'Additionne.', 'Respire.'], s='Le puzzle de calcul mental le plus zen qui soit'),
   p2=dict(t=['Fais 10.', 'C’est tout.'], s='Relie des bulles voisines dont la somme fait exactement dix'),
   p3=dict(t=['Plus long,', 'beaucoup mieux.'], s='Une chaîne de six vaut bien plus que trois paires — de loin'),
   p4=dict(t=['Chaque combo', 'joue sa mélodie.'], s='Chaque bulle est une note — tes doigts deviennent la partition'),
   p5=dict(t=['Un défi.', 'Chaque jour.', 'Le même pour tous.'], s='Grille unique, twists et classement mondial'),
   p6=dict(t=['Ta grille,', 'ton style.'], s='Thèmes, matières et tracés à débloquer — jamais obligatoires'),
   p7=dict(t=['Défie un ami.', 'Même grille.'], s='Il joue quand il veut — un code suffit à lancer le duel'),
   p8=dict(t=['Chaque partie', 'te fait progresser.'], s='100 niveaux, missions du jour, séries et succès à décrocher')),
 "en": dict(
   p1=dict(t=['Connect.', 'Add up.', 'Breathe.'], s='The most soothing mental math puzzle there is'),
   p2=dict(t=['Make 10.', 'That’s it.'], s='Link neighbouring bubbles that add up to exactly ten'),
   p3=dict(t=['Longer chains,', 'far better.'], s='A chain of six is worth much more than three pairs — by far'),
   p4=dict(t=['Every combo plays', 'its own melody.'], s='Each bubble is a note — your fingers become the score'),
   p5=dict(t=['One puzzle.', 'Every day.', 'The same for everyone.'], s='A unique grid, daily twists and a worldwide leaderboard'),
   p6=dict(t=['Your grid,', 'your style.'], s='Themes, bubble skins and trails to unlock — never required'),
   p7=dict(t=['Challenge a friend.', 'Same grid.'], s='They play whenever — one code starts the duel'),
   p8=dict(t=['Every game', 'moves you forward.'], s='100 levels, daily missions, streaks and achievements')),
 "de": dict(
   p1=dict(t=['Verbinde.', 'Addiere.', 'Atme.'], s='Das entspannendste Kopfrechen-Puzzle überhaupt'),
   p2=dict(t=['Mach 10.', 'Mehr nicht.'], s='Verbinde benachbarte Blasen, die zusammen genau zehn ergeben'),
   p3=dict(t=['Länger ist', 'viel besser.'], s='Eine Sechserkette bringt weit mehr als drei Paare'),
   p4=dict(t=['Jede Kombo spielt', 'ihre eigene Melodie.'], s='Jede Blase ist eine Note — deine Finger werden zur Partitur'),
   p5=dict(t=['Ein Rätsel.', 'Jeden Tag.', 'Für alle dasselbe.'], s='Einzigartiges Gitter, Twists und weltweite Rangliste'),
   p6=dict(t=['Dein Gitter,', 'dein Stil.'], s='Themen, Blasen-Looks und Spuren zum Freischalten — nie Pflicht'),
   p7=dict(t=['Fordere Freunde heraus.', 'Gleiches Feld.'], s='Sie spielen wann sie wollen — ein Code genügt'),
   p8=dict(t=['Jede Partie', 'bringt dich weiter.'], s='100 Stufen, Tagesmissionen, Serien und Erfolge')),
 "es": dict(
   p1=dict(t=['Conecta.', 'Suma.', 'Respira.'], s='El puzle de cálculo mental más relajante que existe'),
   p2=dict(t=['Haz 10.', 'Nada más.'], s='Une burbujas vecinas que sumen exactamente diez'),
   p3=dict(t=['Más larga,', 'mucho mejor.'], s='Una cadena de seis vale mucho más que tres parejas'),
   p4=dict(t=['Cada combo toca', 'su propia melodía.'], s='Cada burbuja es una nota: tus dedos son la partitura'),
   p5=dict(t=['Un reto.', 'Cada día.', 'El mismo para todos.'], s='Cuadrícula única, sorpresas y clasificación mundial'),
   p6=dict(t=['Tu cuadrícula,', 'tu estilo.'], s='Temas, burbujas y trazos por desbloquear, nunca obligatorios'),
   p7=dict(t=['Reta a un amigo.', 'Misma cuadrícula.'], s='Juega cuando quiera — basta un código para el duelo'),
   p8=dict(t=['Cada partida', 'te hace avanzar.'], s='100 niveles, misiones diarias, rachas y logros')),
 "it": dict(
   p1=dict(t=['Collega.', 'Somma.', 'Respira.'], s='Il puzzle di calcolo mentale più rilassante che ci sia'),
   p2=dict(t=['Fai 10.', 'Tutto qui.'], s='Collega bolle vicine che sommano esattamente dieci'),
   p3=dict(t=['Più lunga,', 'molto meglio.'], s='Una catena da sei vale molto più di tre coppie'),
   p4=dict(t=['Ogni combo suona', 'la sua melodia.'], s='Ogni bolla è una nota: le tue dita diventano lo spartito'),
   p5=dict(t=['Una sfida.', 'Ogni giorno.', 'Uguale per tutti.'], s='Griglia unica, varianti e classifica mondiale'),
   p6=dict(t=['La tua griglia,', 'il tuo stile.'], s='Temi, materiali e scie da sbloccare, mai obbligatori'),
   p7=dict(t=['Sfida un amico.', 'Stessa griglia.'], s='Gioca quando vuole — basta un codice per il duello'),
   p8=dict(t=['Ogni partita', 'ti fa progredire.'], s='100 livelli, missioni del giorno, serie e obiettivi')),
 "ja": dict(
   p1=dict(t=['つなぐ。', 'たす。', 'ととのう。'], s='いちばん心やすらぐ暗算パズル'),
   p2=dict(t=['10を作る。', 'それだけ。'], s='となり合うバブルをつないで、ぴったり10に'),
   p3=dict(t=['長いほど、', 'ずっと強い。'], s='6個のチェーンは、2個×3回よりはるかに高得点'),
   p4=dict(t=['コンボが', 'メロディを奏でる。'], s='バブルは音符。指先が楽譜になる'),
   p5=dict(t=['毎日ひとつの挑戦。', 'みんな同じ問題。'], s='日替わりグリッドと世界ランキング'),
   p6=dict(t=['自分らしい', 'グリッドに。'], s='テーマや質感、軌跡を解放 — 課金は不要'),
   p7=dict(t=['友だちに挑戦。', '同じ盤面で。'], s='相手はいつでもプレイ可能。コードひとつで対戦開始'),
   p8=dict(t=['一局ごとに、', '前へ進む。'], s='100レベル、デイリーミッション、連続記録と実績')),
 "ko": dict(
   p1=dict(t=['잇고.', '더하고.', '숨 고르고.'], s='가장 마음 편한 암산 퍼즐'),
   p2=dict(t=['10을 만드세요.', '그게 전부.'], s='이웃한 버블을 이어 정확히 10을 만들어요'),
   p3=dict(t=['길수록', '훨씬 좋다.'], s='여섯 개의 체인은 두 개짜리 세 번보다 훨씬 높은 점수'),
   p4=dict(t=['콤보마다', '멜로디가 흐릅니다.'], s='버블은 음표, 손끝이 악보가 됩니다'),
   p5=dict(t=['하루 하나의 도전.', '모두에게 같은 퍼즐.'], s='매일 새로운 그리드와 세계 랭킹'),
   p6=dict(t=['내 그리드,', '내 스타일.'], s='테마·버블·궤적을 해금 — 강요는 없어요'),
   p7=dict(t=['친구에게 도전.', '같은 보드로.'], s='상대는 언제든 플레이 — 코드 하나면 듀얼 시작'),
   p8=dict(t=['한 판마다', '앞으로 나아간다.'], s='100레벨, 일일 미션, 연속 기록과 업적')),
 "nl": dict(
   p1=dict(t=['Verbind.', 'Tel op.', 'Adem uit.'], s='De meest ontspannen hoofdrekenpuzzel die er is'),
   p2=dict(t=['Maak 10.', 'Meer niet.'], s='Verbind buurbellen die samen precies tien zijn'),
   p3=dict(t=['Langer is', 'veel beter.'], s='Een ketting van zes levert veel meer op dan drie paren'),
   p4=dict(t=['Elke combo speelt', 'zijn eigen melodie.'], s='Elke bel is een noot — je vingers worden de partituur'),
   p5=dict(t=['Eén uitdaging.', 'Elke dag.', 'Voor iedereen dezelfde.'], s='Uniek raster, twists en wereldwijde ranglijst'),
   p6=dict(t=['Jouw raster,', 'jouw stijl.'], s='Thema’s, bellen en sporen om vrij te spelen — nooit verplicht'),
   p7=dict(t=['Daag een vriend uit.', 'Zelfde raster.'], s='Hij speelt wanneer hij wil — één code start het duel'),
   p8=dict(t=['Elke partij', 'brengt je verder.'], s='100 niveaus, dagmissies, reeksen en prestaties')),
 "pt-BR": dict(
   p1=dict(t=['Ligue.', 'Some.', 'Respire.'], s='O quebra-cabeça de cálculo mental mais relaxante que existe'),
   p2=dict(t=['Faça 10.', 'Só isso.'], s='Ligue bolhas vizinhas que somem exatamente dez'),
   p3=dict(t=['Mais longa,', 'bem melhor.'], s='Uma corrente de seis vale bem mais que três pares'),
   p4=dict(t=['Cada combo toca', 'a própria melodia.'], s='Cada bolha é uma nota — seus dedos viram a partitura'),
   p5=dict(t=['Um desafio.', 'Todo dia.', 'O mesmo para todos.'], s='Grade única, surpresas e ranking mundial'),
   p6=dict(t=['Sua grade,', 'seu estilo.'], s='Temas, bolhas e traços para desbloquear, nunca obrigatórios'),
   p7=dict(t=['Desafie um amigo.', 'Mesma grade.'], s='Ele joga quando quiser — um código inicia o duelo'),
   p8=dict(t=['Cada partida', 'faz você avançar.'], s='100 níveis, missões diárias, sequências e conquistas')),
 "zh-Hans": dict(
   p1=dict(t=['连接。', '相加。', '深呼吸。'], s='最治愈的心算益智游戏'),
   p2=dict(t=['凑成 10，', '就这么简单。'], s='连接相邻气泡，让总和恰好为十'),
   p3=dict(t=['越长，', '越划算。'], s='一条六连的得分，远高于三次两连'),
   p4=dict(t=['每个连击', '都会奏出旋律。'], s='每个气泡都是音符，指尖化作乐谱'),
   p5=dict(t=['每日一题，', '全球同题。'], s='每天一张新棋盘，还有世界排行榜'),
   p6=dict(t=['你的棋盘,', '你的风格。'], s='解锁主题、材质与轨迹，绝不强制'),
   p7=dict(t=['挑战朋友。', '同一个棋盘。'], s='对方随时开局 — 一个代码就能发起对战'),
   p8=dict(t=['每一局', '都让你更进一步。'], s='100 个等级、每日任务、连续记录与成就')),
}

# panneau → (capture localisée par langue, fond, bulles déco, tilt)
PANELS = {
 1: dict(img="full_menu_{lang}.png",  bg="#F7F2EB", tilt="",   deco=[("30%","#D1F2E0","top:-8%;left:-10%"),("19%","#FFB8DB","top:9%;right:-7%;opacity:.4"),("24%","#FFF59E","bottom:26%;left:-11%;opacity:.45")]),
 2: dict(img="full_game_{lang}.png",  bg="#F7F2EB", tilt="tl", deco=[("27%","#9EDBFF","top:-6%;right:-10%;opacity:.35"),("21%","#FF9E9E","bottom:30%;left:-9%;opacity:.3")]),
 3: dict(img="full_game_{lang}.png",  bg="#F0F6EF", tilt="",   deco=[("32%","#ABF2BF","top:-9%;right:-12%;opacity:.4"),("19%","#D1F2E0","bottom:34%;left:-7%;opacity:.6")]),
 4: dict(img="full_game_{lang}.png",  bg="#F4F1FA", tilt="tr", deco=[("29%","#CCB8FF","top:-8%;left:-10%;opacity:.4"),("22%","#FFB8DB","bottom:32%;right:-9%;opacity:.35")]),
 5: dict(img="full_daily_{lang}.png", bg="#F1F2FB", tilt="",   deco=[("30%","#B8C2FF","top:-9%;right:-10%;opacity:.45"),("19%","#9EDBFF","bottom:31%;left:-7%;opacity:.4")]),
 6: dict(img="full_shop_{lang}.png",  bg="#F7F2EB", tilt="tl", deco=[("26%","#FFB8DB","top:-7%;left:-9%;opacity:.4"),("21%","#FFC79E","bottom:33%;right:-7%;opacity:.4")]),
 7: dict(img="full_duel_{lang}.png",    bg="#FBF0F0", tilt="tr", deco=[("28%","#FF9E9E","top:-8%;right:-10%;opacity:.35"),("20%","#FFC79E","bottom:32%;left:-8%;opacity:.4")]),
 8: dict(img="full_profile_{lang}.png", bg="#F0F4FA", tilt="",   deco=[("31%","#9EDBFF","top:-9%;left:-11%;opacity:.4"),("19%","#ABF2BF","bottom:30%;right:-7%;opacity:.5")]),
}

CSS = """
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:100%;height:100%;overflow:hidden}
body{background:var(--bg);color:#3D3D3D;
 font-family:"Avenir Next","Hiragino Sans","Apple SD Gothic Neo","PingFang SC",ui-rounded,system-ui,sans-serif;
 position:relative}
.deco{position:absolute;border-radius:50%;opacity:.55}
.hook{position:relative;z-index:3;padding:6.2% 6% 0;text-align:center;font-weight:800;
 font-size:5.9vw;line-height:1.16;letter-spacing:.01em}
.pad .hook{font-size:4.4vw;padding-top:5%}
.play .hook{font-size:6.1vw}
.sub{position:relative;z-index:3;text-align:center;margin-top:1.6%;font-size:2.55vw;font-weight:600;
 color:#6E6861;padding:0 9%;line-height:1.42}
.pad .sub{font-size:1.9vw}
.play .sub{font-size:2.7vw}
.phone{position:absolute;z-index:2;left:50%;bottom:-4%;width:76%;transform:translateX(-50%);
 border-radius:5.6vw;overflow:hidden;border:1vw solid #35322E;background:#35322E;
 box-shadow:0 3vw 6vw rgba(50,42,30,.35)}
.phone img{display:block;width:100%;height:auto}
.phone.tl{transform:translateX(-50%) rotate(-4deg);bottom:-6%}
.phone.tr{transform:translateX(-50%) rotate(4deg);bottom:-6%}
.pad .phone{width:44%;bottom:-6%;border-radius:3.4vw;border-width:.62vw}
.pad .phone.tl{transform:translateX(-50%) rotate(-3deg)}
.pad .phone.tr{transform:translateX(-50%) rotate(3deg)}
.play .phone{width:66%;bottom:-8%}
.chain{position:absolute;z-index:4;left:50%;transform:translateX(-50%);display:flex;align-items:center}
.link{height:.9vw;width:3.4vw;background:rgba(255,255,255,.85);border-radius:.6vw;margin:0 -0.5vw}
.bub{display:flex;align-items:center;justify-content:center;border-radius:50%;font-weight:800;color:#404040;
 box-shadow:inset 0 -0.4vw 0 rgba(0,0,0,.06),0 .9vw 2vw rgba(61,53,42,.18);z-index:1}
.eq{font-weight:800;font-size:4.6vw;margin:0 1vw}
.badge10{position:absolute;z-index:4;border-radius:50%;background:#FF9E9E;color:#fff;display:flex;
 align-items:center;justify-content:center;font-weight:800;
 box-shadow:0 1.4vw 3vw rgba(255,120,120,.45)}
.zen{position:relative;z-index:3;display:flex;flex-direction:column;gap:1.9%;padding-top:4%;
 font-weight:700;font-size:3.1vw;width:max-content;margin:0 auto}
.pad .zen{font-size:2.6vw;padding-top:3%}
.play .zen{font-size:3.2vw}
.zen div{display:flex;align-items:center;gap:2.4vw}
.zen span{width:5.4vw;height:5.4vw;border-radius:50%;display:flex;align-items:center;justify-content:center;
 font-size:2.8vw;color:#404040;flex:none}
.pad .zen span{width:3.8vw;height:3.8vw;font-size:2vw}
.melody{position:absolute;z-index:4;opacity:.85;font-size:4.2vw}
"""

def chain_html(fmt):
    b = "9.2vw" if fmt != "ipad" else "6.4vw"
    B = "10.6vw" if fmt != "ipad" else "7.4vw"
    top = "23%" if fmt == "phone" else ("21%" if fmt == "play" else "20%")
    return f'''<div class="chain" style="top:{top}">
      <span class="bub" style="width:{b};height:{b};font-size:4.4vw;background:#FFC79E">2</span><span class="link"></span>
      <span class="bub" style="width:{b};height:{b};font-size:4.4vw;background:#FFF59E">3</span><span class="link"></span>
      <span class="bub" style="width:{B};height:{B};font-size:5vw;background:#9EDBFF">5</span>
      <span class="eq">=</span>
      <span class="bub" style="width:{B};height:{B};font-size:5vw;background:#FF9E9E;color:#fff">10</span>
    </div>'''

def theme_dots(fmt):
    d = "6.4vw" if fmt != "ipad" else "4.4vw"
    top = "24.5%" if fmt == "phone" else ("22.5%" if fmt == "play" else "21%")
    cols = ["#ABF2BF", "#9EDBFF", "#FFC79E", "#2B2D45", "#FFB8DB"]
    dots = "".join(f'<span class="bub" style="width:{d};height:{d};background:{c};margin:0 .7vw"></span>' for c in cols)
    return f'<div class="chain" style="top:{top}">{dots}</div>'

def melody(fmt):
    notes = [("♪","25%","10%",""), ("♫","22.5%","20%","font-size:5vw"), ("♩","25.5%",None,""), ("♪","23%",None,"font-size:4.8vw")]
    out = []
    for ch, top, left, extra in notes[:2]:
        out.append(f'<span class="melody" style="top:{top};left:{left};{extra}">{ch}</span>')
    out.append(f'<span class="melody" style="top:25.5%;right:18%">♩</span>')
    out.append(f'<span class="melody" style="top:23%;right:9%;font-size:4.8vw">♪</span>')
    return "".join(out)

def panel_html(p, lang, fmt):
    cfg = PANELS[p]
    tx = T[lang][f"p{p}"]
    w, h, dpr, cls = FORMATS[fmt]
    title = "<br>".join(tx["t"])
    deco = "".join(f'<i class="deco" style="width:{s};aspect-ratio:1;background:{c};{pos}"></i>'
                   for s, c, pos in cfg["deco"])
    body = f'<div class="hook">{title}</div>'
    body += f'<div class="sub">{tx["s"]}</div>'
    if p == 2:
        body += chain_html(fmt)
    if p == 4:
        body += melody(fmt)
    if p == 6:
        body += theme_dots(fmt)
    tilt = {"tl": " tl", "tr": " tr", "": ""}[cfg["tilt"]]
    img = cfg["img"].format(lang=lang)
    body += f'<div class="phone{tilt}"><img src="{R}/{img}"></div>'
    return (f'<!doctype html><html><head><meta charset="utf-8"><style>:root{{--bg:{cfg["bg"]}}}'
            f'{CSS}</style></head><body class="{cls}">{deco}{body}</body></html>')

def render(p, lang, fmt, out_png):
    w, h, dpr, _ = FORMATS[fmt]
    html_path = os.path.join(R, f"tmp_{p}_{lang}_{fmt}.html")
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(panel_html(p, lang, fmt))
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    f"--window-size={w},{h}", f"--force-device-scale-factor={dpr}",
                    f"--screenshot={out_png}", f"file://{html_path}"],
                   check=True, capture_output=True)
    os.remove(html_path)

if __name__ == "__main__":
    only = sys.argv[1:] or None  # ex: "1 fr phone" pour un rendu unique
    os.makedirs(OUT, exist_ok=True)
    if only:
        p, lang, fmt = int(only[0]), only[1], only[2]
        out = os.path.join(OUT, f"p{p}_{lang}_{fmt}.png")
        render(p, lang, fmt, out)
        print(out)
    else:
        n = 0
        for lang in T:
            for p in PANELS:
                for fmt in FORMATS:
                    out = os.path.join(OUT, f"p{p}_{lang}_{fmt}.png")
                    render(p, lang, fmt, out)
                    n += 1
                    print(f"\r{n}/180", end="", flush=True)
        print("\nterminé")
