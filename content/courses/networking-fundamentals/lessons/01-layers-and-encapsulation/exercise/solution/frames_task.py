import sys


def main():
    fields = {}
    for token in sys.stdin.read().split():
        key, _, value = token.partition("=")
        fields[key] = int(value)
    payload = fields["payload"]
    transport = fields["transport"]
    network = fields["network"]
    link = fields["link"]
    mtu = fields["mtu"]
    df = fields.get("df", 0)

    ip_payload = payload + transport
    per_fragment = max(0, (mtu - network) // 8 * 8)
    print(f"ip_payload={ip_payload}")
    fits = ip_payload <= mtu - network
    if not fits:
        if per_fragment < 8:
            print("error: mtu too small")
            return
        if df:
            print("dropped: fragmentation needed but DF set")
            return
    print(f"max_data_per_fragment={per_fragment}")

    count = 1 if fits else -(-ip_payload // per_fragment)
    print(f"fragments={count}")
    wire_total = 0
    offset_bytes = 0
    remaining = ip_payload
    for index in range(1, count + 1):
        data = remaining if fits else min(remaining, per_fragment)
        remaining -= data
        more = 1 if remaining > 0 else 0
        ip_total = network + data
        frame = ip_total + link
        wire_total += frame
        print(f"fragment {index}: offset={offset_bytes // 8} data={data} ip_total={ip_total} frame={frame} mf={more}")
        offset_bytes += data
    print(f"total_on_wire={wire_total}")
    print(f"efficiency={100 * payload / wire_total:.2f}%")


main()
