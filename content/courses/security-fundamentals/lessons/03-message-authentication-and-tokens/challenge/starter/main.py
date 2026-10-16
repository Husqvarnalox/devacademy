import base64
import hmac
import json
import re
import sys

B64URL = re.compile(r"[A-Za-z0-9_-]*")


def b64url_decode(text):
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def verify(token, keys, now):
    """keys: kid -> (key_bytes, status) with status active | retired | revoked.
    Return 'VALID kid=<kid>' or 'REJECT <reason>'. Reuse your verifier from the exercise and add the kid step."""
    # TODO: structure, header, alg, then kid (missing-kid, unknown-kid, revoked-kid), signature with THAT key, claims.
    return "REJECT todo"


def main():
    lines = sys.stdin.read().splitlines()
    k = int(lines[0])
    keys = {}
    for line in lines[1:1 + k]:
        kid, key_b64, status = line.split()
        keys[kid] = (b64url_decode(key_b64), status)
    for line in lines[1 + k:]:
        now_text, _, token = line.partition(" ")
        print(verify(token, keys, int(now_text)))


main()
