import sys

# Рёбра диаграммы RFC 9293, рисунок 5 (раздел 3.3.2) и примечание 2 к ней.
TRANSITIONS = {
    ("CLOSED", "passive_open"): ("LISTEN", "-"),
    ("CLOSED", "active_open"): ("SYN_SENT", "SYN"),
    ("LISTEN", "close"): ("CLOSED", "-"),
    ("LISTEN", "rcv_syn"): ("SYN_RCVD", "SYN+ACK"),
    ("LISTEN", "send"): ("SYN_SENT", "SYN"),
    ("SYN_SENT", "close"): ("CLOSED", "-"),
    ("SYN_SENT", "rcv_syn"): ("SYN_RCVD", "SYN+ACK"),
    ("SYN_SENT", "rcv_synack"): ("ESTABLISHED", "ACK"),
    ("SYN_RCVD", "rcv_ack"): ("ESTABLISHED", "-"),
    ("SYN_RCVD", "close"): ("FIN_WAIT_1", "FIN"),
    ("ESTABLISHED", "close"): ("FIN_WAIT_1", "FIN"),
    ("ESTABLISHED", "rcv_fin"): ("CLOSE_WAIT", "ACK"),
    ("FIN_WAIT_1", "rcv_ack"): ("FIN_WAIT_2", "-"),
    ("FIN_WAIT_1", "rcv_fin"): ("CLOSING", "ACK"),
    ("FIN_WAIT_1", "rcv_finack"): ("TIME_WAIT", "ACK"),
    ("FIN_WAIT_2", "rcv_fin"): ("TIME_WAIT", "ACK"),
    ("CLOSING", "rcv_ack"): ("TIME_WAIT", "-"),
    ("CLOSE_WAIT", "close"): ("LAST_ACK", "FIN"),
    ("LAST_ACK", "rcv_ack"): ("CLOSED", "-"),
    ("TIME_WAIT", "timeout"): ("CLOSED", "-"),
}
KNOWN_EVENTS = {event for _, event in TRANSITIONS} | {"rcv_rst"}

state = "CLOSED"
passive = False  # SYN_RCVD достигнут из LISTEN (пассивное открытие)
for raw in sys.stdin.read().splitlines():
    event = raw.strip()
    if not event:
        continue
    if event not in KNOWN_EVENTS:
        print(f"{event}: unknown event")
        continue
    if event == "rcv_rst":
        if state in ("CLOSED", "LISTEN"):
            print(f"{event}: {state} ignored")
            continue
        new_state = "LISTEN" if state == "SYN_RCVD" and passive else "CLOSED"
        segment = "-"
    elif (state, event) in TRANSITIONS:
        new_state, segment = TRANSITIONS[(state, event)]
    else:
        print(f"{event}: {state} ignored")
        continue
    print(f"{event}: {state} -> {new_state} send={segment}")
    if new_state == "SYN_RCVD":
        passive = state == "LISTEN"
    state = new_state
print(f"final={state}")
