import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import dict_tools as m


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
    if actual != expected or type(actual) is not type(expected):
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


def t_count_basic():
    for words, want in [(["a", "b", "a"], {"a": 2, "b": 1}), (["x"], {"x": 1}), (["да", "да", "да"], {"да": 3})]:
        bad = expect(m.count_words(words), want, f"count_words({words})")
        if bad:
            return bad


def t_count_edges():
    bad = expect(m.count_words([]), {}, "count_words([])")
    if bad:
        return bad
    words = ["b", "a", "b"]
    m.count_words(words)
    if words != ["b", "a", "b"]:
        return "count_words изменила переданный список"
    return expect(m.count_words(["A", "a"]), {"A": 1, "a": 1}, "count_words(['A', 'a'])")


def t_unique_basic():
    for items, want in [([3, 1, 3, 2], [1, 2, 3]), (["б", "а", "б"], ["а", "б"]), ([5], [5])]:
        bad = expect(m.unique_sorted(items), want, f"unique_sorted({items})")
        if bad:
            return bad


def t_unique_edges():
    for items, want in [([], []), ([2, 2, 2], [2]), ([-1, 0, -1], [-1, 0])]:
        bad = expect(m.unique_sorted(items), want, f"unique_sorted({items})")
        if bad:
            return bad


def t_common_basic():
    for a, b, want in [([1, 2, 3], [2, 3, 4], [2, 3]), (["a", "b"], ["b", "c"], ["b"]), ([1], [2], [])]:
        bad = expect(m.common_items(a, b), want, f"common_items({a}, {b})")
        if bad:
            return bad


def t_common_edges():
    for a, b, want in [([], [1], []), ([1, 1, 2], [1, 1], [1]), ([3, 2, 1], [1, 2, 3], [1, 2, 3])]:
        bad = expect(m.common_items(a, b), want, f"common_items({a}, {b})")
        if bad:
            return bad


def t_cart_basic():
    prices = {"хлеб": 45, "молоко": 80, "сыр": 300}
    for cart, want in [(["хлеб", "молоко"], 125), (["хлеб", "хлеб"], 90), ([], 0)]:
        bad = expect(m.cart_total(prices, cart), want, f"cart_total(prices, {cart})")
        if bad:
            return bad


def t_cart_unknown():
    prices = {"хлеб": 45}
    bad = expect(m.cart_total(prices, ["хлеб", "яхта"]), 45, "cart_total(prices, ['хлеб', 'яхта'])")
    if bad:
        return bad
    return expect(m.cart_total({}, ["a"]), 0, "cart_total({}, ['a'])")


case("count-basic", t_count_basic)
case("unique-basic", t_unique_basic)
case("common-basic", t_common_basic)
case("cart-basic", t_cart_basic)
case("count-edges", t_count_edges)
case("unique-edges", t_unique_edges)
case("common-edges", t_common_edges)
case("cart-unknown", t_cart_unknown)
