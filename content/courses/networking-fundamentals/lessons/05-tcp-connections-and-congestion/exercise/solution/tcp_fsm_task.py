import sys

TRANSITIONS = {
    ("CLOSED", "passive_open"): ("LISTEN", "-"),
    ("CLOSED", "active_open"): ("SYN_SENT", "SYN"),
    ("LISTEN", "rcv_syn"): ("SYN_RCVD", "SYN+ACK"),
    ("LISTEN", "active_open"): ("SYN_SENT", "SYN"),
    ("LISTEN", "close"): ("CLOSED", "-"),
    ("SYN_SENT", "rcv_synack"): ("ESTABLISHED", "ACK"),
    ("SYN_SENT", "rcv_syn"): ("SYN_RCVD", "SYN+ACK"),
    ("SYN_SENT", "close"): ("CLOSED", "-"),
    ("SYN_RCVD", "rcv_ack"): ("ESTABLISHED", "-"),
    ("SYN_RCVD", "close"): ("FIN_WAIT_1", "FIN"),
    ("ESTABLISHED", "close"): ("FIN_WAIT_1", "FIN"),
    ("ESTABLISHED", "rcv_fin"): ("CLOSE_WAIT", "ACK"),
    ("FIN_WAIT_1", "rcv_ack"): ("FIN_WAIT_2", "-"),
    ("FIN_WAIT_1", "rcv_fin"): ("CLOSING", "ACK"),
    ("FIN_WAIT_1", "rcv_finack"): ("TIME_WAIT", "ACK"),
    ("FIN_WAIT_2", "rcv_fin"): ("TIME_WAIT", "ACK"),
    ("CLOSING", "rcv_ack"): ("TIME_WAIT", "-"),
    ("TIME_WAIT", "timeout"): ("CLOSED", "-"),
    ("CLOSE_WAIT", "close"): ("LAST_ACK", "FIN"),
    ("LAST_ACK", "rcv_ack"): ("CLOSED", "-"),
}
KNOWN_EVENTS = {event for _, event in TRANSITIONS} | {"rcv_rst"}

state = "CLOSED"
for raw in sys.stdin.read().splitlines():
    event = raw.strip()
    if not event:
        continue
    if event not in KNOWN_EVENTS:
        print(f"{event}: unknown event")
    elif event == "rcv_rst" and state not in ("CLOSED", "LISTEN"):
        print(f"{event}: {state} -> CLOSED send=-")
        state = "CLOSED"
    elif (state, event) in TRANSITIONS:
        new_state, segment = TRANSITIONS[(state, event)]
        print(f"{event}: {state} -> {new_state} send={segment}")
        state = new_state
    else:
        print(f"{event}: {state} ignored")
print(f"final={state}")
