import sys


def match(pattern, value):
    if pattern == "*" or pattern == value:
        return True
    return pattern.endswith(":*") and value.startswith(pattern[:-1])


def main():
    roles, users, owner_perms, owners, denies, requests = {}, {}, [], {}, [], []
    for line in sys.stdin.read().split("\n"):
        tokens = line.split()
        if not tokens:
            continue
        kind, args = tokens[0], tokens[1:]
        if kind == "ROLE":
            roles.setdefault(args[0], []).extend(args[1:])
        elif kind == "USER":
            users.setdefault(args[0], []).extend(args[1:])
        elif kind == "OWNERPERM":
            owner_perms.extend(args)
        elif kind == "OWNER":
            owners[args[0]] = args[1]
        elif kind == "DENY":
            denies.append(tuple(args))
        elif kind == "REQ":
            requests.append(tuple(args))
    for user, perm, resource in requests:
        if user not in users:
            print("DENY unknown-user")
        elif any(match(u, user) and match(p, perm) and match(r, resource) for u, p, r in denies):
            print("DENY explicit")
        elif owners.get(resource) == user and any(match(p, perm) for p in owner_perms):
            print("ALLOW owner")
        else:
            for role in users[user]:
                if any(match(p, perm) for p in roles.get(role, [])):
                    print(f"ALLOW role={role}")
                    break
            else:
                print("DENY default")


main()
