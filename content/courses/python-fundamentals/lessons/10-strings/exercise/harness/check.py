import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import text_tools as m


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


def expect_all(func, name, pairs):
    for args, want in pairs:
        got = func(*args)
        if got != want:
            shown = ", ".join(repr(a) for a in args)
            return f"{name}({shown}) вернуло {got!r}, ожидалось {want!r}"
    return None


def t_initial_basic():
    return expect_all(m.initial, "initial", [(("анна",), "А."), (("Борис",), "Б."), (("ёж",), "Ё.")])


def t_initial_edges():
    return expect_all(m.initial, "initial", [(("  иван ",), "И."), (("",), ""), (("   ",), "")])


def t_palindrome_basic():
    return expect_all(m.is_palindrome, "is_palindrome",
                      [(("шалаш",), True), (("python",), False), (("А роза упала на лапу Азора",), True), (("ab",), False)])


def t_palindrome_edges():
    return expect_all(m.is_palindrome, "is_palindrome",
                      [(("",), True), (("а",), True), (("Aa",), True), (("аб ба",), True), (("аб вб",), False)])


def t_vowels_basic():
    return expect_all(m.count_vowels, "count_vowels", [(("привет",), 2), (("ЁЖ",), 1), (("ГРМ",), 0)])


def t_vowels_edges():
    return expect_all(m.count_vowels, "count_vowels",
                      [(("",), 0), (("аеёиоуыэюя",), 10), (("Съешь ещё этих мягких булок",), 9), (("hello",), 0)])


def t_mask_basic():
    return expect_all(m.mask_number, "mask_number",
                      [(("1234567890123456",), "************3456"), (("12345",), "*2345")])


def t_mask_edges():
    return expect_all(m.mask_number, "mask_number", [(("1234",), "1234"), (("12",), "12"), (("",), "")])


def t_returns_strings():
    for value in (m.initial("а"), m.mask_number("123456")):
        if not isinstance(value, str):
            return f"функция должна возвращать строку, а вернула {value!r}"
    if m.is_palindrome("а") is not True:
        return "is_palindrome должна возвращать True или False"


case("initial-basic", t_initial_basic)
case("palindrome-basic", t_palindrome_basic)
case("vowels-basic", t_vowels_basic)
case("mask-basic", t_mask_basic)
case("initial-edges", t_initial_edges)
case("palindrome-edges", t_palindrome_edges)
case("vowels-edges", t_vowels_edges)
case("mask-edges", t_mask_edges)
case("types", t_returns_strings)
