import hashlib
import sys

ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"


def encode(n):
    """Целое -> строка base62 (0 -> '0')."""
    # TODO
    return ""


def decode(s):
    """Строка base62 -> целое."""
    # TODO
    return 0


def hash_code(url, attempt, length):
    """Кандидат кода для URL при попытке attempt (по условию)."""
    # TODO
    return ""


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    mode, param = lines[0].split()
    param = int(param)
    for line in lines[1:]:
        cmd, arg = line.split()
        # TODO: ENCODE / DECODE / SHORTEN / RESOLVE по условию
    # TODO: напечатайте итог "urls=... collisions=..."


main()
