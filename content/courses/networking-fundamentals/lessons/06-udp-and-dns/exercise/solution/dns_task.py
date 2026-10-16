import sys

RCODES = {0: "NOERROR", 1: "FORMERR", 2: "SERVFAIL", 3: "NXDOMAIN", 4: "NOTIMP", 5: "REFUSED"}
TYPES = {1: "A", 2: "NS", 5: "CNAME", 28: "AAAA"}
CLASSES = {1: "IN"}


class DnsError(Exception):
    pass


def read_name(data, pos):
    """Возвращает (имя, позиция после имени в исходном потоке)."""
    labels = []
    end = None
    seen = 0
    while True:
        if pos >= len(data):
            raise DnsError("truncated")
        length = data[pos]
        if length & 0xC0 == 0xC0:
            if pos + 1 >= len(data):
                raise DnsError("truncated")
            target = ((length & 0x3F) << 8) | data[pos + 1]
            if end is None:
                end = pos + 2
            if target >= pos:
                raise DnsError("bad pointer")
            seen += 1
            if seen > len(data):
                raise DnsError("bad pointer")
            pos = target
        elif length & 0xC0:
            raise DnsError("bad label")
        elif length == 0:
            if end is None:
                end = pos + 1
            return (".".join(labels) if labels else "."), end
        else:
            if pos + 1 + length > len(data):
                raise DnsError("truncated")
            labels.append(data[pos + 1:pos + 1 + length].decode("ascii", "replace"))
            pos += 1 + length


def u16(data, pos):
    if pos + 2 > len(data):
        raise DnsError("truncated")
    return (data[pos] << 8) | data[pos + 1]


def u32(data, pos):
    if pos + 4 > len(data):
        raise DnsError("truncated")
    return int.from_bytes(data[pos:pos + 4], "big")


def decode(data):
    if len(data) < 12:
        raise DnsError("truncated")
    ident = u16(data, 0)
    flags = u16(data, 2)
    qd, an, ns, ar = (u16(data, 4), u16(data, 6), u16(data, 8), u16(data, 10))
    out = [
        f"id=0x{ident:04x} qr={flags >> 15} opcode={(flags >> 11) & 15} aa={(flags >> 10) & 1} tc={(flags >> 9) & 1}"
        f" rd={(flags >> 8) & 1} ra={(flags >> 7) & 1} rcode={flags & 15} ({RCODES.get(flags & 15, 'other')})",
        f"questions={qd} answers={an} authority={ns} additional={ar}",
    ]
    pos = 12
    for _ in range(qd):
        name, pos = read_name(data, pos)
        qtype, qclass = u16(data, pos), u16(data, pos + 2)
        pos += 4
        out.append(f"Q {name} {TYPES.get(qtype, 'TYPE' + str(qtype))} {CLASSES.get(qclass, 'CLASS' + str(qclass))}")
    for _ in range(an):
        name, pos = read_name(data, pos)
        rtype, rclass, ttl, rdlen = u16(data, pos), u16(data, pos + 2), u32(data, pos + 4), u16(data, pos + 8)
        pos += 10
        if pos + rdlen > len(data):
            raise DnsError("truncated")
        rdata = data[pos:pos + rdlen]
        if rtype == 1 and rdlen == 4:
            shown = ".".join(str(b) for b in rdata)
        elif rtype == 28 and rdlen == 16:
            shown = ":".join(format((rdata[i] << 8) | rdata[i + 1], "x") for i in range(0, 16, 2))
        elif rtype in (2, 5):
            shown, _ = read_name(data, pos)
        else:
            shown = rdata.hex()
        pos += rdlen
        out.append(
            f"AN {name} {TYPES.get(rtype, 'TYPE' + str(rtype))} {CLASSES.get(rclass, 'CLASS' + str(rclass))} ttl={ttl} {shown}"
        )
    return out


def main():
    try:
        data = bytes.fromhex("".join(sys.stdin.read().split()))
    except ValueError:
        print("error: bad hex")
        return
    try:
        print("\n".join(decode(data)))
    except DnsError as error:
        print(f"error: {error}")


main()
