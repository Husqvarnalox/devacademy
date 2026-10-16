from collections import Counter, defaultdict, deque, namedtuple

Entry = namedtuple("Entry", ["date", "level", "service", "message"])


def parse(line):
    parts = line.split(maxsplit=3)
    if len(parts) < 3:
        raise ValueError(f"слишком мало полей: {line!r}")
    if len(parts) == 3:
        parts.append("")
    return Entry(*parts)


def count_levels(lines):
    return Counter(parse(line).level for line in lines)


def top_error_services(lines, n):
    entries = (parse(line) for line in lines)
    return Counter(e.service for e in entries if e.level == "ERROR").most_common(n)


def messages_by_service(lines):
    groups = defaultdict(list)
    for line in lines:
        e = parse(line)
        groups[e.service].append(e.message)
    return dict(groups)


def last_lines(lines, n):
    return list(deque(lines, maxlen=n))
