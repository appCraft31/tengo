#!/usr/bin/env python3
# Liste les localisations de la fiche ASC (app + version 2.2) sans dépendance :
# JWT ES256 signé via openssl, appels API via urllib.
import base64, json, subprocess, tempfile, time, urllib.request, os

KEY = "/Users/nicolas/StudioProjects/tenGO/ios/fastlane/AuthKey_U5B34558L6.p8"
KID = "U5B34558L6"
ISS = "69a6de88-d732-47e3-e053-5b8c7c11a4d1"

def b64url(b): return base64.urlsafe_b64encode(b).rstrip(b"=")

def der_to_raw(der):
    # signature DER → r||s (32 octets chacun)
    assert der[0] == 0x30
    i = 2
    assert der[i] == 0x02
    lr = der[i+1]; r = der[i+2:i+2+lr]; i += 2 + lr
    assert der[i] == 0x02
    ls = der[i+1]; s = der[i+2:i+2+ls]
    return int.from_bytes(r, "big").to_bytes(32, "big") + int.from_bytes(s, "big").to_bytes(32, "big")

def jwt_token():
    header = b64url(json.dumps({"alg": "ES256", "kid": KID, "typ": "JWT"}).encode())
    payload = b64url(json.dumps({"iss": ISS, "exp": int(time.time()) + 1200,
                                 "aud": "appstoreconnect-v1"}).encode())
    signing_input = header + b"." + payload
    with tempfile.NamedTemporaryFile(suffix=".bin", delete=False) as f:
        f.write(signing_input); tmp = f.name
    der = subprocess.run(["openssl", "dgst", "-sha256", "-sign", KEY, tmp],
                         capture_output=True, check=True).stdout
    os.unlink(tmp)
    return (signing_input + b"." + b64url(der_to_raw(der))).decode()

TOK = jwt_token()

def get(path):
    req = urllib.request.Request("https://api.appstoreconnect.apple.com" + path,
                                 headers={"Authorization": "Bearer " + TOK})
    return json.load(urllib.request.urlopen(req))

app = get("/v1/apps?filter[bundleId]=AppCraft31.tenGO")["data"][0]
app_id = app["id"]

# Localisations de l'app (infos) et de la version 2.2
vers = get(f"/v1/apps/{app_id}/appStoreVersions?filter[versionString]=2.2&limit=1")["data"]
if not vers:
    print("version 2.2 introuvable sur ASC")
    raise SystemExit
vid = vers[0]["id"]
print("état version 2.2 :", vers[0]["attributes"]["appStoreState"])
locs = get(f"/v1/appStoreVersions/{vid}/appStoreVersionLocalizations?limit=50")["data"]
print("localisations de la version 2.2 :")
for l in sorted(locs, key=lambda x: x["attributes"]["locale"]):
    print(" -", l["attributes"]["locale"])
