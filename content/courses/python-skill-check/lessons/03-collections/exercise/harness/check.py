import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import collections_check as m


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


def t_freq_basic():
    got = m.word_freq("To be, or not to be!")
    bad = expect(got, {"to": 2, "be": 2, "or": 1, "not": 1}, "word_freq('To be, or not to be!')")
    return bad or expect(list(got), ["to", "be", "or", "not"], "порядок ключей word_freq")


def t_freq_edges():
    bad = expect(m.word_freq(""), {}, "word_freq('')")
    bad = bad or expect(m.word_freq("  ... ?!  "), {}, "word_freq('  ... ?!  ')")
    return bad or expect(m.word_freq("Да, да.  ДА!\nнет"), {"да": 3, "нет": 1}, "word_freq с кириллицей и переносом строки")


def t_invert_basic():
    return expect(m.invert({"a": 1, "b": 2, "c": 1}), {1: ["a", "c"], 2: ["b"]}, "invert({'a': 1, 'b': 2, 'c': 1})")


def t_invert_edges():
    source = {"z": 0, "y": 0, "x": 0}
    bad = expect(m.invert(source), {0: ["x", "y", "z"]}, "invert с одним значением: ключи должны быть отсортированы")
    bad = bad or expect(m.invert({}), {}, "invert({})")
    return bad or expect(source, {"z": 0, "y": 0, "x": 0}, "исходный словарь после invert")


def t_common_basic():
    return expect(m.common([3, 1, 2, 2], [2, 3, 5, 3]), [2, 3], "common([3, 1, 2, 2], [2, 3, 5, 3])")


def t_common_edges():
    a = [5, 1]
    bad = expect(m.common([], [1]), [], "common([], [1])")
    bad = bad or expect(m.common(["b", "a"], ["a", "b", "c"]), ["a", "b"], "common со строками")
    bad = bad or expect(m.common([1], [2]), [], "common без общих")
    m.common(a, [1])
    return bad or expect(a, [5, 1], "исходный список после common")


case("freq-basic", t_freq_basic)
case("invert-basic", t_invert_basic)
case("common-basic", t_common_basic)
case("freq-edges", t_freq_edges)
case("invert-edges", t_invert_edges)
case("common-edges", t_common_edges)
