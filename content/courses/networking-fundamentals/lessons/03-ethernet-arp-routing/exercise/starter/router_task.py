import sys

# Вход: первая строка - число маршрутов N, затем N строк вида
#   10.0.0.0/8 via 192.168.1.1 dev eth0 [metric 5]
#   192.168.1.0/24 dev eth0
# а дальше по одному адресу назначения на строку.
lines = sys.stdin.read().splitlines()
count = int(lines[0])
routes = lines[1:1 + count]
destinations = [line.strip() for line in lines[1 + count:] if line.strip()]

# TODO: для каждого адреса найдите маршрут с самым длинным префиксом (при равенстве - с меньшей metric)
# и выведите строку по условию задачи.
for destination in destinations:
    print(destination + " -> unreachable")
