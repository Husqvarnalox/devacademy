count = 0
total = 0

while True:
    line = input()
    if line == "итог":
        break
    price = int(line)
    if price <= 0:
        continue
    count = count + 1
    total = total + price

print("Позиций:", count)
print("Сумма:", total)
