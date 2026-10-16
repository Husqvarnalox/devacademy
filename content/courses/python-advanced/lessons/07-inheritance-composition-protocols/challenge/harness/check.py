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


import coop_task as m


def t_basic():
    d = m.Document(name="a", created=5, tags=["x", "y"])
    bad = expect((d.name, d.created, d.tags), ("a", 5, ("x", "y")), "атрибуты Document")
    if bad:
        return bad


def t_defaults():
    d = m.Document(name="b")
    return expect((d.name, d.created, d.tags), ("b", 0, ()), "значения по умолчанию")


def t_mro_names():
    return expect(
        m.mro_names(m.Document),
        ["Document", "NamedMixin", "TimestampedMixin", "TaggedMixin", "Entity"],
        "mro_names(Document)",
    )


def t_any_order():
    class X(m.TaggedMixin, m.NamedMixin):
        pass

    x = X(name="a", tags=[1])
    bad = expect((x.name, x.tags), ("a", (1,)), "атрибуты X")
    if bad:
        return bad

    class Y(m.TimestampedMixin, m.TaggedMixin, m.NamedMixin):
        pass

    y = Y(created=9, name="n")
    bad = expect((y.created, y.name, y.tags), (9, "n", ()), "атрибуты Y")
    if bad:
        return bad

    class Only(m.TimestampedMixin):
        pass

    return expect(Only(created=3).created, 3, "одиночный миксин")


def t_unknown():
    for cls_args in [(m.Document, {"name": "a", "bogus": 1}), (m.Entity, {"x": 1})]:
        cls, kw = cls_args
        try:
            cls(**kw)
        except TypeError:
            continue
        return f"{cls.__name__}({kw}) должен поднимать TypeError"

    class X(m.TaggedMixin, m.NamedMixin):
        pass

    try:
        X(name="a", extra=2)
    except TypeError:
        return None
    return "неизвестный аргумент в другом порядке миксинов должен давать TypeError"


def t_missing():
    try:
        m.Document(created=1)
    except TypeError:
        return None
    return "Document без name должен поднимать TypeError"


case("coop-basic", t_basic)
case("coop-defaults", t_defaults)
case("mro-names", t_mro_names)
case("coop-any-order", t_any_order)
case("coop-unknown-kwarg", t_unknown)
case("coop-missing-name", t_missing)
