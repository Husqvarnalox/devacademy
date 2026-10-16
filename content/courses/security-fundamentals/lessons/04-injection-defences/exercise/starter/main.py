import posixpath
import re
import sys
from urllib.parse import unquote


def resolve(base, user):
    """Return the resolved absolute path inside base, or None to reject.
    base is an absolute, already normalised POSIX path such as /srv/app/uploads."""
    # TODO: apply the rules from the task statement in order:
    # empty -> decode once -> still %XX / NUL / backslash -> absolute or drive -> normalise -> containment.
    return "TODO"  # replace with the path string, or None to reject


def main():
    lines = sys.stdin.read().split("\n")
    base = lines[0]
    n = int(lines[1])
    for user in lines[2:2 + n]:
        result = resolve(base, user)
        print("REJECT" if result is None else result)


main()
