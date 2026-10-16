import math
from abc import ABC, abstractmethod
from typing import Protocol, runtime_checkable


class Base:
    def handle(self):
        return ["Base"]


class LoggingMixin(Base):
    def handle(self):
        return ["Logging"] + super().handle()


class ValidationMixin(Base):
    def handle(self):
        return ["Validation"] + super().handle()


class Service(LoggingMixin, ValidationMixin):
    pass


class Shape(ABC):
    @abstractmethod
    def area(self):
        """Площадь фигуры."""

    def describe(self):
        return f"{type(self).__name__} area={self.area():.2f}"


class Circle(Shape):
    def __init__(self, r):
        self.r = r

    def area(self):
        return math.pi * self.r * self.r


class Rect(Shape):
    def __init__(self, w, h):
        self.w, self.h = w, h

    def area(self):
        return self.w * self.h


@runtime_checkable
class Speaker(Protocol):
    def speak(self) -> str:
        ...


def make_chorus(objs):
    """Результаты speak() у объектов, удовлетворяющих Speaker; остальные пропускаются."""
    return [o.speak() for o in objs if isinstance(o, Speaker)]


class Stack:
    def __init__(self):
        self._items = []

    def push(self, item):
        self._items.append(item)

    def pop(self):
        if not self._items:
            raise IndexError("pop from empty stack")
        return self._items.pop()

    def peek(self):
        if not self._items:
            raise IndexError("peek from empty stack")
        return self._items[-1]

    def __len__(self):
        return len(self._items)
