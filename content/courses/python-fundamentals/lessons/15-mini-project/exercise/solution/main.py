def parse_line(line):
    parts = line.split()
    if len(parts) != 2:
        return None
    try:
        minutes = int(parts[1])
    except ValueError:
        return None
    if minutes <= 0:
        return None
    return parts[0], minutes


totals = {}
count = 0
number = 0
while True:
    line = input().strip()
    if line == "конец":
        break
    number += 1
    entry = parse_line(line)
    if entry is None:
        print(f"Пропущена строка {number}: {line}")
        continue
    name, minutes = entry
    totals[name] = totals.get(name, 0) + minutes
    count += 1

if count == 0:
    print("Занятий нет")
else:
    print(f"Занятий: {count}")
    print(f"Всего минут: {sum(totals.values())}")
    best = None
    for name in sorted(totals):
        print(f"{name}: {totals[name]}")
        if best is None or totals[name] > totals[best]:
            best = name
    print(f"Больше всего времени: {best}")
