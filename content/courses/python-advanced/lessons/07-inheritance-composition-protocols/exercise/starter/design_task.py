import math
from abc import ABC, abstractmethod
from typing import Protocol, runtime_checkable


class Base:
    def handle(self):
        return ["Base"]


class LoggingMixin(Base):
    def handle(self):
        # TODO: вернуть ["Logging"] + продолжение цепочки через super()
        return ["Logging"]


class ValidationMixin(Base):
    def handle(self):
        # TODO: вернуть ["Validation"] + продолжение цепочки через super()
        return ["Validation"]


class Service(LoggingMixin, ValidationMixin):
    pass


class Shape:
    # TODO: сделайте абстрактным (ABC, @abstractmethod area) и добавьте describe()
    def area(self):
        pass


class Circle(Shape):
    def __init__(self, r):
        self.r = r

    # TODO: area


class Rect(Shape):
    def __init__(self, w, h):
        self.w, self.h = w, h

    # TODO: area


# TODO: Speaker - runtime_checkable Protocol с методом speak(self) -> str
class Speaker:
    pass


def make_chorus(objs):
    """Результаты speak() у объектов, удовлетворяющих Speaker; остальные пропускаются."""
    # TODO
    return []


class Stack(list):
    # TODO: замените наследование от list композицией (внутренний список) и реализуйте push/pop/peek/__len__
    pass
