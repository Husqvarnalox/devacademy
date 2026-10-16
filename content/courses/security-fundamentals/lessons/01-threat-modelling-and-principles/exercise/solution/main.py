import sys

LEVEL = {"internet": 0, "dmz": 1, "internal": 2}
LETTERS = "STRIDE"


def classify(comps, flow):
    src, dst, proto, op = flow
    skind, szone = comps[src]
    dkind, dzone = comps[dst]
    found = set()
    if skind == "external" or szone != dzone:
        found.add("S")
    if proto == "plain" or op == "write":
        found.add("T")
    if op in ("write", "call") and dkind != "external":
        found.add("R")
    if proto == "plain" or op == "read":
        found.add("I")
    if dkind == "process" or szone == "internet":
        found.add("D")
    if dkind == "process" and LEVEL[szone] < LEVEL[dzone]:
        found.add("E")
    return [c for c in LETTERS if c in found], szone != dzone


def main():
    lines = sys.stdin.read().split("\n")
    n, m = map(int, lines[0].split())
    comps = {}
    for line in lines[1:1 + n]:
        name, kind, zone = line.split()
        comps[name] = (kind, zone)
    totals = dict.fromkeys(LETTERS, 0)
    crossings = 0
    for line in lines[1 + n:1 + n + m]:
        flow = tuple(line.split())
        letters, crosses = classify(comps, flow)
        crossings += crosses
        for c in letters:
            totals[c] += 1
        print(f"{flow[0]}->{flow[1]}: {','.join(letters) if letters else 'none'}")
    print(f"crossings: {crossings}")
    print("summary: " + " ".join(f"{c}={totals[c]}" for c in LETTERS))


main()
