import base64
import hashlib
import hmac
import json
import re
import sys

B64URL = re.compile(r"[A-Za-z0-9_-]*")


def b64url_decode(text):
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def well_formed(token):
    parts = token.split(".")
    if len(parts) != 3:
        return None
    for i, p in enumerate(parts):
        if not B64URL.fullmatch(p) or len(p) % 4 == 1:
            return None
        if i < 2 and not p:
            return None
    return parts


def json_object(text):
    try:
        value = json.loads(b64url_decode(text).decode("utf-8"))
    except (ValueError, UnicodeDecodeError):
        return None
    return value if isinstance(value, dict) else None


def is_number(x):
    return isinstance(x, (int, float)) and not isinstance(x, bool)


def verify(token, key, now):
    parts = well_formed(token)
    if parts is None:
        return "REJECT malformed"
    header = json_object(parts[0])
    if header is None:
        return "REJECT bad-header"
    if header.get("alg") != "HS256":
        return "REJECT unsupported-alg"
    signing_input = (parts[0] + "." + parts[1]).encode("ascii")
    expected = hmac.digest(key, signing_input, "sha256")
    try:
        signature = b64url_decode(parts[2]) if parts[2] else b""
    except ValueError:
        signature = b""
    if not hmac.compare_digest(expected, signature):
        return "REJECT bad-signature"
    payload = json_object(parts[1])
    if payload is None:
        return "REJECT bad-payload"
    if "exp" not in payload:
        return "REJECT missing-exp"
    if not is_number(payload["exp"]) or ("nbf" in payload and not is_number(payload["nbf"])):
        return "REJECT bad-claim"
    if now >= payload["exp"]:
        return "REJECT expired"
    if "nbf" in payload and now < payload["nbf"]:
        return "REJECT not-yet-valid"
    return "VALID"


def main():
    lines = sys.stdin.read().splitlines()
    key = b64url_decode(lines[0])
    for line in lines[1:]:
        now_text, _, token = line.partition(" ")
        print(verify(token, key, int(now_text)))


main()
