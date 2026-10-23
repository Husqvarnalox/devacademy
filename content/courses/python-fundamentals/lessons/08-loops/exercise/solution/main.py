rows = int(input())
seats = int(input())

count = 0

for i in range(1, rows + 1):
    print(f"Ряд {i}: ", end="")
    for j in range(1, seats + 1):
        print(f"[{j}]", end="")
        count = count + 1
    print()

print(f"Всего мест: {count}")
