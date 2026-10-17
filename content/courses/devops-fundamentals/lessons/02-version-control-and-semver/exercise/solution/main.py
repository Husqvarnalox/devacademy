import re
import sys

SEMVER = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
    r"(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$"
)  # регулярное выражение с semver.org


def parse(text):
    """-> (major, minor, patch, [pre-идентификаторы], build_строка_или_None) или None."""
    m = SEMVER.match(text)
    if m is None:
        return None
    pre = m.group(4).split(".") if m.group(4) else []
    return int(m.group(1)), int(m.group(2)), int(m.group(3)), pre, m.group(5)


def cmp_ident(a, b):
    a_num, b_num = a.isdigit(), b.isdigit()
    if a_num and b_num:
        a, b = int(a), int(b)
    elif a_num != b_num:
        return -1 if a_num else 1  # числовые идентификаторы младше буквенных
    return (a > b) - (a < b)


def compare(x, y):
    for i in range(3):
        if x[i] != y[i]:
            return -1 if x[i] < y[i] else 1
    px, py = x[3], y[3]
    if not px and not py:
        return 0
    if not px:
        return 1  # версия без пре-релиза старше
    if not py:
        return -1
    for a, b in zip(px, py):
        c = cmp_ident(a, b)
        if c:
            return c
    return (len(px) > len(py)) - (len(px) < len(py))


def render(v):
    s = f"{v[0]}.{v[1]}.{v[2]}"
    if v[3]:
        s += "-" + ".".join(v[3])
    if v[4] is not None:
        s += "+" + v[4]
    return s


def bump(kind, v):
    major, minor, patch, pre, _ = v
    if kind == "major":
        return major + 1, 0, 0, [], None
    if kind == "minor":
        return major, minor + 1, 0, [], None
    if kind == "patch":
        return major, minor, patch + 1, [], None
    if kind == "prerelease":
        if not pre:
            return major, minor, patch + 1, ["rc", "1"], None
        if pre[-1].isdigit():
            return major, minor, patch, pre[:-1] + [str(int(pre[-1]) + 1)], None
        return major, minor, patch, pre + ["1"], None
    return None


def main():
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        cmd, args = p[0], p[1:]
        if cmd == "PARSE":
            v = parse(args[0])
            print("INVALID" if v is None else f"major={v[0]} minor={v[1]} patch={v[2]} pre={'.'.join(v[3]) or '-'} build={v[4] or '-'}")
        elif cmd == "CMP":
            a, b = parse(args[0]), parse(args[1])
            print("INVALID" if a is None or b is None else compare(a, b))
        elif cmd == "BUMP":
            v = parse(args[1])
            r = None if v is None else bump(args[0], v)
            print("INVALID" if r is None else render(r))
        elif cmd == "SORT":
            good = [(parse(a), a) for a in args if parse(a) is not None]
            bad = [a for a in args if parse(a) is None]
            import functools
            good.sort(key=functools.cmp_to_key(lambda x, y: compare(x[0], y[0])))
            print(" ".join(a for _, a in good))
            if bad:
                print("INVALID " + " ".join(bad))


main()
