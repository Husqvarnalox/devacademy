import sys

lines = [line.strip() for line in sys.stdin.read().splitlines() if line.strip()]
settings = {}
for token in lines[0].split():
    key, _, value = token.partition("=")
    settings[key] = int(value)
cwnd = settings["init"]
ssthresh = settings["ssthresh"]
rwnd = settings["rwnd"]


def phase():
    return "slow-start" if cwnd < ssthresh else "congestion-avoidance"


def report(label, rtt):
    print(f"rtt {rtt}: {label} cwnd={cwnd} ssthresh={ssthresh} phase={phase()} send_window={min(cwnd, rwnd)}")


report("init", 0)
peak = cwnd
for rtt, event in enumerate(lines[1:], start=1):
    if event == "ack":
        if cwnd < ssthresh:
            cwnd = min(cwnd * 2, ssthresh)
        else:
            cwnd += 1
    elif event == "3dupack":
        ssthresh = max(cwnd // 2, 2)
        cwnd = ssthresh
    elif event == "timeout":
        ssthresh = max(cwnd // 2, 2)
        cwnd = 1
    else:
        print(f"rtt {rtt}: {event} unknown event")
        continue
    peak = max(peak, cwnd)
    report(event, rtt)
print(f"max_cwnd={peak}")
