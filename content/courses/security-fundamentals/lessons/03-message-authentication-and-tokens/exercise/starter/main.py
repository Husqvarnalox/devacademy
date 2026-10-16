import base64
import hashlib
import hmac
import json
import re
import sys

B64URL = re.compile(r"[A-Za-z0-9_-]*")


def b64url_decode(text):
    """Decode base64url text that has no '=' padding."""
    return base64.urlsafe_b64decode(text + "=" * (-len(text) % 4))


def verify(token, key, now):
    """Return 'VALID' or 'REJECT <reason>'. Check order and reasons are in the task statement."""
    # TODO: 1) structure -> malformed  2) header -> bad-header  3) alg -> unsupported-alg
    #       4) signature -> bad-signature  5) payload -> bad-payload
    #       6) exp/nbf -> missing-exp, bad-claim, expired, not-yet-valid
    return "REJECT todo"


def main():
    lines = sys.stdin.read().splitlines()
    key = b64url_decode(lines[0])
    for line in lines[1:]:
        now_text, _, token = line.partition(" ")
        print(verify(token, key, int(now_text)))


main()
