import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import functions_check as m


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
    for text, want in [("level", True), ("Python", False), ("А роза упала на лапу Азора", True), ("A man, a plan, a canal: Panama", True)]:
        got = m.is_palindrome(text)
        if got is not want:
            return f"is_palindrome({text!r}) вернуло {got!r}, ожидалось {want!r}"


def t_pal_edges():
    for text, want in [("", True), ("!!!", True), ("ab", False), ("12321", True), ("1a2", False)]:
        got = m.is_palindrome(text)
        if got is not want:
            return f"is_palindrome({text!r}) вернуло {got!r}, ожидалось {want!r}"


def t_caesar_basic():
    bad = expect(m.caesar("Abc, xyz!", 3), "Def, abc!", "caesar('Abc, xyz!', 3)")
    return bad or expect(m.caesar("Hello", 0), "Hello", "caesar('Hello', 0)")


def t_caesar_edges():
    for args, want in [(("abc", -1), "zab"), (("XYZ", 29), "ABC"), (("Привет 123", 5), "Привет 123"), (("", 4), ""), (("Zz", 26), "Zz")]:
        bad = expect(m.caesar(*args), want, f"caesar{args!r}")
        if bad:
            return bad


def t_trunc_basic():
    bad = expect(m.truncate("Hello, world", 8), "Hello...", "truncate('Hello, world', 8)")
    return bad or expect(m.truncate("short", 10), "short", "truncate('short', 10)")


def t_trunc_edges():
    bad = expect(m.truncate("abcdef", 6), "abcdef", "truncate('abcdef', 6)")
    bad = bad or expect(m.truncate("abcdefg", 6, "~"), "abcde~", "truncate('abcdefg', 6, '~')")
    bad = bad or expect(m.truncate("abcdef", 3), "...", "truncate('abcdef', 3)")
    if bad:
        return bad
    try:
        m.truncate("abcdef", 2)
    except ValueError:
        return None
    return "truncate('abcdef', 2) должна вызвать ValueError"


case("pal-basic", t_pal_basic)
case("caesar-basic", t_caesar_basic)
case("trunc-basic", t_trunc_basic)
case("pal-edges", t_pal_edges)
case("caesar-edges", t_caesar_edges)
case("trunc-edges", t_trunc_edges)
