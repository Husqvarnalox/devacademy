import re
import sys

# TODO: fill the two escape tables from the task statement (body and double-quoted attribute).
BODY = {}
ATTR = {}
ALLOWED = {"http", "https", "mailto"}


def escape(text, table):
    """Replace every character that has an entry in the table; leave the rest unchanged."""
    # TODO
    return text


def clean(url):
    """Remove tab/CR/LF anywhere; strip leading and trailing characters <= U+0020."""
    # TODO
    return url


def safe_href(url):
    """Return the cleaned URL if it may be used in href, otherwise None."""
    # TODO: scheme allowlist, relative-URL rules, empty -> None
    return None


def main():
    for line in sys.stdin.read().split("\n"):
        if not line:
            continue
        cmd, _, text = line.partition(" ")
        if cmd == "body":
            print(escape(text, BODY))
        elif cmd == "attr":
            print(escape(text, ATTR))
        elif cmd == "href":
            cleaned = safe_href(text)
            print("TODO" if cleaned is None else "ALLOW " + escape(cleaned, ATTR))


main()
