class Temperature:
    """Температура с проверкой и пересчётом между шкалами."""

    def __init__(self, celsius):
        self.celsius = celsius

    @property
    def celsius(self):
        return self._celsius

    @celsius.setter
    def celsius(self, value):
        if value < -273.15:
            raise ValueError("температура ниже абсолютного нуля")
        self._celsius = value

    @property
    def fahrenheit(self):
        return self._celsius * 9 / 5 + 32

    @fahrenheit.setter
    def fahrenheit(self, value):
        self.celsius = (value - 32) * 5 / 9

    @classmethod
    def from_fahrenheit(cls, f):
        return cls((f - 32) * 5 / 9)

    @staticmethod
    def is_valid(c):
        return c >= -273.15


class Registry:
    """Присваивает экземплярам id по общему счётчику класса."""

    _next_id = 1

    def __init__(self):
        self.id = Registry._next_id
        Registry._next_id += 1

    @classmethod
    def count(cls):
        return Registry._next_id - 1

    @classmethod
    def reset(cls):
        Registry._next_id = 1


class Bag:
    """Сумка с элементами; у каждого экземпляра свой список."""

    def __init__(self):
        self.items = []

    def add(self, item):
        self.items.append(item)
        return self


class Point:
    """Неизменяемая по набору атрибутов точка."""

    __slots__ = ("x", "y")

    def __init__(self, x, y):
        self.x = x
        self.y = y

    def moved(self, dx, dy):
        return Point(self.x + dx, self.y + dy)
