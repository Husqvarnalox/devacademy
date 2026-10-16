n = int(input())
steps = 0
highest = n
while n != 1:
    if n % 2 == 0:
        n //= 2
    else:
        n = 3 * n + 1
    steps += 1
    if n > highest:
        highest = n
print(steps, highest)
