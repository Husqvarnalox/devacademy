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


import classes_task as m


def close(a, b):
    return abs(a - b) < 1e-9


def t_temperature_basic():
    t = m.Temperature(100)
    if not close(t.celsius, 100) or not close(t.fahrenheit, 212):
        return f"Temperature(100): celsius={t.celsius!r}, fahrenheit={t.fahrenheit!r}, ожидалось 100 и 212"
    t.fahrenheit = 32
    if not close(t.celsius, 0):
        return f"после fahrenheit = 32 celsius = {t.celsius!r}, ожидалось 0"
    t.celsius = 37
    if not close(t.fahrenheit, 98.6):
        return f"37 C = {t.fahrenheit!r} F, ожидалось 98.6"
    u = m.Temperature.from_fahrenheit(212)
    if not close(u.celsius, 100):
        return f"from_fahrenheit(212).celsius = {u.celsius!r}, ожидалось 100"


def t_temperature_validation():
    try:
        m.Temperature(-300)
    except ValueError:
        pass
    else:
        return "Temperature(-300) должен поднимать ValueError"
    t = m.Temperature(0)
    try:
        t.celsius = -273.16
    except ValueError:
        pass
    else:
        return "запись celsius = -273.16 должна поднимать ValueError"
    if not close(t.celsius, 0):
        return "после неудачной записи значение должно остаться прежним"
    try:
        t.fahrenheit = -500
    except ValueError:
        pass
    else:
        return "запись слишком низкой fahrenheit должна поднимать ValueError"
    if m.Temperature.is_valid(-273.15) is not True or m.Temperature.is_valid(-300) is not False:
        return "is_valid(-273.15) должен давать True, is_valid(-300) - False"
    if t.is_valid(5) is not True:
        return "is_valid должен вызываться и у экземпляра"
    if not isinstance(vars(m.Temperature)["is_valid"], staticmethod):
        return "is_valid должен быть объявлен как @staticmethod"
    if not isinstance(vars(m.Temperature)["from_fahrenheit"], classmethod):
        return "from_fahrenheit должен быть объявлен как @classmethod"


def t_temperature_subclass():
    class Kelvinish(m.Temperature):
        pass

    obj = Kelvinish.from_fahrenheit(32)
    if type(obj) is not Kelvinish:
        return f"from_fahrenheit у подкласса вернул {type(obj).__name__}, ожидался Kelvinish (используйте cls)"


def t_registry_basic():
    m.Registry.reset()
    a, b = m.Registry(), m.Registry()
    bad = expect((a.id, b.id), (1, 2), "id двух первых экземпляров")
    if bad:
        return bad
    return expect(m.Registry.count(), 2, "Registry.count()")


def t_registry_class_attr():
    m.Registry.reset()
    objs = [m.Registry() for _ in range(3)]
    for o in objs:
        if "_next_id" in vars(o):
            return "у экземпляра появился собственный _next_id: изменяйте атрибут класса, а не self"
    bad = expect(m.Registry._next_id, 4, "Registry._next_id после трёх экземпляров")
    if bad:
        return bad
    m.Registry.reset()
    bad = expect((m.Registry._next_id, m.Registry.count()), (1, 0), "состояние после reset()")
    if bad:
        return bad
    return expect(m.Registry().id, 1, "id после reset()")


def t_bag_basic():
    b = m.Bag()
    r = b.add(1).add("x")
    if r is not b:
        return "add должен возвращать self"
    return expect(b.items, [1, "x"], "b.items")


def t_bag_independent():
    a, b = m.Bag(), m.Bag()
    a.add(1)
    if b.items != []:
        return f"у второго экземпляра items = {b.items!r}, ожидался []: список общий для всех"
    if a.items is b.items:
        return "списки items разных экземпляров - один и тот же объект"


def t_point_basic():
    p = m.Point(1, 2)
    q = p.moved(2, 3)
    bad = expect((q.x, q.y), (3, 5), "Point(1, 2).moved(2, 3)")
    if bad:
        return bad
    if q is p or (p.x, p.y) != (1, 2):
        return "moved должен вернуть новую точку и не менять исходную"


def t_point_slots():
    p = m.Point(1, 2)
    if hasattr(p, "__dict__"):
        return "у экземпляров Point не должно быть __dict__ (задайте __slots__)"
    try:
        p.z = 5
    except AttributeError:
        pass
    else:
        return "присваивание p.z должно поднимать AttributeError"
    if tuple(m.Point.__slots__) != ("x", "y"):
        return "__slots__ должен быть ('x', 'y')"


case("temperature-basic", t_temperature_basic)
case("registry-basic", t_registry_basic)
case("bag-basic", t_bag_basic)
case("point-basic", t_point_basic)
case("temperature-validation", t_temperature_validation)
case("temperature-subclass", t_temperature_subclass)
case("registry-class-attr", t_registry_class_attr)
case("bag-independent", t_bag_independent)
case("point-slots", t_point_slots)
