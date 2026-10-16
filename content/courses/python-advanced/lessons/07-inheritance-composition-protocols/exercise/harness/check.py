import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import math
import typing

import design_task as m


def t_mro_service():
    return expect(m.Service().handle(), ["Logging", "Validation", "Base"], "Service().handle()")


def t_mro_swapped():
    class Swapped(m.ValidationMixin, m.LoggingMixin):
        pass

    return expect(Swapped().handle(), ["Validation", "Logging", "Base"], "handle() при обратном порядке наследования")


def t_mro_alone():
    bad = expect(m.LoggingMixin().handle(), ["Logging", "Base"], "LoggingMixin().handle()")
    if bad:
        return bad
    bad = expect(m.ValidationMixin().handle(), ["Validation", "Base"], "ValidationMixin().handle()")
    if bad:
        return bad
    names = [c.__name__ for c in m.Service.__mro__]
    return expect(names[:4], ["Service", "LoggingMixin", "ValidationMixin", "Base"], "начало MRO у Service")


def t_shape_basic():
    bad = expect(m.Circle(1).describe(), "Circle area=3.14", "Circle(1).describe()")
    if bad:
        return bad
    bad = expect(m.Rect(2, 3).describe(), "Rect area=6.00", "Rect(2, 3).describe()")
    if bad:
        return bad
    if abs(m.Circle(2).area() - math.pi * 4) > 1e-9:
        return "Circle(2).area() должна быть pi * r**2"


def t_shape_abstract():
    try:
        m.Shape()
    except TypeError:
        pass
    else:
        return "Shape() должен поднимать TypeError (абстрактный класс)"

    class NoArea(m.Shape):
        pass

    try:
        NoArea()
    except TypeError:
        pass
    else:
        return "подкласс без area() не должен создаваться"
    if "area" not in getattr(m.Shape, "__abstractmethods__", ()):
        return "area должен быть помечен @abstractmethod"


class Dog:
    def speak(self):
        return "Woof"


class Robot:
    def speak(self):
        return "Beep"


class Mute:
    def talk(self):
        return "..."


def t_chorus_basic():
    return expect(m.make_chorus([Dog(), 42, Robot(), Mute()]), ["Woof", "Beep"], "make_chorus([...])")


def t_chorus_structural():
    if typing.Protocol not in m.Speaker.__mro__:
        return "Speaker должен быть typing.Protocol"
    try:
        m.Speaker()
    except TypeError:
        pass
    else:
        return "Protocol нельзя инстанцировать"
    if Dog in m.Speaker.__mro__ or issubclass(Dog, m.Speaker) is False:
        return "Dog не наследует Speaker, но должен ему удовлетворять (нужен @runtime_checkable)"
    if isinstance(Mute(), m.Speaker):
        return "объект без speak не должен удовлетворять Speaker"
    return expect(m.make_chorus([]), [], "make_chorus([])")


def t_stack_basic():
    s = m.Stack()
    s.push(1)
    s.push(2)
    bad = expect((s.peek(), len(s)), (2, 2), "peek() и len после двух push")
    if bad:
        return bad
    bad = expect((s.pop(), s.pop()), (2, 1), "два pop")
    if bad:
        return bad
    return expect(len(s), 0, "len после опустошения")


def t_stack_composition():
    s = m.Stack()
    if isinstance(s, list):
        return "Stack не должен наследоваться от list (используйте композицию)"
    for name in ("append", "insert", "sort", "extend"):
        if hasattr(s, name):
            return f"у Stack не должно быть метода {name}"
    for op in (s.pop, s.peek):
        try:
            op()
        except IndexError:
            continue
        return "pop/peek на пустом стеке должны поднимать IndexError"


case("mro-service", t_mro_service)
case("shape-basic", t_shape_basic)
case("chorus-basic", t_chorus_basic)
case("stack-basic", t_stack_basic)
case("mro-swapped-order", t_mro_swapped)
case("mro-alone", t_mro_alone)
case("shape-abstract", t_shape_abstract)
case("chorus-structural", t_chorus_structural)
case("stack-composition", t_stack_composition)
