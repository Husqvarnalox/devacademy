def clamp(value, low=0, high=100):
    """Ограничивает value диапазоном [low, high]."""
    if value < low:
        return low
    if value > high:
        return high
    return value


def add_item(item, bucket=None):
    """Добавляет item в bucket и возвращает bucket; без bucket создаёт новый список."""
    if bucket is None:
        bucket = []
    bucket.append(item)
    return bucket


def min_max(numbers):
    """Возвращает (минимум, максимум) или None для пустой последовательности."""
    if not numbers:
        return None
    return (min(numbers), max(numbers))


def greet(name, greeting="Привет", *, loud=False):
    """Формирует приветствие; с loud=True - заглавными буквами."""
    text = f"{greeting}, {name}!"
    return text.upper() if loud else text
