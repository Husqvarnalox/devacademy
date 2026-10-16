import sys


def parse_ipv4(text):
    parts = text.split(".")
    if len(parts) != 4:
        return None
    value = 0
    for part in parts:
        if not part.isascii() or not part.isdigit() or len(part) > 3 or (len(part) > 1 and part[0] == "0"):
            return None
        octet = int(part)
        if octet > 255:
            return None
        value = (value << 8) | octet
    return value


def fmt(value):
    return ".".join(str((value >> shift) & 255) for shift in (24, 16, 8, 0))


def parse_route(line):
    tokens = line.split()
    prefix_text, slash, length_text = tokens[0].partition("/")
    address = parse_ipv4(prefix_text)
    length = int(length_text)
    mask = (0xFFFFFFFF << (32 - length)) & 0xFFFFFFFF
    route = {"network": address & mask, "mask": mask, "length": length, "via": None, "dev": None, "metric": 0}
    index = 1
    while index < len(tokens):
        key, value = tokens[index], tokens[index + 1]
        if key == "via":
            route["via"] = parse_ipv4(value)
        elif key == "dev":
            route["dev"] = value
        elif key == "metric":
            route["metric"] = int(value)
        index += 2
    return route


def lookup(routes, destination):
    best = None
    for route in routes:
        if destination & route["mask"] != route["network"]:
            continue
        if best is None:
            best = route
        elif route["length"] > best["length"]:
            best = route
        elif route["length"] == best["length"] and route["metric"] < best["metric"]:
            best = route
    return best


lines = sys.stdin.read().splitlines()
count = int(lines[0])
routes = [parse_route(line) for line in lines[1:1 + count]]
for raw in lines[1 + count:]:
    text = raw.strip()
    if not text:
        continue
    destination = parse_ipv4(text)
    if destination is None:
        print(f"{text} -> invalid")
        continue
    route = lookup(routes, destination)
    if route is None:
        print(f"{text} -> unreachable")
        continue
    matched = f"{fmt(route['network'])}/{route['length']}"
    if route["via"] is None:
        print(f"{text} -> dev {route['dev']} direct arp={text} route={matched}")
    else:
        print(f"{text} -> dev {route['dev']} via {fmt(route['via'])} arp={fmt(route['via'])} route={matched}")
