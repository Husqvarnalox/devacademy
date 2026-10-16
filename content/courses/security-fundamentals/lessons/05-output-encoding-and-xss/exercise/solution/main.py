import re
import sys

BODY = {"&": "&amp;", "<": "&lt;", ">": "&gt;"}
ATTR = {
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#x27;", "`": "&#x60;",
    "\n": "&#xA;", "\r": "&#xD;", "\t": "&#x9;",
}
SCHEME = re.compile(r"[A-Za-z][A-Za-z0-9+.\-]*")
ALLOWED = {"http", "https", "mailto"}


def escape(text, table):
    return "".join(table.get(ch, ch) for ch in text)


def clean(url):
    url = url.replace("\t", "").replace("\r", "").replace("\n", "")
    return url.strip("".join(chr(i) for i in range(0x21)))


def safe_href(url):
    """Return the cleaned URL if it may be put into href, otherwise None."""
    c = clean(url)
    if not c:
        return None
    cut = len(c)
    for ch in "/?#":
        i = c.find(ch)
        if i != -1:
            cut = min(cut, i)
    head = c[:cut]
    if ":" in head:
        scheme = c[:c.index(":")]
        if not SCHEME.fullmatch(scheme) or scheme.lower() not in ALLOWED:
            return None
        return c
    if c.startswith("//") or c.startswith("\\") or c.startswith("/\\"):
        return None
    return c


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
            print("DENY" if cleaned is None else "ALLOW " + escape(cleaned, ATTR))


main()
