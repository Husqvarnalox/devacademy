import base64
import binascii
import hashlib
import hmac
import sys

SCHEME = "pbkdf2_sha256"
DKLEN = 32
MAX_ITERATIONS = 10_000_000


def create(iterations, salt, password):
    """Return 'pbkdf2_sha256$<iterations>$<salt_b64>$<hash_b64>' (standard base64 with padding)."""
    # TODO: hashlib.pbkdf2_hmac("sha256", password bytes, salt, iterations, DKLEN)
    return "TODO"


def parse(record):
    """Return (iterations, salt, digest) or None if the record is malformed."""
    # TODO: validate the shape exactly as the task statement says.
    return None


def verify(record, password):
    """Return 'OK', 'FAIL' or 'INVALID'. Compare digests with hmac.compare_digest."""
    # TODO
    return "TODO"


def check(record, minimum):
    """Return 'REHASH' (iterations < minimum), 'CURRENT' or 'INVALID'."""
    # TODO
    return "TODO"


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
