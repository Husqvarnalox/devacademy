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


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


def t_pal_basic():
    for text, want in [("level", True), ("Level", True), ("python", False), ("а роза упала на лапу азора", True)]:
        bad = expect(m.is_palindrome(text), want, f"is_palindrome({text!r})")
        if bad:
            return bad


def t_pal_punct():
    for text, want in [("А роза упала на лапу Азора!", True), ("Was it a car or a cat I saw?", True),
                       ("abca", False), ("", True), ("12321", True), ("Не палиндром, точно", False)]:
        bad = expect(m.is_palindrome(text), want, f"is_palindrome({text!r})")
        if bad:
            return bad


def t_initials_basic():
    for text, want in [("Иван Петрович Сидоров", "И.П.С."), ("Анна Ли", "А.Л."), ("Мадонна", "М.")]:
        bad = expect(m.initials(text), want, f"initials({text!r})")
        if bad:
            return bad


def t_initials_edge():
    for text, want in [("  анна   мария  ", "А.М."), ("", ""), ("   ", ""), ("ёж", "Ё.")]:
        bad = expect(m.initials(text), want, f"initials({text!r})")
        if bad:
            return bad


def t_row_basic():
    bad = expect(m.format_row("Хлеб", 2, 45.5), "Хлеб      |   2|   45.50", "format_row('Хлеб', 2, 45.5)")
    if bad:
        return bad
    return expect(m.format_row("Milk", 10, 1), "Milk      |  10|    1.00", "format_row('Milk', 10, 1)")


def t_row_edge():
    for args, want in [(("Очень длинное имя", 1, 2.5), "Очень длинное имя|   1|    2.50"),
                       (("A", 1000, 12345.678), "A         |1000|12345.68"),
                       (("", 0, 0), "          |   0|    0.00")]:
        bad = expect(m.format_row(*args), want, f"format_row{args}")
        if bad:
            return bad


def t_rev_basic():
    for text, want in [("a b c", "c b a"), ("один два", "два один"), ("слово", "слово")]:
        bad = expect(m.reverse_words(text), want, f"reverse_words({text!r})")
        if bad:
            return bad


def t_rev_spaces():
    for text, want in [("  a   b  ", "b a"), ("", ""), ("   ", ""), ("x\ty\nz", "z y x")]:
        bad = expect(m.reverse_words(text), want, f"reverse_words({text!r})")
        if bad:
            return bad


case("palindrome-basic", t_pal_basic)
case("initials-basic", t_initials_basic)
case("format-row-basic", t_row_basic)
case("reverse-words-basic", t_rev_basic)
case("palindrome-punctuation", t_pal_punct)
case("initials-edge", t_initials_edge)
case("format-row-edge", t_row_edge)
case("reverse-words-spaces", t_rev_spaces)
