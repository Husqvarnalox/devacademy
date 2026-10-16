import sys

# Вход: по одному событию на строку. Автомат начинает в состоянии CLOSED.
state = "CLOSED"
for raw in sys.stdin.read().splitlines():
    event = raw.strip()
    if not event:
        continue
    # TODO: найдите переход по паре (состояние, событие), выведите строку по условию
    # и обновите состояние. Недопустимые и неизвестные события не меняют состояние.
    print(f"{event}: {state} ignored")
print(f"final={state}")
