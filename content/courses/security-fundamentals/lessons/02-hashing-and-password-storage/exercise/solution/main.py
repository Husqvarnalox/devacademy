import base64
import binascii
import hashlib
import hmac
import sys

SCHEME = "pbkdf2_sha256"
DKLEN = 32
MAX_ITERATIONS = 10_000_000


def derive(password, salt, iterations):
    return hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, iterations, DKLEN)


def create(iterations, salt, password):
    digest = derive(password, salt, iterations)
    return "$".join([
        SCHEME,
        str(iterations),
        base64.b64encode(salt).decode("ascii"),
        base64.b64encode(digest).decode("ascii"),
    ])


def parse(record):
    """Return (iterations, salt, digest) or None for any malformed record."""
    parts = record.split("$")
    if len(parts) != 4 or parts[0] != SCHEME:
        return None
    it_text = parts[1]
    if not (it_text.isascii() and it_text.isdigit()):
        return None
    iterations = int(it_text)
    if iterations < 1 or iterations > MAX_ITERATIONS:
        return None
    try:
        salt = base64.b64decode(parts[2], validate=True)
        digest = base64.b64decode(parts[3], validate=True)
    except (binascii.Error, ValueError):
        return None
    if not salt:
        return None
    return iterations, salt, digest


def verify(record, password):
    parsed = parse(record)
    if parsed is None:
        return "INVALID"
    iterations, salt, digest = parsed
    candidate = derive(password, salt, iterations)
    return "OK" if hmac.compare_digest(candidate, digest) else "FAIL"


def check(record, minimum):
    parsed = parse(record)
    if parsed is None:
        return "INVALID"
    return "REHASH" if parsed[0] < minimum else "CURRENT"


def main():
    for line in sys.stdin.read().split("\n"):
        if not line.strip():
            continue
        cmd, _, rest = line.partition(" ")
        if cmd == "create":
            iterations, salt_hex, password = rest.split(" ", 2)
            print(create(int(iterations), bytes.fromhex(salt_hex), password))
        elif cmd == "verify":
            record, password = rest.split(" ", 1)
            print(verify(record, password))
        elif cmd == "check":
            record, minimum = rest.split(" ")
            print(check(record, int(minimum)))


main()
