import sys


def ignored(path, patterns):
    for p in patterns:
        if p.startswith("**/*."):
            if path.endswith(p[4:]):
                return True
        elif p.startswith("*."):
            if "/" not in path and path.endswith(p[1:]):
                return True
        else:
            base = p.rstrip("/")
            if path == base or path.startswith(base + "/"):
                return True
    return False


def hits(source, path):
    if source in (".", "./"):
        return True
    base = source.rstrip("/")
    return path == base or path.startswith(base + "/")


def parse_copy(text):
    parts = text.split()
    from_stage, args = None, []
    for tok in parts[1:]:
        if tok.startswith("--from="):
            from_stage = tok[len("--from="):]
        elif not tok.startswith("--"):
            args.append(tok)
    return from_stage, args[:-1]


def main():
    lines = sys.stdin.read().splitlines()
    cut = lines.index("---")
    instr = [l.strip() for l in lines[:cut] if l.strip() and not l.strip().startswith("#")]
    patterns, built, edited = [], False, set()
    build_no = 0
    for raw in lines[cut + 1:]:
        p = raw.split()
        if not p:
            continue
        if p[0] == "IGNORE":
            patterns += p[1:]
        elif p[0] == "EDIT":
            k = int(p[1])
            instr[k - 1] = raw.split(None, 2)[2]
            edited.add(k - 1)
        elif p[0] == "BUILD":
            build_no += 1
            changed = [f for f in p[1:] if not ignored(f, patterns)]
            print(f"build {build_no}")
            print("context=" + (",".join(changed) or "-"))
            stage_names, stage_idx, rebuilt_in_stage = {}, -1, {}
            dirty, total, layers = False, 0, 0
            for i, text in enumerate(instr):
                word = text.split()[0].upper()
                if word == "FROM":
                    stage_idx += 1
                    rebuilt_in_stage[stage_idx] = False
                    toks = text.split()
                    if len(toks) >= 4 and toks[2].upper() == "AS":
                        stage_names[toks[3].lower()] = stage_idx
                    dirty = False
                rebuild = (not built) or (i in edited) or dirty
                if word in ("COPY", "ADD") and not rebuild:
                    src_stage, sources = parse_copy(text)
                    if src_stage is not None:
                        idx = stage_names.get(src_stage.lower(), int(src_stage) if src_stage.isdigit() else None)
                        rebuild = idx is not None and rebuilt_in_stage.get(idx, False)
                    else:
                        rebuild = any(hits(s, f) for s in sources for f in changed)
                if rebuild:
                    dirty = True
                    rebuilt_in_stage[stage_idx] = True
                    total += 1
                    if word in ("RUN", "COPY", "ADD"):
                        layers += 1
                print(f"{i + 1} {'REBUILT' if rebuild else 'CACHED'} {text}")
            print(f"rebuilt={total}/{len(instr)} layers={layers}")
            built = True
            edited = set()


main()
