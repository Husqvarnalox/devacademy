import re
import sys

TRUE = {"true", "1", "yes", "on"}
FALSE = {"false", "0", "no", "off"}


def redact_url(value):
    if "://" not in value:
        return value
    scheme, rest = value.split("://", 1)
    end = len(rest)
    for ch in "/?#":
        i = rest.find(ch)
        if i != -1:
            end = min(end, i)
    authority, tail = rest[:end], rest[end:]
    userinfo, at, hostport = authority.rpartition("@")
    if at and ":" in userinfo:
        user = userinfo.split(":", 1)[0]
        authority = f"{user}:***@{hostport}"
    return f"{scheme}://{authority}{tail}"


def main():
    types, values, sources, errors = {}, {}, {}, []
    entries, section = [], None
    for raw in sys.stdin.read().splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1].lower()
            continue
        entries.append((section, line))
    for section, line in entries:  # сначала все объявления: ключи известны независимо от порядка секций
        if section == "defaults":
            decl, eq, default = line.partition("=")
            key, _, typ = decl.strip().partition(":")
            types[key] = typ
            if eq:
                values[key], sources[key] = default.strip(), "default"
    layers = {"file": {}, "env": {}, "flags": {}}
    for section, line in entries:
        if section == "flags":
            if not line.startswith("--"):
                continue
            name, eq, val = line[2:].partition("=")
            key = name.replace("-", "_")
            if key not in types:
                errors.append(f"ERROR flags: unknown key '{key}'")
                continue
            if not eq:
                if types[key] != "bool":
                    errors.append(f"ERROR flags: '{key}' needs a value")
                    continue
                val = "true"
            layers["flags"][key] = val.strip()
        elif section == "env":
            name, _, val = line.partition("=")
            name = name.strip()
            if not name.startswith("APP_"):
                continue
            key = name[4:].lower()
            if key not in types:
                errors.append(f"ERROR env: unknown key '{key}'")
                continue
            layers["env"][key] = val.strip()
        elif section == "file":
            name, _, val = line.partition("=")
            key = name.strip()
            if key not in types:
                errors.append(f"ERROR file: unknown key '{key}'")
                continue
            layers["file"][key] = val.strip()
    for source in ("file", "env", "flags"):  # от слабого к сильному
        for key, val in layers[source].items():
            values[key], sources[key] = val, source
    shown = {}
    for key in sorted(types):
        if key not in values:
            errors.append(f"ERROR {key}: missing required value")
            continue
        typ, raw = types[key], values[key]
        if typ == "int":
            if not re.fullmatch(r"[+-]?\d+", raw):
                errors.append(f"ERROR {key}: invalid int '{raw}'")
                continue
            shown[key] = str(int(raw))
        elif typ == "bool":
            if raw.lower() in TRUE:
                shown[key] = "true"
            elif raw.lower() in FALSE:
                shown[key] = "false"
            else:
                errors.append(f"ERROR {key}: invalid bool '{raw}'")
                continue
        elif typ == "secret":
            shown[key] = "***" if raw else ""
        elif typ == "url":
            shown[key] = redact_url(raw)
        else:
            shown[key] = raw
    if errors:
        print("\n".join(errors))
        print(f"errors={len(errors)}")
        return
    for key in sorted(shown):
        print(f"{key}={shown[key]} ({sources[key]})")
    print(f"keys={len(shown)}")


main()
