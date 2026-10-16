import sys


def match(pattern, value):
    """Pattern is '*', an exact string, or 'ns:*' (any value starting with 'ns:')."""
    # TODO
    return False


def main():
    roles, users, owner_perms, owners, denies, requests = {}, {}, [], {}, [], []
    for line in sys.stdin.read().split("\n"):
        tokens = line.split()
        if not tokens:
            continue
        kind, args = tokens[0], tokens[1:]
        # TODO: ROLE <role> <perm>..., USER <user> <role>..., OWNERPERM <perm>..., OWNER <resource> <user>,
        #       DENY <user> <perm> <resource>, REQ <user> <perm> <resource>
        if kind == "REQ":
            requests.append(tuple(args))
    for user, perm, resource in requests:
        # TODO: decide in the order from the task statement and print the decision
        print("TODO")


main()
