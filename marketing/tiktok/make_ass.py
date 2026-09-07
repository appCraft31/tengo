#!/usr/bin/env python3
"""
Génère le fichier ASS des accroches d'une publicité.

Le texte est le seul porteur du message (aucune voix off) : il doit rester dans
la zone sûre de TikTok — 130 px en haut, 484 px en bas, 140 px à droite — sinon
l'interface (pseudo, légende, bouton) le recouvre.

Usage : make_ass.py <sortie.ass> "<début>|<fin>|<texte>[|position]" ...
        position ∈ top (défaut) | mid | low ; \\N pour un retour à la ligne.
"""
import sys

# (alignement ASS, marge verticale) déduits des zones sûres, en 1080×1920.
POSITIONS = {
    "top": (8, 340),   # sous les onglets et sous le logo du jeu
    "mid": (5, 0),     # centré
    "low": (2, 620),   # au-dessus de la légende et du bouton d'installation
}

INFO = """[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
"""

# Texte blanc sur un bandeau sombre translucide (BorderStyle=3) : le plateau est
# clair et saturé, un simple contour s'y perdait dès que la grille bougeait.
# `Outline` sert ici de marge intérieure au bandeau. Marge droite de 250 px pour
# la colonne d'icônes de TikTok.
STYLE = ("Style: Hook{name},Nunito,72,&H00FFFFFF,&H00000000,&HB0261F1C,-1,0,0,0,"
         "100,100,0,0,3,16,0,{align},110,250,{margin},1")

EVENTS = """
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""


def timestamp(seconds: float) -> str:
    h, rest = divmod(max(0.0, seconds), 3600)
    m, s = divmod(rest, 60)
    return f"{int(h)}:{int(m):02d}:{s:05.2f}"


def main() -> None:
    if len(sys.argv) < 3:
        sys.exit("usage: make_ass.py <sortie.ass> \"<début>|<fin>|<texte>[|position]\" ...")
    out, cues = sys.argv[1], sys.argv[2:]

    used, events = [], []
    for cue in cues:
        parts = cue.split("|")
        if len(parts) < 3:
            sys.exit(f"accroche mal formée : {cue}")
        start, end, text = float(parts[0]), float(parts[1]), parts[2]
        pos = parts[3] if len(parts) > 3 and parts[3] else "top"
        if pos not in POSITIONS:
            sys.exit(f"position inconnue : {pos}")
        if pos not in used:
            used.append(pos)
        # Fondu court : une accroche qui claque sèchement se lit moins bien.
        events.append(f"Dialogue: 0,{timestamp(start)},{timestamp(end)},Hook{pos},,0,0,0,,"
                      f"{{\\fad(160,160)}}{text}")

    styles = "\n".join(
        STYLE.format(name=p, align=POSITIONS[p][0], margin=POSITIONS[p][1]) for p in used)
    with open(out, "w", encoding="utf-8") as f:
        f.write(INFO + styles + "\n" + EVENTS + "\n".join(events) + "\n")
    print(f"{len(events)} accroches → {out}")


if __name__ == "__main__":
    main()
