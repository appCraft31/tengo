#!/usr/bin/env python3
"""Génère Assets/Resources/Localization/strings.json depuis le catalogue iOS.

Usage (depuis unity/) :
  python3 -I Tools/i18n/gen_strings.py ../ios/tenGO/Localizable.xcstrings Assets/Resources/Localization/strings.json

Format de sortie : {"languages": [...], "strings": {clé: {langue: texte}}}.
Les formats Swift sont convertis pour `string.Format` de C# :
  %d, %lld, %@ → {0}, {1}… dans l'ordre d'apparition ; %1$d, %2$@ → {0}, {1} ;
  %% → % ; les accolades littérales sont doublées.
À relancer quand les textes iOS changent (étape 6 : tous les écrans).
"""
import json, re, sys

SPEC = re.compile(r"%(?:(\d+)\$)?(?:lld|ld|d|@|f|\.\d+f|%)")

def convert(text: str) -> str:
    out, last, auto = [], 0, 0
    text = text.replace("{", "{{").replace("}", "}}")
    for m in SPEC.finditer(text):
        out.append(text[last:m.start()])
        if m.group(0) == "%%":
            out.append("%")
        else:
            if m.group(1):
                idx = int(m.group(1)) - 1
            else:
                idx = auto
                auto += 1
            out.append("{%d}" % idx)
        last = m.end()
    out.append(text[last:])
    return "".join(out)

def main(src, dst):
    catalog = json.load(open(src, encoding="utf-8"))
    langs, strings = set(), {}
    for key, entry in sorted(catalog["strings"].items()):
        per = {}
        for lang, loc in entry.get("localizations", {}).items():
            unit = loc.get("stringUnit")
            if unit is None:
                # Variations (pluriel…) : forme « other ».
                plural = loc.get("variations", {}).get("plural", {})
                unit = plural.get("other", {}).get("stringUnit")
            if unit is None:
                continue
            per[lang] = convert(unit["value"])
            langs.add(lang)
        if per:
            strings[key] = per
    out = {"source": catalog.get("sourceLanguage", "fr"), "languages": sorted(langs), "strings": strings}
    with open(dst, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=0, sort_keys=True)
        f.write("\n")
    print(f"{len(strings)} clés, {len(langs)} langues → {dst}")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
