import re
import sys

MAX_AGE_LIMIT = 30 * 24 * 3600
INT = re.compile(r"-?[0-9]+")


def audit(line):
    parts = [p.strip() for p in line.split(";")]
    name, eq, _value = parts[0].partition("=")
    name = name.strip()
    if not eq or not name:
        return None
    attrs = {}
    for part in parts[1:]:
        if not part:
            continue
        key, _, val = part.partition("=")
        attrs[key.strip().lower()] = val.strip()
    secure = "secure" in attrs
    found = []
    if not secure:
        found.append("no-secure")
    if "httponly" not in attrs and not name.lower().startswith("csrf"):
        found.append("no-httponly")
    if "samesite" not in attrs:
        found.append("no-samesite")
    else:
        mode = attrs["samesite"].lower()
        if mode not in ("strict", "lax", "none"):
            found.append("bad-samesite")
        elif mode == "none" and not secure:
            found.append("samesite-none-insecure")
    max_age = attrs.get("max-age")
    if max_age is not None and INT.fullmatch(max_age) and int(max_age) > MAX_AGE_LIMIT:
        found.append("long-lived")
    if name.startswith("__Host-") and (not secure or attrs.get("path") != "/" or "domain" in attrs):
        found.append("host-prefix")
    if name.startswith("__Secure-") and not secure:
        found.append("secure-prefix")
    return name, found


def main():
    lines = sys.stdin.read().split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    for number, line in enumerate(lines, 1):
        result = audit(line)
        if result is None:
            print(f"line {number}: MALFORMED")
        else:
            name, found = result
            print(f"{name}: {' '.join(found) if found else 'OK'}")


main()
