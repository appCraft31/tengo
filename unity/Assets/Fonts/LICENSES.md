# Polices de tenGO : licences

Toutes les polices de ce dossier sont sous **SIL Open Font License 1.1** (`Nunito-OFL.txt`, `Fallback/NotoSansCJK-OFL.txt`). L'OFL autorise l'usage, l'intégration dans une application (y compris commerciale), la modification et la redistribution, à condition de ne pas vendre les polices seules, de garder la licence et le copyright, et de ne pas donner un « nom de police réservé » à une version modifiée.

| Fichier | Famille, source | Copyright | Nom réservé | Modification |
|---|---|---|---|---|
| `Nunito-Regular.ttf`, `-SemiBold`, `-Bold`, `-ExtraBold`, `-Black` | Nunito, instances statiques du fichier variable de l'app Android (`android/app/src/main/res/font/nunito.ttf`, google/fonts `ofl/nunito`) | Copyright 2014 The Nunito Project Authors (https://github.com/googlefonts/nunito) | aucun | instances figées (wght 400 à 900) avec fontTools `varLib.instancer` (étape 1) |
| `Fallback/NotoSansJP-Bold.ttf` | Noto Sans JP, google/fonts `ofl/notosansjp/NotoSansJP[wght].ttf` | Copyright 2014-2021 Adobe (http://www.adobe.com/) | « Source » (non utilisé : les fichiers s'appellent Noto Sans) | instance wght 700, **réduite** aux caractères des traductions ja, ko, zh-Hans + ASCII, ponctuation CJK, pleine chasse, kana |
| `Fallback/NotoSansKR-Bold.ttf` | Noto Sans KR, `ofl/notosanskr` | idem | idem | idem |
| `Fallback/NotoSansSC-Bold.ttf` | Noto Sans SC, `ofl/notosanssc` | idem | idem | idem |

Fabrication des replis : `Tools~/build_cjk_fallbacks.py` (fontTools), à relancer quand `ios/tenGO/Localizable.xcstrings` change (sinon les nouveaux caractères CJK manqueront). Polices TMP et chaîne de repli : `Tools/unity.sh TenGo.Editor.ProjectSetup.Apply`.

Ordre des replis : japonais, coréen, chinois simplifié ; `Fonts.ApplyLanguage("zh-Hans")` place le chinois en tête pour que les idéogrammes communs prennent la forme chinoise.
