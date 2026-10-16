import sys

# event -> (state before, state after). A pid that was never created is in state NONE.
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
    """events: list of (pid, event_name). Return the list of output lines."""
    # TODO: keep a dict pid -> state and the pid that is currently RUNNING.
    # For every event print a line only when it is invalid (see the task text),
    # then the "final:" block, "invalid: K" and "dispatches: D".
    return []


def main():
    data = sys.stdin.read().split("\n")
    n = int(data[0])
    events = []
    for line in data[1:1 + n]:
        pid, ev = line.split()
        events.append((int(pid), ev))
    print("\n".join(simulate(events)))


main()
