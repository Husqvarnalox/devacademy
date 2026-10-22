def average(numbers):
    """Среднее арифметическое чисел из списка; для пустого списка вернуть 0."""
    if len(numbers) == 0:
        return 0
    return sum(numbers) / len(numbers)


def without_duplicates(items):
    """Новый список без повторов; порядок первых вхождений сохраняется. Исходный список не менять."""
    result = []
    for item in items:
        if item not in result:
            result.append(item)
    return result


def top_three(scores):
    """Три наибольших значения по убыванию (если значений меньше трёх - все). Исходный список не менять."""
    return sorted(scores, reverse=True)[:3]


def add_unique(items, item):
    """Добавить item в конец списка items, если его там ещё нет. Вернуть True, если добавили, и False, если нет."""
    if item in items:
        return False
    items.append(item)
    return True
