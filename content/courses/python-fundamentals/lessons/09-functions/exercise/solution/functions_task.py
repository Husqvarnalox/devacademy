def square(n):
    """Вернуть квадрат числа n."""
    return n * n


def discount(price, percent=10):
    """Вернуть цену после скидки percent процентов (по умолчанию 10)."""
    return price * (100 - percent) / 100


def grade(points):
    """Вернуть оценку словом по числу баллов."""
    if points >= 85:
        return "отлично"
    elif points >= 70:
        return "хорошо"
    elif points >= 50:
        return "удовлетворительно"
    else:
        return "неудовлетворительно"


def sum_to(n):
    """Вернуть сумму целых чисел от 1 до n; для n < 1 вернуть 0."""
    total = 0
    for i in range(1, n + 1):
        total = total + i
    return total
