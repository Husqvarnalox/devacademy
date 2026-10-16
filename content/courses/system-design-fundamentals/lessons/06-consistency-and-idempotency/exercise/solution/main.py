import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    limit = int(lines[0])
    charged = {}
    seen = {}  # key -> (account, amount, outcome text)
    charges = 0
    for n, line in enumerate(lines[1:], start=1):
        key, account, amount = line.split()
        amount = int(amount)
        if key != "-" and key in seen:
            acc0, amt0, outcome = seen[key]
            if (acc0, amt0) == (account, amount):
                print(f"{n} {key} REPLAY {outcome}")
            else:
                print(f"{n} {key} CONFLICT")
            continue
        if amount <= 0:
            outcome = "INVALID"
        elif charged.get(account, 0) + amount > limit:
            outcome = "DECLINED"
        else:
            charges += 1
            charged[account] = charged.get(account, 0) + amount
            outcome = f"OK charge=c{charges} charged={charged[account]}"
        if key != "-":
            seen[key] = (account, amount, outcome)
        print(f"{n} {key} {outcome}")
    for account in sorted(charged):
        print(f"ACCOUNT {account} charged={charged[account]}")
    print(f"charges={charges}")


main()
