import sys

# Вход: по одной записи a.b.c.d/N на строку. Пустые строки пропускаются.
for raw in sys.stdin.read().splitlines():
    line = raw.strip()
    if not line:
        continue
    # TODO: разберите адрес и длину префикса, постройте маску и посчитайте
    # network, broadcast, wildcard, first, last, hosts и category целочисленными битовыми операциями.
    print(line + " -> not implemented")
