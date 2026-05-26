#!/usr/bin/env python3
"""
Génère les répertoires marketing manquants (it, pt-BR, nl, ko, zh-Hans)
en traduisant les accroches depuis la version anglaise.
"""

import os
import shutil

BASE = os.path.dirname(os.path.abspath(__file__))
EN_DIR = os.path.join(BASE, "marketing-en")

# Slogans par langue et par écran
# Clé : code langue → (lang_attr, tagline_1, headline_2, headline_3, headline_4, headline_5, headline_6, pts_label)
LOCALES = {
    "it": (
        "it",
        "Collega. Somma. Libera.",
        'Trova<span class="dot"></span>dieci.',
        'Respira<span class="dot"></span>',
        'Nessun<span class="dot"></span>timer.',
        'Collega.<span class="dot"></span>Libera.',
        'Una<span class="dot"></span>melodia<span class="dot"></span>per<span class="dot"></span>partita.',
        "pts",
    ),
    "pt-BR": (
        "pt-BR",
        "Conecte. Some. Libere.",
        'Faça<span class="dot"></span>dez.',
        'Respire<span class="dot"></span>',
        'Sem<span class="dot"></span>timer.',
        'Conecte.<span class="dot"></span>Libere.',
        'Uma<span class="dot"></span>melodia<span class="dot"></span>por<span class="dot"></span>jogo.',
        "pts",
    ),
    "nl": (
        "nl",
        "Verbind. Tel op. Bevrijd.",
        'Maak<span class="dot"></span>tien.',
        'Adem<span class="dot"></span>',
        'Geen<span class="dot"></span>timer.',
        'Verbind.<span class="dot"></span>Bevrijd.',
        'Een<span class="dot"></span>melodie<span class="dot"></span>per<span class="dot"></span>spel.',
        "pts",
    ),
    "ko": (
        "ko",
        "연결. 더하기. 해방.",
        '10을<span class="dot"></span>만들어요.',
        '숨쉬어<span class="dot"></span>',
        '타이머<span class="dot"></span>없음.',
        '연결.<span class="dot"></span>해방.',
        '매번<span class="dot"></span>새로운<span class="dot"></span>멜로디.',
        "점",
    ),
    "zh-Hans": (
        "zh-Hans",
        "连接。相加。释放。",
        '凑<span class="dot"></span>十。',
        '呼吸<span class="dot"></span>',
        '无<span class="dot"></span>计时器。',
        '连接。<span class="dot"></span>释放。',
        '每局<span class="dot"></span>独特<span class="dot"></span>旋律。',
        "分",
    ),
}

# Accroches anglaises à remplacer (dans l'ordre marketing-1 à 6)
EN_TAGLINE = "Connect. Add. Release."
EN_HEADLINES = [
    'Find<span class="dot"></span>ten.',
    'Breathe<span class="dot"></span>',
    'No<span class="dot"></span>timer.',
    'Connect.<span class="dot"></span>Release.',
    'A<span class="dot"></span>melody<span class="dot"></span>per<span class="dot"></span>game.',
]
EN_PTS = '<div class="label">pts</div>'


def translate_file(content: str, screen: int, locale_data: tuple) -> str:
    lang, tagline, h2, h3, h4, h5, h6, pts = locale_data
    headlines = [h2, h3, h4, h5, h6]

    # lang attribute
    content = content.replace('<html lang="en">', f'<html lang="{lang}">')

    # Screen 1 : tagline
    if screen == 1:
        content = content.replace(EN_TAGLINE, tagline)
    else:
        en_h = EN_HEADLINES[screen - 2]
        loc_h = headlines[screen - 2]
        content = content.replace(en_h, loc_h)

    # Label pts
    content = content.replace(EN_PTS, f'<div class="label">{pts}</div>')

    return content


for code, locale_data in LOCALES.items():
    out_dir = os.path.join(BASE, f"marketing-{code}")
    os.makedirs(out_dir, exist_ok=True)

    for n in range(1, 7):
        src = os.path.join(EN_DIR, f"marketing-{n}.html")
        dst = os.path.join(out_dir, f"marketing-{n}.html")

        with open(src, "r", encoding="utf-8") as f:
            content = f.read()

        content = translate_file(content, n, locale_data)

        with open(dst, "w", encoding="utf-8") as f:
            f.write(content)

        print(f"✓ marketing-{code}/marketing-{n}.html")

print("\n✅ Répertoires générés :", ", ".join(f"marketing-{c}" for c in LOCALES))
