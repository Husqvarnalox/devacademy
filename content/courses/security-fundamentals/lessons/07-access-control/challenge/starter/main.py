import re
import sys

COND = re.compile(r"(.+?)(==|!=|>=|<=|>|<)(.+)")
INT = re.compile(r"-?[0-9]+")


def match(pattern, value):
    """Permission pattern: '*', exact, or 'ns:*'."""
    return pattern == "*" or pattern == value or (pattern.endswith(":*") and value.startswith(pattern[:-1]))


def holds(cond, user, res, env):
    """Evaluate one condition like 'user.dept==resource.dept' or 'env.hour>=9'.
    user/res/env are dicts of strings. A missing attribute makes the condition false."""
    # TODO
    return False


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
        # TODO: unknown user/resource -> DENY; any matching deny rule -> DENY; any matching allow rule -> ALLOW; else DENY
        print("TODO")


main()
