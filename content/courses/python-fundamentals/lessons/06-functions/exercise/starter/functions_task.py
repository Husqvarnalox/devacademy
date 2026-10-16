def clamp(value, low=0, high=100):
    """Ограничивает value диапазоном [low, high]."""
    # TODO: верните low, если value меньше low; high, если больше high; иначе value
    pass


def add_item(item, bucket=[]):
    """Добавляет item в bucket и возвращает bucket; без bucket создаёт новый список."""
    # TODO: в этом виде функция хранит один общий список между вызовами - исправьте
    bucket.append(item)
    return bucket


def min_max(numbers):
    """Возвращает (минимум, максимум) или None для пустой последовательности."""
    # TODO
    pass


def greet(name, greeting="Привет", loud=False):
    """Формирует приветствие; с loud=True - заглавными буквами."""
    # TODO: сделайте loud только именованным параметром и реализуйте loud=True
    return greeting + ", " + name + "!"
