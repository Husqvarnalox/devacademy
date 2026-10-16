import sys

# event -> (state before, state after)
TRANSITIONS = {
    "create": ("NONE", "NEW"),
    "admit": ("NEW", "READY"),
    "dispatch": ("READY", "RUNNING"),
    "timeout": ("RUNNING", "READY"),
    "block": ("RUNNING", "WAITING"),
    "wake": ("WAITING", "READY"),
    "exit": ("RUNNING", "TERMINATED"),
}


def simulate(events):
    state = {}
    running = None
    invalid = 0
    dispatches = 0
    out = []
    for i, (pid, ev) in enumerate(events, 1):
        if ev not in TRANSITIONS:
            out.append(f"event {i}: unknown event '{ev}'")
            invalid += 1
            continue
        before, after = TRANSITIONS[ev]
        cur = state.get(pid, "NONE")
        if cur != before:
            out.append(f"event {i}: pid {pid} cannot {ev} in state {cur}")
            invalid += 1
            continue
        if ev == "dispatch" and running is not None:
            out.append(f"event {i}: pid {pid} cannot dispatch: CPU busy (running: {running})")
            invalid += 1
            continue
        state[pid] = after
        if ev == "dispatch":
            running = pid
            dispatches += 1
        elif cur == "RUNNING":
            running = None
    out.append("final:")
    for pid in sorted(state):
        out.append(f"{pid} {state[pid]}")
    out.append(f"invalid: {invalid}")
    out.append(f"dispatches: {dispatches}")
    return out


def main():
    data = sys.stdin.read().split("\n")
    n = int(data[0])
    events = []
    for line in data[1:1 + n]:
        pid, ev = line.split()
        events.append((int(pid), ev))
    print("\n".join(simulate(events)))


main()
