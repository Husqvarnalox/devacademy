import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import text_task as m


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


def t_collect_basic():
    bad = expect(m.collect("a"), ["a"], "collect('a')")
    box = ["x"]
    return bad or expect(m.collect("y", box), ["x", "y"], "collect('y', ['x'])")


def t_collect_fresh():
    first = m.collect("a")
    second = m.collect("b")
    if first is second:
        return "без bucket каждый вызов должен возвращать новый список, а не общий"
    bad = expect(second, ["b"], "collect('b') после collect('a')")
    if bad:
        return bad
    box = []
    if m.collect(1, box) is not box or box != [1]:
        return "при переданном bucket функция должна дополнить именно его и вернуть тот же список"
    return expect(m.collect("c"), ["c"], "collect('c') после работы с чужим bucket")


def t_slug_basic():
    bad = expect(m.slugify("Hello World"), "hello-world", "slugify('Hello World')")
    return bad or expect(m.slugify("Привет, мир!"), "привет-мир", "slugify('Привет, мир!')")


def t_slug_edges():
    cases = [("  Hello,   World! 2024 ", "hello-world-2024"), ("---", ""), ("", ""), ("a_b", "a-b"),
             ("Python 3.14: что нового?", "python-3-14-что-нового"), ("ОДИН", "один")]
    for text, want in cases:
        bad = expect(m.slugify(text), want, f"slugify({text!r})")
        if bad:
            return bad


def t_money_basic():
    bad = expect(m.money(123456), "1,234.56 USD", "money(123456)")
    return bad or expect(m.money(99, "EUR"), "0.99 EUR", "money(99, 'EUR')")


def t_money_edges():
    cases = [((0,), "0.00 USD"), ((-5,), "-0.05 USD"), ((5,), "0.05 USD"), ((100000000,), "1,000,000.00 USD"),
             ((-123456, "RUB"), "-1,234.56 RUB"), ((100,), "1.00 USD")]
    for args, want in cases:
        bad = expect(m.money(*args), want, f"money{args!r}")
        if bad:
            return bad


def t_initials_basic():
    bad = expect(m.initials("анна петровна иванова"), "А.П.И.", "initials('анна петровна иванова')")
    return bad or expect(m.initials("ivan petrov"), "I.P.", "initials('ivan petrov')")


def t_initials_edges():
    for text, want in [("", ""), ("   ", ""), ("  ада   лавлейс ", "А.Л."), ("Madonna", "M.")]:
        bad = expect(m.initials(text), want, f"initials({text!r})")
        if bad:
            return bad


def t_join_basic():
    bad = expect(m.join_nonempty("a", "b", "c"), "a b c", "join_nonempty('a', 'b', 'c')")
    return bad or expect(m.join_nonempty("Москва", "", " ул. Ленина ", sep=", "), "Москва, ул. Ленина",
                         "join_nonempty('Москва', '', ' ул. Ленина ', sep=', ')")


def t_join_kwonly():
    bad = expect(m.join_nonempty(), "", "join_nonempty()")
    bad = bad or expect(m.join_nonempty("  ", ""), "", "join_nonempty('  ', '')")
    bad = bad or expect(m.join_nonempty("a", "b", sep="/"), "a/b", "join_nonempty('a', 'b', sep='/')")
    if bad:
        return bad
    return expect(m.join_nonempty("a", "-", "b"), "a - b", "join_nonempty('a', '-', 'b'): '-' - тоже часть, а не sep")


case("collect-basic", t_collect_basic)
case("slugify-basic", t_slug_basic)
case("money-basic", t_money_basic)
case("initials-basic", t_initials_basic)
case("join-basic", t_join_basic)
case("collect-fresh", t_collect_fresh)
case("slugify-edges", t_slug_edges)
case("money-edges", t_money_edges)
case("initials-edges", t_initials_edges)
case("join-keyword-only", t_join_kwonly)
