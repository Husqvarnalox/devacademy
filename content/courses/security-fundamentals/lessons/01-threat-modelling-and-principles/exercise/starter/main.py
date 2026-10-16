import sys

LEVEL = {"internet": 0, "dmz": 1, "internal": 2}
LETTERS = "STRIDE"


def classify(comps, flow):
    """comps: name -> (kind, zone); flow: (src, dst, proto, op).
    Return (list of STRIDE letters in the order S,T,R,I,D,E; crosses_boundary)."""
    src, dst, proto, op = flow
    # TODO: apply the six rules from the task statement.
    return [], False


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
        # TODO: update crossings and totals, print "<src>-><dst>: S,T,..." or "none".
    # TODO: print "crossings: K" and "summary: S=.. T=.. R=.. I=.. D=.. E=.."


main()
