#!/usr/bin/env python3
"""Migration appstore_fiche/ → fastlane/metadata/"""

import os
import re

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC  = os.path.join(BASE, "marketing", "appstore_fiche")
DST  = os.path.join(BASE, "fastlane", "metadata")

LOCALES = {
    "en.txt":      "en-US",
    "fr.txt":      "fr-FR",
    "de.txt":      "de-DE",
    "es.txt":      "es-ES",
    "it.txt":      "it",
    "ja.txt":      "ja",
    "ko.txt":      "ko",
    "nl.txt":      "nl-NL",
    "pt-BR.txt":   "pt-BR",
    "zh-Hans.txt": "zh-Hans",
}

# Marqueurs de début de chaque section (multilingue)
SECTION_MARKERS = {
    "name":             re.compile(r"^(?:NAME|NOM|NOME|NOMBRE|NAAM|名前|이름|名称)[^\n]*$", re.MULTILINE),
    "subtitle":         re.compile(r"^(?:SUBTITLE|SOUS-TITRE|UNTERTITEL|SUBTÍTULO|ONDERTITEL|サブタイトル|부제|副标题|SOTTOTITOLO)[^\n]*$", re.MULTILINE),
    "promotional_text": re.compile(r"^(?:PROMOTIONAL TEXT|TEXTE PROMOTIONNEL|WERBETEXT|TEXTO PROMOCIONAL|PROMOTIETEKST|プロモーションテキスト|홍보 텍스트|宣传文字|TESTO PROMOZIONALE)[^\n]*$", re.MULTILINE),
    "description":      re.compile(r"^(?:DESCRIPTION|BESCHREIBUNG|DESCRIPCIÓN|DESCRIZIONE|BESCHRIJVING|DESCRIÇÃO|説明|설명|描述)[^\n]*$", re.MULTILINE),
    "keywords":         re.compile(r"^(?:KEYWORDS|MOTS-CLÉS|SCHLÜSSELWÖRTER|PALABRAS CLAVE|PAROLE CHIAVE|TREFWOORDEN|キーワード|키워드|关键词|PALAVRAS-CHAVE)[^\n]*$", re.MULTILINE),
}

def extract_between(text, start_pattern, end_pattern):
    """Extrait le texte entre le marqueur de début et le marqueur de fin."""
    m_start = start_pattern.search(text)
    if not m_start:
        return ""
    content_start = m_start.end()
    m_end = end_pattern.search(text, content_start)
    content_end = m_end.start() if m_end else len(text)
    return text[content_start:content_end].strip()

def extract_single_line(text, pattern):
    """Extrait la première ligne non vide après le marqueur."""
    m = pattern.search(text)
    if not m:
        return ""
    rest = text[m.end():]
    for line in rest.splitlines():
        line = line.strip()
        if line:
            return line
    return ""

def extract_fields(text):
    markers = SECTION_MARKERS
    return {
        "name.txt":             extract_single_line(text, markers["name"]),
        "subtitle.txt":         extract_single_line(text, markers["subtitle"]),
        "promotional_text.txt": extract_single_line(text, markers["promotional_text"]),
        "description.txt":      extract_between(text, markers["description"], markers["keywords"]),
        "keywords.txt":         extract_single_line(text, markers["keywords"]),
    }

def write_file(path, content):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content + "\n")

for filename, locale in LOCALES.items():
    src_path = os.path.join(SRC, filename)
    if not os.path.exists(src_path):
        print(f"[SKIP] {filename} introuvable")
        continue

    with open(src_path, encoding="utf-8") as f:
        text = f.read()

    locale_dir = os.path.join(DST, locale)
    fields = extract_fields(text)

    for fname, content in fields.items():
        if content:
            write_file(os.path.join(locale_dir, fname), content)
            print(f"[OK]   {locale}/{fname}")
        else:
            print(f"[WARN] {locale}/{fname} — champ non extrait")

print("\nMigration terminée → fastlane/metadata/")
