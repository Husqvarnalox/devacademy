import re
import sys

MAX_AGE_LIMIT = 30 * 24 * 3600


def audit(line):
    """Return (cookie_name, [finding codes in the order from the task]) or None if malformed."""
    parts = [p.strip() for p in line.split(";")]
    # TODO: parse name=value, collect attributes (names case-insensitive, last one wins),
    # then apply the rules from the task statement in order.
    return None


def main():
    lines = sys.stdin.read().split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    for number, line in enumerate(lines, 1):
        result = audit(line)
        if result is None:
            print("TODO")
        else:
            name, found = result
            print(f"{name}: {' '.join(found) if found else 'OK'}")


main()
