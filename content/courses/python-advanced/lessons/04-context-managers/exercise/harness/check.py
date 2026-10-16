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


import contextlib

import ctx_task as m


def t_transaction_commit():
    data = {"a": 1}
    with m.Transaction(data) as d:
        if d is not data:
            return "__enter__ должен возвращать сам словарь store"
        d["a"] = 2
        d["b"] = 3
    return expect(data, {"a": 2, "b": 3}, "store после успешного блока")


def t_transaction_rollback():
    data = {"a": 1, "b": 2}
    try:
        with m.Transaction(data) as d:
            d["a"] = 100
            del d["b"]
            d["c"] = 3
            raise RuntimeError("сбой")
    except RuntimeError:
        pass
    else:
        return "исключение не должно подавляться"
    return expect(data, {"a": 1, "b": 2}, "store после отката")


def t_ignore_basic():
    with m.Ignore(KeyError) as ig:
        if ig.caught is not None:
            return "до исключения caught должен быть None"
        {}["x"]
    if not isinstance(ig.caught, KeyError):
        return f"ig.caught = {ig.caught!r}, ожидалось KeyError"
    with m.Ignore(ValueError) as ok:
        pass
    return expect(ok.caught, None, "caught без исключения")


def t_ignore_propagates():
    try:
        with m.Ignore(KeyError):
            raise ValueError("не моё")
    except ValueError:
        pass
    else:
        return "ValueError не входит в exc_types и должно пролететь наружу"
    with m.Ignore(LookupError) as ig:
        {}["k"]
    if not isinstance(ig.caught, KeyError):
        return "подклассы (KeyError для LookupError) тоже должны подавляться"
    try:
        with m.Ignore():
            raise OSError
    except OSError:
        return None
    return "Ignore() без типов ничего не подавляет"


class Obj:
    pass


def t_temp_attr_basic():
    o = Obj()
    o.x = 1
    with m.temp_attr(o, "x", 99) as got:
        if got is not o:
            return "менеджер должен возвращать obj"
        inside = o.x
    bad = expect(inside, 99, "значение внутри блока")
    if bad:
        return bad
    return expect(o.x, 1, "значение после блока")


def t_temp_attr_restore():
    o = Obj()
    o.x = 1
    try:
        with m.temp_attr(o, "x", 5):
            raise RuntimeError
    except RuntimeError:
        pass
    bad = expect(o.x, 1, "значение после блока с исключением")
    if bad:
        return bad
    with m.temp_attr(o, "fresh", "v"):
        if o.fresh != "v":
            return "новый атрибут должен быть виден внутри блока"
    if hasattr(o, "fresh"):
        return "атрибута раньше не было - после блока его нужно удалить"


class Res:
    def __init__(self, name, log, fail_enter=False):
        self.name, self.log, self.fail_enter = name, log, fail_enter

    def __enter__(self):
        if self.fail_enter:
            self.log.append(f"fail {self.name}")
            raise RuntimeError(self.name)
        self.log.append(f"open {self.name}")
        return self.name.upper()

    def __exit__(self, *exc):
        self.log.append(f"close {self.name}")
        return False


def t_enter_all_basic():
    log = []
    with m.enter_all(Res("a", log), Res("b", log)) as vals:
        bad = expect(list(vals), ["A", "B"], "значения enter_all")
        if bad:
            return bad
    return expect(log, ["open a", "open b", "close b", "close a"], "журнал открытия и закрытия")


def t_enter_all_cleanup():
    log = []
    try:
        with m.enter_all(Res("a", log), Res("b", log), Res("c", log)):
            raise ValueError
    except ValueError:
        pass
    else:
        return "исключение из тела не должно подавляться"
    return expect(
        log,
        ["open a", "open b", "open c", "close c", "close b", "close a"],
        "журнал при исключении в теле",
    )


def t_enter_all_partial():
    log = []
    try:
        with m.enter_all(Res("a", log), Res("b", log), Res("c", log, fail_enter=True)):
            return "тело не должно выполниться: вход в третий менеджер падает"
    except RuntimeError:
        pass
    return expect(log, ["open a", "open b", "fail c", "close b", "close a"], "журнал при падении входа")


case("transaction-commit", t_transaction_commit)
case("ignore-basic", t_ignore_basic)
case("temp-attr-basic", t_temp_attr_basic)
case("enter-all-basic", t_enter_all_basic)
case("transaction-rollback", t_transaction_rollback)
case("ignore-propagates", t_ignore_propagates)
case("temp-attr-restore", t_temp_attr_restore)
case("enter-all-cleanup", t_enter_all_cleanup)
case("enter-all-partial", t_enter_all_partial)
