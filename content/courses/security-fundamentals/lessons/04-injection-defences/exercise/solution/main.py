import posixpath
import re
import sys
from urllib.parse import unquote

PCT = re.compile(r"%[0-9A-Fa-f]{2}")
DRIVE = re.compile(r"[A-Za-z]:")


def resolve(base, user):
    """Return the resolved path inside base, or None to reject."""
    if not user:
        return None
    decoded = unquote(user)  # exactly one pass
    if PCT.search(decoded):  # still encoded after one pass: double encoding
        return None
    if "\x00" in decoded or "\\" in decoded:
        return None
    if decoded.startswith("/") or DRIVE.match(decoded):
        return None
    joined = posixpath.normpath(posixpath.join(base, decoded))
    if joined == base or posixpath.commonpath([base, joined]) != base:
        return None
    return joined


def main():
    lines = sys.stdin.read().split("\n")
    base = lines[0]
    n = int(lines[1])
    for user in lines[2:2 + n]:
        result = resolve(base, user)
        print("REJECT" if result is None else result)


main()
