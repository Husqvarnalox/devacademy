import sys

# Вход: первая строка "init=1 ssthresh=16 rwnd=32" (в MSS), затем по одному событию на RTT:
# ack, 3dupack или timeout.
lines = [line.strip() for line in sys.stdin.read().splitlines() if line.strip()]
settings = {}
for token in lines[0].split():
    key, _, value = token.partition("=")
    settings[key] = int(value)

# TODO: смоделируйте Reno по RTT: slow start, congestion avoidance, fast retransmit/recovery и timeout,
# выведите трассу cwnd по условию задачи.
print("not implemented")
