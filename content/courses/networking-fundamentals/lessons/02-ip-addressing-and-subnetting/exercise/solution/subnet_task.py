import sys


def parse_ipv4(text):
    parts = text.split(".")
    if len(parts) != 4:
        return None
    value = 0
    for part in parts:
        if not part.isascii() or not part.isdigit() or len(part) > 3:
            return None
        if len(part) > 1 and part[0] == "0":
            return None
        octet = int(part)
        if octet > 255:
            return None
        value = (value << 8) | octet
    return value


def fmt(value):
    return ".".join(str((value >> shift) & 255) for shift in (24, 16, 8, 0))


def make_mask(prefix):
    return (0xFFFFFFFF << (32 - prefix)) & 0xFFFFFFFF


CATEGORIES = [
    ("0.0.0.0", 8, "this-network"),
    ("10.0.0.0", 8, "private"),
    ("100.64.0.0", 10, "shared"),
    ("127.0.0.0", 8, "loopback"),
    ("169.254.0.0", 16, "link-local"),
    ("172.16.0.0", 12, "private"),
    ("192.0.2.0", 24, "documentation"),
    ("192.168.0.0", 16, "private"),
    ("198.51.100.0", 24, "documentation"),
    ("203.0.113.0", 24, "documentation"),
    ("224.0.0.0", 4, "multicast"),
    ("240.0.0.0", 4, "reserved"),
]


def category(address):
    for base, prefix, name in CATEGORIES:
        if address & make_mask(prefix) == parse_ipv4(base):
            return name
    return "public"


def describe(line):
    address_text, slash, prefix_text = line.partition("/")
    address = parse_ipv4(address_text)
    if not slash or address is None or not prefix_text.isascii() or not prefix_text.isdigit():
        return None
    prefix = int(prefix_text)
    if prefix > 32:
        return None
    mask = make_mask(prefix)
    network = address & mask
    broadcast = network | (~mask & 0xFFFFFFFF)
    if prefix == 32:
        hosts, first, last = 1, network, network
    elif prefix == 31:
        hosts, first, last = 2, network, broadcast
    else:
        hosts, first, last = (1 << (32 - prefix)) - 2, network + 1, broadcast - 1
    return [
        f"{line.strip()}",
        f"  network={fmt(network)} broadcast={fmt(broadcast)} mask={fmt(mask)} wildcard={fmt(~mask & 0xFFFFFFFF)}",
        f"  first={fmt(first)} last={fmt(last)} hosts={hosts} category={category(address)}",
    ]


for raw in sys.stdin.read().splitlines():
    line = raw.strip()
    if not line:
        continue
    result = describe(line)
    if result is None:
        print(f"{line} -> invalid")
    else:
        print("\n".join(result))
