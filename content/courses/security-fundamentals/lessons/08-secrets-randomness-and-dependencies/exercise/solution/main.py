import re
import sys

URL_USERINFO = re.compile(r"([A-Za-z][A-Za-z0-9+.-]*://)([^\s:/@]+):([^\s/@]+)@")
BEARER = re.compile(r"\b[Bb][Ee][Aa][Rr][Ee][Rr]\s+([A-Za-z0-9._~+/=-]{8,})")
KEY_SHAPES = re.compile(
    r"AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{36,}|xox[abprs]-[A-Za-z0-9-]{10,}"
)
FIELD = re.compile(
    r"""([\w-]*(?:password|passwd|pwd|secret|token|api[_-]?key))(["']?\s*[:=]\s*)("[^"]*"|'[^']*'|[^\s,;&"']+)""",
    re.IGNORECASE,
)
MASK = "[REDACTED]"


def bearer_sub(m):
    if any(not c.isalpha() for c in m.group(1)):
        return "Bearer " + MASK
    return m.group(0)


def field_sub(m):
    value = m.group(3)
    if value[0] in "\"'":
        value = value[0] + MASK + value[0]
    else:
        value = MASK
    return m.group(1) + m.group(2) + value


def redact(line):
    line = URL_USERINFO.sub(lambda m: m.group(1) + m.group(2) + ":" + MASK + "@", line)
    line = BEARER.sub(bearer_sub, line)
    line = KEY_SHAPES.sub(MASK, line)
    line = FIELD.sub(field_sub, line)
    return line


for line in sys.stdin.read().splitlines():
    print(redact(line))
