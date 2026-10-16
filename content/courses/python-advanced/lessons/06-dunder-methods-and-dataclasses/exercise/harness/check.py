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


import dataclasses

import model_task as m


def t_money_repr_str():
    x = m.Money(150)
    bad = expect(repr(x), "Money(150, 'USD')", "repr(Money(150))")
    if bad:
        return bad
    bad = expect(str(x), "1.50 USD", "str(Money(150))")
    if bad:
        return bad
    bad = expect(repr(m.Money(5, "EUR")), "Money(5, 'EUR')", "repr(Money(5, 'EUR'))")
    if bad:
        return bad
    return expect(str(m.Money(5, "EUR")), "0.05 EUR", "str(Money(5, 'EUR'))")


def t_money_order():
    xs = [m.Money(300), m.Money(100), m.Money(200)]
    bad = expect([x.cents for x in sorted(xs)], [100, 200, 300], "sorted(...)")
    if bad:
        return bad
    if max(xs).cents != 300:
        return "max(...) вернул неверный объект"
    a, b = m.Money(1), m.Money(2)
    if not (a < b and a <= b and b > a and b >= a and not (a > b)):
        return "должны работать <, <=, >, >= (используйте functools.total_ordering)"


def t_money_eq_hash():
    a, b = m.Money(5), m.Money(5)
    if not (a == b) or a != b:
        return "Money(5) == Money(5) должно быть True"
    if hash(a) != hash(b):
        return "равные объекты должны иметь равные хеши"
    if len({a, b, m.Money(6)}) != 2:
        return "в множестве {Money(5), Money(5), Money(6)} должно быть 2 элемента"
    if m.Money(5) == m.Money(5, "EUR"):
        return "разные валюты - разные объекты"
    if (m.Money(5) == 5) is not False or (m.Money(5) != "x") is not True:
        return "сравнение с объектами других типов должно давать False/True без исключения"
    d = {m.Money(1): "one"}
    return expect(d[m.Money(1)], "one", "доступ по ключу Money(1)")


def t_money_currency_mismatch():
    try:
        m.Money(1, "USD") < m.Money(2, "EUR")
    except TypeError:
        pass
    else:
        return "сравнение порядка разных валют должно давать TypeError"
    try:
        m.Money(1) < 5
    except TypeError:
        return None
    return "Money(1) < 5 должно давать TypeError"


def t_playlist_basic():
    p = m.Playlist(["a", "b", "c"])
    bad = expect(len(p), 3, "len(Playlist)")
    if bad:
        return bad
    bad = expect(p[1], "b", "Playlist[1]")
    if bad:
        return bad
    if "c" not in p or "z" in p:
        return "оператор in работает неверно"
    return expect(bool(m.Playlist([])), False, "bool(Playlist([]))")


def t_playlist_index_slice():
    p = m.Playlist(["a", "b", "c", "d"])
    bad = expect(p[-1], "d", "Playlist[-1]")
    if bad:
        return bad
    s = p[1:3]
    if not isinstance(s, m.Playlist):
        return f"срез должен возвращать Playlist, а вернул {type(s).__name__}"
    bad = expect(list(s), ["b", "c"], "список из среза p[1:3]")
    if bad:
        return bad
    bad = expect(len(s), 2, "len(p[1:3])")
    if bad:
        return bad
    try:
        p[10]
    except IndexError:
        pass
    else:
        return "p[10] должен поднимать IndexError"
    return expect([t for t in p], ["a", "b", "c", "d"], "цикл for по Playlist")


def t_version_order():
    vs = [m.Version(1, 10), m.Version(1, 2), m.Version(0, 9, 9), m.Version(1, 2, 1)]
    bad = expect([str(v) for v in sorted(vs)], ["0.9.9", "1.2.0", "1.2.1", "1.10.0"], "sorted(versions)")
    if bad:
        return bad
    if not (m.Version(1, 2) < m.Version(1, 10)) or m.Version(1) != m.Version(1, 0, 0):
        return "сравнения Version работают неверно"
    return expect(repr(m.Version(2)), "Version(major=2, minor=0, patch=0)", "repr(Version(2))")


def t_version_frozen_hash():
    v = m.Version(1, 2, 3)
    try:
        v.major = 5
    except dataclasses.FrozenInstanceError:
        pass
    except AttributeError:
        pass
    else:
        return "Version должен быть неизменяемым (frozen=True)"
    if len({m.Version(1), m.Version(1, 0, 0), m.Version(2)}) != 2:
        return "равные версии должны давать один элемент множества"
    if not dataclasses.is_dataclass(m.Version):
        return "Version должен быть dataclass"


def t_cart_basic():
    c = m.Cart("ann")
    bad = expect((c.owner, c.items, c.discount), ("ann", [], 0.0), "поля Cart('ann')")
    if bad:
        return bad
    c.add("apple")
    c.add("pear")
    bad = expect(c.items, ["apple", "pear"], "c.items после add")
    if bad:
        return bad
    bad = expect(repr(m.Cart("a")), "Cart(owner='a', items=[], discount=0.0)", "repr(Cart('a'))")
    if bad:
        return bad
    return expect(m.Cart("x", ["a"], 0.5) == m.Cart("x", ["a"], 0.5), True, "сравнение двух одинаковых Cart")


def t_cart_independent_slots():
    a, b = m.Cart("a"), m.Cart("b")
    a.add(1)
    if b.items != [] or a.items is b.items:
        return "списки items разных корзин должны быть независимыми (default_factory)"
    if hasattr(a, "__dict__"):
        return "у Cart не должно быть __dict__ (slots=True)"
    try:
        a.extra = 1
    except AttributeError:
        return None
    return "присваивание неописанного атрибута должно давать AttributeError"


case("money-repr-str", t_money_repr_str)
case("money-order", t_money_order)
case("playlist-basic", t_playlist_basic)
case("version-order", t_version_order)
case("cart-basic", t_cart_basic)
case("money-eq-hash", t_money_eq_hash)
case("money-currency-mismatch", t_money_currency_mismatch)
case("playlist-index-slice", t_playlist_index_slice)
case("version-frozen-hash", t_version_frozen_hash)
case("cart-independent-slots", t_cart_independent_slots)
