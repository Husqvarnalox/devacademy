import sys


def fixed(num, den, places):
    """num/den, округлённое half-up до places знаков, строкой (без float)."""
    q = (2 * num * 10**places + den) // (2 * den)
    s = str(q).rjust(places + 1, "0")
    return s[:-places] + "." + s[-places:]


def main():
    order_in, dur, needs, errors = [], {}, {}, []
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        name = p[0]
        if name in dur:
            errors.append(f"ERROR duplicate job: {name}")
            continue
        order_in.append(name)
        dur[name] = int(p[1])
        needs[name] = p[2:]
    for name in order_in:
        for dep in needs[name]:
            if dep not in dur:
                errors.append(f"ERROR unknown dependency: {name} needs {dep}")
    if errors:
        print("\n".join(errors))
        return
    level, remaining = {}, set(order_in)
    stage = 0
    while remaining:
        ready = sorted(j for j in remaining if all(d in level for d in needs[j]))
        if not ready:
            print("ERROR cycle: " + " ".join(sorted(remaining)))
            return
        stage += 1
        for j in ready:
            level[j] = stage
        remaining -= set(ready)
    stages = [sorted(j for j in order_in if level[j] == s) for s in range(1, stage + 1)]
    for i, jobs in enumerate(stages, 1):
        print(f"stage {i}: " + " ".join(jobs))
    print("order: " + " ".join(j for jobs in stages for j in jobs))
    print(f"stages={len(stages)} max_parallel={max(len(s) for s in stages)}")
    finish = {}
    for jobs in stages:  # зависимости всегда в более ранних стадиях
        for j in jobs:
            finish[j] = dur[j] + max((finish[d] for d in needs[j]), default=0)
    total, wall = sum(dur.values()), max(finish.values())
    print(f"sequential_s={total} wall_s={wall} speedup={fixed(total, wall, 2)}")
    cur = min(j for j in finish if finish[j] == wall)
    path = [cur]
    while needs[cur]:
        start = finish[cur] - dur[cur]
        cur = min(d for d in needs[cur] if finish[d] == start)
        path.append(cur)
    print("critical_path=" + ">".join(reversed(path)))


main()
