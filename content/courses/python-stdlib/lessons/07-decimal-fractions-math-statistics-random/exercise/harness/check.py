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


import random
import re
from decimal import Decimal as D

import money_task as m


def t_parse_basic():
    for text, want in ((" 3.1 ", D("3.10")), ("5", D("5.00")), ("12.500", D("12.50")), ("0", D("0.00")), ("-2.5", D("-2.50"))):
        got = m.parse_money(text)
        bad = expect(got, want, f"parse_money({text!r})")
        if bad:
            return bad
        if got.as_tuple().exponent != -2:
            return f"parse_money({text!r}) должен иметь ровно два знака после запятой, получено {got!r}"


def t_parse_errors():
    for text in ("1.005", "abc", "", "NaN", "Infinity", "-inf", "1,5", "12.345678"):
        try:
            got = m.parse_money(text)
        except ValueError:
            continue
        return f"parse_money({text!r}) вернуло {got!r}, ожидался ValueError"


def t_round_basic():
    bad = expect(m.round_money(D("2.675")), D("2.68"), "round_money(2.675)")
    if bad:
        return bad
    bad = expect(m.round_money(D("2.665"), "half_even"), D("2.66"), "round_money(2.665, half_even)")
    if bad:
        return bad
    bad = expect(m.round_money(D("2.665")), D("2.67"), "round_money(2.665, half_up)")
    if bad:
        return bad
    return expect(m.round_money(D("2.675"), "half_even"), D("2.68"), "round_money(2.675, half_even)")


def t_round_ties():
    bad = expect(m.round_money(D("-2.675")), D("-2.68"), "round_money(-2.675) half_up уводит от нуля")
    if bad:
        return bad
    bad = expect(m.round_money(D("-2.665"), "half_even"), D("-2.66"), "round_money(-2.665, half_even)")
    if bad:
        return bad
    bad = expect(m.round_money(D("0.005")), D("0.01"), "round_money(0.005)")
    if bad:
        return bad
    bad = expect(m.round_money(D("0.005"), "half_even"), D("0.00"), "round_money(0.005, half_even)")
    if bad:
        return bad
    try:
        m.round_money(D("1.234"), "up")
    except ValueError:
        return None
    return "неизвестный режим должен давать ValueError"


def t_total_basic():
    bad = expect(m.line_total(D("19.99"), 3, 10), D("53.97"), "line_total(19.99, 3, 10)")
    if bad:
        return bad
    bad = expect(m.line_total(D("10.00"), 1, 0), D("10.00"), "line_total(10.00, 1, 0)")
    if bad:
        return bad
    return expect(m.line_total(D("0.99"), 7, D("12.5")), D("6.06"), "line_total(0.99, 7, 12.5)")


def t_vat_and_errors():
    bad = expect(m.vat_included(D("120.00"), 20), D("20.00"), "vat_included(120.00, 20)")
    if bad:
        return bad
    bad = expect(m.vat_included(D("10.00"), 7), D("0.65"), "vat_included(10.00, 7)")
    if bad:
        return bad
    bad = expect(m.vat_included(D("19.99"), D("22")), D("3.60"), "vat_included(19.99, 22)")
    if bad:
        return bad
    for args in ((D("1.00"), -1, 0), (D("1.00"), 1, 101), (D("1.00"), 1, -5)):
        try:
            m.line_total(*args)
        except ValueError:
            continue
        return f"line_total{args} должен поднимать ValueError"


def t_split_basic():
    got = m.split_bill(D("100.00"), 3)
    bad = expect(got, [D("33.34"), D("33.33"), D("33.33")], "split_bill(100.00, 3)")
    if bad:
        return bad
    bad = expect(m.split_bill(D("10.00"), 4), [D("2.50")] * 4, "split_bill(10.00, 4)")
    if bad:
        return bad
    return expect(sum(m.split_bill(D("0.10"), 3)), D("0.10"), "сумма частей split_bill(0.10, 3)")


