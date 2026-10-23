def collatz_steps(n):
    """Число шагов, за которое n превращается в 1."""
    steps = 0
    while n != 1:
        if n % 2 == 0:
            n = n // 2
        else:
            n = 3 * n + 1
        steps += 1
    return steps


def digit_sum(n):
    """Сумма десятичных цифр неотрицательного целого n."""
    total = 0
    while n > 0:
        total += n % 10
        n //= 10
    return total


def is_leap(year):
    """True для високосного года."""
    return year % 4 == 0 and (year % 100 != 0 or year % 400 == 0)
