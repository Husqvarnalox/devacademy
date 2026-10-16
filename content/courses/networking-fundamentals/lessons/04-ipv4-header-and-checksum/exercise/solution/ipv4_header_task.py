import sys

PROTOCOLS = {1: "ICMP", 2: "IGMP", 6: "TCP", 17: "UDP", 41: "IPv6", 47: "GRE", 50: "ESP", 89: "OSPF"}


def internet_checksum(data):
    if len(data) % 2:
        data += b"\x00"
    total = 0
    for i in range(0, len(data), 2):
        total += (data[i] << 8) | data[i + 1]
    while total >> 16:
        total = (total & 0xFFFF) + (total >> 16)
    return ~total & 0xFFFF


def fmt(raw):
    return ".".join(str(b) for b in raw)


def main():
    text = "".join(sys.stdin.read().split())
    try:
        data = bytes.fromhex(text)
    except ValueError:
        print("error: bad hex")
        return
    if len(data) < 20:
        print("error: truncated header")
        return
    version = data[0] >> 4
    ihl = data[0] & 0x0F
    if version != 4:
        print("error: not IPv4")
        return
    if ihl < 5:
        print("error: bad ihl")
        return
    header_len = ihl * 4
    if len(data) < header_len:
        print("error: truncated header")
        return
    header = data[:header_len]
    dscp = header[1] >> 2
    ecn = header[1] & 3
    total_length = (header[2] << 8) | header[3]
    identification = (header[4] << 8) | header[5]
    flags = header[6] >> 5
    offset = ((header[6] & 0x1F) << 8) | header[7]
    ttl = header[8]
    protocol = header[9]
    checksum = (header[10] << 8) | header[11]
    zeroed = header[:10] + b"\x00\x00" + header[12:]
    expected = internet_checksum(zeroed)
    names = []
    if flags & 0b010:
        names.append("DF")
    if flags & 0b001:
        names.append("MF")
    print(f"version={version}")
    print(f"ihl={ihl} ({header_len} bytes)")
    print(f"dscp={dscp} ecn={ecn}")
    print(f"total_length={total_length}")
    print(f"identification=0x{identification:04x}")
    print(f"flags={'+'.join(names) if names else 'none'}")
    print(f"fragment_offset={offset}")
    print(f"ttl={ttl}")
    print(f"protocol={protocol} ({PROTOCOLS.get(protocol, 'other')})")
    print(f"header_checksum=0x{checksum:04x}")
    if checksum == expected:
        print("checksum=valid")
    else:
        print(f"checksum=invalid (expected 0x{expected:04x})")
    print(f"src={fmt(header[12:16])}")
    print(f"dst={fmt(header[16:20])}")
    print(f"options={header_len - 20} bytes")


main()
