import sys


def main():
    lines = [l.split() for l in sys.stdin.read().splitlines() if l.strip()]
    _, threshold, timeout, probes = lines[0]
    threshold, timeout, probes = int(threshold), int(timeout), int(probes)
    state, failures, successes, opened_at = "closed", 0, 0, 0
    calls = rejected = opens = 0
    for t, outcome in lines[1:]:
        t = int(t)
        if state == "open" and t - opened_at >= timeout:
            state, successes = "half_open", 0
        if state == "open":
            rejected += 1
            print(f"{t} rejected state=open")
            continue
        calls += 1
        if outcome == "ok":
            if state == "half_open":
                successes += 1
                if successes >= probes:
                    state, failures = "closed", 0
            else:
                failures = 0
        else:
            if state == "half_open":
                state, opened_at = "open", t
                opens += 1
            else:
                failures += 1
                if failures >= threshold:
                    state, opened_at = "open", t
                    opens += 1
        print(f"{t} {outcome} state={state}")
    print(f"calls={calls} rejected={rejected} opens={opens}")


main()
