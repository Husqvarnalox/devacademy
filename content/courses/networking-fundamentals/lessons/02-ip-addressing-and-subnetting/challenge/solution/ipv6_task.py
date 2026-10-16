import sys

HEX = set("0123456789abcdefABCDEF")


def parse_ipv4_tail(text):
    parts = text.split(".")
    if len(parts) != 4:
        return None
    octets = []
    for part in parts:
        if not part.isascii() or not part.isdigit() or len(part) > 3 or (len(part) > 1 and part[0] == "0"):
            return None
        if int(part) > 255:
            return None
        octets.append(int(part))
    return [(octets[0] << 8) | octets[1], (octets[2] << 8) | octets[3]]


def parse_groups(text):
    """Список групп; None, если запись некорректна."""
    if not text:
        return []
    groups = []
    parts = text.split(":")
    for index, part in enumerate(parts):
        if "." in part:
            if index != len(parts) - 1:
                return None
            tail = parse_ipv4_tail(part)
            if tail is None:
                return None
            groups.extend(tail)
        else:
            if not 1 <= len(part) <= 4 or any(ch not in HEX for ch in part):
                return None
            groups.append(int(part, 16))
    return groups


def parse_ipv6(text):
    if text.count("::") > 1 or ":::" in text:
        return None
    if "::" in text:
        left, _, right = text.partition("::")
        head = parse_groups(left)
        tail = parse_groups(right)
        if head is None or tail is None:
            return None
        missing = 8 - len(head) - len(tail)
        if missing < 1:
            return None
        return head + [0] * missing + tail
    groups = parse_groups(text)
    if groups is None or len(groups) != 8:
        return None
    return groups


def compress(groups):
    best_start, best_len = -1, 0
    index = 0
    while index < 8:
        if groups[index] == 0:
            start = index
            while index < 8 and groups[index] == 0:
                index += 1
            if index - start > best_len:
                best_start, best_len = start, index - start
        else:
            index += 1
    words = [format(g, "x") for g in groups]
    if best_len < 2:
        return ":".join(words)
    return ":".join(words[:best_start]) + "::" + ":".join(words[best_start + best_len:])


for raw in sys.stdin.read().splitlines():
    line = raw.strip()
    if not line:
        continue
    groups = parse_ipv6(line)
    if groups is None:
        print(f"{line} -> invalid")
    else:
        print(f"{':'.join(format(g, '04x') for g in groups)} {compress(groups)}")
