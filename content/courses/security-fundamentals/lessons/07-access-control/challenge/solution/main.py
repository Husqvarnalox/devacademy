import re
import sys

COND = re.compile(r"(.+?)(==|!=|>=|<=|>|<)(.+)")
INT = re.compile(r"-?[0-9]+")


def match(pattern, value):
    if pattern == "*" or pattern == value:
        return True
    return pattern.endswith(":*") and value.startswith(pattern[:-1])


def operand(text, user, res, env):
    """Return (found, value)."""
    for prefix, attrs in (("user.", user), ("resource.", res), ("env.", env)):
        if text.startswith(prefix):
            name = text[len(prefix):]
            return (name in attrs), attrs.get(name)
    return True, text


def holds(cond, user, res, env):
    m = COND.fullmatch(cond)
    if not m:
        return False
    lhs, op, rhs = m.groups()
    ok1, a = operand(lhs, user, res, env)
    ok2, b = operand(rhs, user, res, env)
    if not (ok1 and ok2):
        return False
    both_int = INT.fullmatch(a) and INT.fullmatch(b)
    if op in ("==", "!="):
        equal = (int(a) == int(b)) if both_int else (a == b)
        return equal if op == "==" else not equal
    if not both_int:
        return False
    a, b = int(a), int(b)
    return {">=": a >= b, "<=": a <= b, ">": a > b, "<": a < b}[op]


def attrs_of(tokens):
    out = {}
    for t in tokens:
        k, _, v = t.partition("=")
        out[k] = v
    return out


def main():
    rules, users, resources, env, requests = [], {}, {}, {}, []
    for line in sys.stdin.read().split("\n"):
        tokens = line.split()
        if not tokens:
            continue
        kind, args = tokens[0], tokens[1:]
        if kind == "RULE":
            rules.append((args[0], args[1], args[2:]))
        elif kind == "USER":
            users[args[0]] = attrs_of(args[1:])
        elif kind == "RES":
            resources[args[0]] = attrs_of(args[1:])
        elif kind == "ENV":
            env.update(attrs_of(args))
        elif kind == "REQ":
            requests.append(tuple(args))
    for user, perm, resource in requests:
        if user not in users or resource not in resources:
            print("DENY")
            continue
        u, r = users[user], resources[resource]
        matching = [effect for effect, p, conds in rules
                    if match(p, perm) and all(holds(c, u, r, env) for c in conds)]
        print("ALLOW" if "deny" not in matching and "allow" in matching else "DENY")


main()
