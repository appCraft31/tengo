#!/usr/bin/env python3
"""
_build-screen-6.py
Génère le screen-6 « Défi du jour » (iPhone + iPad) pour les 10 langues.

Stratégie : on réutilise screen-4 de chaque langue comme template (même
charte : fond beige, bulles pastel, téléphone, branding TEN·GO). On ne
remplace que (a) l'accroche headline/subline et (b) le screenshot intégré.

Usage : ./_build-screen-6.py <screenshot_defi.png>
"""
import base64
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent

# Accroche « Un nouveau défi / chaque jour. » — (headline, subline) par langue.
COPY = {
    "en":      ("A new challenge", "every day."),
    "fr":      ("Un nouveau défi", "chaque jour."),
    "de":      ("Eine neue Aufgabe", "jeden Tag."),
    "es":      ("Un nuevo desafío", "cada día."),
    "it":      ("Una nuova sfida", "ogni giorno."),
    "pt-BR":   ("Um novo desafio", "todos os dias."),
    "nl":      ("Een nieuwe uitdaging", "elke dag."),
    "ja":      ("新しい挑戦を", "毎日。"),
    "ko":      ("새로운 도전", "매일."),
    "zh-Hans": ("每天", "全新挑战。"),
}

IMG_RE = re.compile(r'(<img src=")data:image/[^"]*(")')
TEXT_RE = re.compile(
    r'<div class="headline">.*?</div><div class="subline">.*?</div>',
    re.DOTALL,
)
# Contraint la largeur du titre en vw : force un retour à la ligne propre sur
# iPhone (viewport 428) sans wrapper inutilement sur iPad (viewport 1024).
# Le template screen-4 (titre court « Défiez ») n'en a pas besoin, mais notre
# accroche est plus longue et tombe pile à la limite.
HEADLINE_RE = re.compile(r'(\.headline\{[^}]*?)\}')


def build(png_b64: str) -> None:
    data_uri = "data:image/png;base64," + png_b64
    for lang, (headline, subline) in COPY.items():
        new_text = (
            f'<div class="headline">{headline}</div>'
            f'<div class="subline">{subline}</div>'
        )
        for suffix in ("", "_ipad"):
            template = HERE / lang / f"screen-4{suffix}.html"
            html = template.read_text(encoding="utf-8")
            html = TEXT_RE.sub(new_text, html, count=1)
            html = HEADLINE_RE.sub(r'\1;max-width:80vw;margin:0 auto}', html,
                                   count=1)
            html = IMG_RE.sub(lambda m: m.group(1) + data_uri + m.group(2),
                              html, count=1)
            out = HERE / lang / f"screen-6{suffix}.html"
            out.write_text(html, encoding="utf-8")
            print(f"✅ {out.relative_to(HERE)}")


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit("Usage: ./_build-screen-6.py <screenshot_defi.png>")
    png = Path(sys.argv[1])
    png_b64 = base64.b64encode(png.read_bytes()).decode("ascii")
    build(png_b64)


if __name__ == "__main__":
    main()
