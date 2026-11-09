"""Réempaquette com.google.firebase.app pour dépendre de l'EDM d'Unity.

Usage : python3 -I repack_firebase_app.py <archive officielle> <archive de sortie>
Seul package/package.json change : la dépendance
« com.google.external-dependency-manager » devient
« com.unity.external-dependency-manager » 2.1.0. Le reste est recopié tel quel.
"""
import io
import json
import sys
import tarfile

src, dst = sys.argv[1], sys.argv[2]
with tarfile.open(src, "r:gz") as tin, tarfile.open(dst, "w:gz") as tout:
    for member in tin.getmembers():
        data = tin.extractfile(member) if member.isfile() else None
        if member.name == "package/package.json":
            manifest = json.load(data)
            deps = manifest.get("dependencies", {})
            deps.pop("com.google.external-dependency-manager", None)
            deps["com.unity.external-dependency-manager"] = "2.1.0"
            manifest["dependencies"] = deps
            raw = (json.dumps(manifest, indent=2) + "\n").encode("utf-8")
            member.size = len(raw)
            tout.addfile(member, io.BytesIO(raw))
        else:
            tout.addfile(member, data)
