name = input()
price = float(input())
count = int(input())

total = price * count

print(f"Товар: {name}")
print(f"Сумма: {total:.2f} руб.")
print(f"Длина названия: {len(name)}")
