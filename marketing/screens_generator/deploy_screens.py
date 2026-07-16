#!/usr/bin/env python3
# Distribue les rendus vers les arborescences fastlane (ASC + Play).
import os, shutil, glob

OUT = "/private/tmp/claude-501/-Users-nicolas-StudioProjects-tenGO/f5f7da44-b45a-4058-88ad-c6c360ba25d9/scratchpad/render/out"
IOS = "/Users/nicolas/StudioProjects/tenGO/ios/fastlane/screenshots"
AND = "/Users/nicolas/StudioProjects/tenGO/android/fastlane/metadata/android"

IOS_LOCALES = {
 "de-DE":"de", "en-US":"en", "en-AU":"en", "en-CA":"en", "en-GB":"en",
 "es-ES":"es", "es-MX":"es", "fr-FR":"fr", "it":"it", "ja":"ja", "ko":"ko",
 "nl-NL":"nl", "pt-BR":"pt-BR", "pt-PT":"pt-BR", "zh-Hans":"zh-Hans",
}
AND_LOCALES = {
 "de-DE":"de", "en-AU":"en", "en-CA":"en", "en-GB":"en", "en-US":"en",
 "es-419":"es", "es-ES":"es", "fr-FR":"fr", "it-IT":"it", "ja-JP":"ja",
 "ko-KR":"ko", "nl-NL":"nl", "pt-BR":"pt-BR", "pt-PT":"pt-BR", "zh-CN":"zh-Hans",
}

n_ios = n_and = 0

for loc, lang in IOS_LOCALES.items():
    d = os.path.join(IOS, loc)
    os.makedirs(d, exist_ok=True)
    for p in range(1, 7):
        shutil.copy(f"{OUT}/p{p}_{lang}_phone.png", f"{d}/iPhone 6.5 inch-{p}.png")
        shutil.copy(f"{OUT}/p{p}_{lang}_ipad.png",
                    f"{d}/iPad Pro (12.9-inch) (3rd generation)-{p}.png")
        n_ios += 2

for loc, lang in AND_LOCALES.items():
    base = os.path.join(AND, loc, "images")
    for sub, fmt in [("phoneScreenshots", "play"),
                     ("sevenInchScreenshots", "ipad"),
                     ("tenInchScreenshots", "ipad")]:
        d = os.path.join(base, sub)
        os.makedirs(d, exist_ok=True)
        for old in glob.glob(os.path.join(d, "*")):
            os.remove(old)
        for p in range(1, 7):
            shutil.copy(f"{OUT}/p{p}_{lang}_{fmt}.png", f"{d}/{p}.png")
            n_and += 1

print(f"iOS : {n_ios} fichiers dans {len(IOS_LOCALES)} locales")
print(f"Play : {n_and} fichiers dans {len(AND_LOCALES)} locales")
