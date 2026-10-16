class Temperature:
    """Температура с проверкой и пересчётом между шкалами."""

    def __init__(self, celsius):
        self.celsius = celsius

    # TODO: свойства celsius, fahrenheit, classmethod from_fahrenheit, staticmethod is_valid


class Registry:
    """Присваивает экземплярам id по общему счётчику класса."""

    _next_id = 1

    def __init__(self):
        # TODO: в таком виде счётчик заводится у экземпляра, а не у класса
        self.id = self._next_id
        self._next_id += 1

    # TODO: classmethod count() и reset()


class Bag:
    """Сумка с элементами; у каждого экземпляра свой список."""

    items = []

    def add(self, item):
        # TODO: items сейчас общий для всех экземпляров
        self.items.append(item)
        return self


class Point:
    """Неизменяемая по набору атрибутов точка."""

    def __init__(self, x, y):
        self.x = x
        self.y = y

    # TODO: __slots__ и метод moved(dx, dy)