def t_split_edge():
    got = m.split_bill(D("0.02"), 5)
    bad = expect(got, [D("0.01"), D("0.01"), D("0.00"), D("0.00"), D("0.00")], "split_bill(0.02, 5)")
    if bad:
        return bad
    if any(not isinstance(p, D) or p.as_tuple().exponent != -2 for p in got):
        return "каждая часть - Decimal с двумя знаками"
    big = m.split_bill(D("1234567.89"), 7)
    bad = expect(sum(big), D("1234567.89"), "сумма частей большой суммы")
    if bad:
        return bad
    bad = expect(m.split_bill(D("5.00"), 1), [D("5.00")], "split_bill(5.00, 1)")
    if bad:
        return bad
    for args in ((D("1.00"), 0), (D("-1.00"), 2)):
        try:
            m.split_bill(*args)
        except ValueError:
            continue
        return f"split_bill{args} должен поднимать ValueError"


def t_fraction_sum():
    bad = expect(m.exact_sum(["1/2", "1/3", "1/3"]), "7/6", "exact_sum(['1/2', '1/3', '1/3'])")
    if bad:
        return bad
    bad = expect(m.exact_sum(["0.1", "0.2"]), "3/10", "exact_sum(['0.1', '0.2'])")
    if bad:
        return bad
    bad = expect(m.exact_sum([]), "0", "exact_sum([])")
    if bad:
        return bad
    return expect(m.exact_sum(["1/2", "1/2"]), "1", "exact_sum(['1/2', '1/2'])")


def t_summary():
    got = m.summary([2, 4, 4, 4, 5, 5, 7, 9])
    bad = expect(
        got,
        {"mean": 5.0, "median": 4.5, "stdev": 2.13809, "pstdev": 2.0},
        "summary([2, 4, 4, 4, 5, 5, 7, 9])",
    )
    if bad:
        return bad
    bad = expect(m.summary([1, 3])["stdev"], 1.414214, "summary([1, 3])['stdev']")
    if bad:
        return bad
    for vals in ([], [5]):
        try:
            m.summary(vals)
        except ValueError:
            continue
        return f"summary({vals}) должен поднимать ValueError"


def t_shuffled():
    items = list(range(20))
    snapshot = list(items)
    state = random.getstate()
    a = m.shuffled(items, 123)
    b = m.shuffled(items, 123)
    if random.getstate() != state:
        return "shuffled изменила состояние глобального random; используйте random.Random(seed)"
    if items != snapshot:
        return "исходный список не должен меняться"
    bad = expect(a, b, "два вызова shuffled с одним seed")
    if bad:
        return bad
    bad = expect(sorted(a), snapshot, "shuffled должна быть перестановкой")
    if bad:
        return bad
    if a == snapshot and m.shuffled(items, 124) == snapshot:
        return "shuffled не перемешивает"
    if not any(m.shuffled(items, s) != a for s in range(5)):
        return "разные seed должны давать разные порядки"
    return expect(m.shuffled(iter("abc"), 1).__len__(), 3, "shuffled принимает любой итерируемый объект")


def t_token():
    t1 = m.new_token()
    t2 = m.new_token()
    if not re.fullmatch(r"[0-9a-f]{32}", t1):
        return f"new_token() вернуло {t1!r}, ожидалась hex-строка из 32 символов"
    if t1 == t2:
        return "два токена совпали: используйте secrets"
    if len(m.new_token(4)) != 8:
        return "длина new_token(4) должна быть 8"
    try:
        m.new_token(0)
    except ValueError:
        pass
    else:
        return "new_token(0) должен поднимать ValueError"
    state = random.getstate()
    m.new_token()
    if random.getstate() != state:
        return "токен не должен зависеть от глобального random"


case("parse-basic", t_parse_basic)
case("round-basic", t_round_basic)
case("total-basic", t_total_basic)
case("split-basic", t_split_basic)
case("parse-errors", t_parse_errors)
case("round-ties", t_round_ties)
case("split-edge", t_split_edge)
case("vat-and-errors", t_vat_and_errors)
case("fraction-sum", t_fraction_sum)
case("summary-stats", t_summary)
case("shuffled-repro", t_shuffled)
case("token-secure", t_token)
