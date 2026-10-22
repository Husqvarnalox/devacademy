import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import collections_task as m


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


def t_group_basic():
    got = m.group_by_first(["Яблоко", "банан", "ананас", "Банан"])
    want = {"я": ["Яблоко"], "б": ["банан", "Банан"], "а": ["ананас"]}
    bad = expect(got, want, "group_by_first(['Яблоко', 'банан', 'ананас', 'Банан'])")
    return bad or expect(list(got), ["я", "б", "а"], "порядок ключей group_by_first")


def t_group_edges():
    bad = expect(m.group_by_first([]), {}, "group_by_first([])")
    bad = bad or expect(m.group_by_first(["", "a", "", "A"]), {"a": ["a", "A"]}, "group_by_first(['', 'a', '', 'A'])")
    words = ["b", "a"]
    m.group_by_first(words)
    return bad or expect(words, ["b", "a"], "исходный список после group_by_first")


def t_dedupe_basic():
    bad = expect(m.dedupe([3, 1, 3, 2, 1]), [3, 1, 2], "dedupe([3, 1, 3, 2, 1])")
    return bad or expect(m.dedupe("abca"), ["a", "b", "c"], "dedupe('abca')")


def t_dedupe_edges():
    bad = expect(m.dedupe([]), [], "dedupe([])")
    bad = bad or expect(m.dedupe([1, "1", 1, (1,), (1,)]), [1, "1", (1,)], "dedupe([1, '1', 1, (1,), (1,)])")
    data = [2, 2, 1]
    got = m.dedupe(data)
    if got is data:
        return "dedupe должна вернуть новый список"
    return bad or expect(data, [2, 2, 1], "исходный список после dedupe")


def t_merge_basic():
    return expect(m.merge_counts({"a": 1, "b": 2}, {"b": 3, "c": 1}), {"a": 1, "b": 5, "c": 1},
                  "merge_counts({'a': 1, 'b': 2}, {'b': 3, 'c': 1})")


def t_merge_pure():
    a = {"x": 1}
    b = {"x": 2, "y": 3}
    got = m.merge_counts(a, b)
    if a != {"x": 1} or b != {"x": 2, "y": 3}:
        return "merge_counts изменила входные словари"
    if got is a or got is b:
        return "merge_counts должна вернуть новый словарь"
    bad = expect(m.merge_counts({}, {}), {}, "merge_counts({}, {})")
    return bad or expect(m.merge_counts({}, {"z": 4}), {"z": 4}, "merge_counts({}, {'z': 4})")


def t_top_basic():
    return expect(m.top_n({"x": 2, "y": 5, "z": 2}, 2), [("y", 5), ("x", 2)], "top_n({'x': 2, 'y': 5, 'z': 2}, 2)")


def t_top_edges():
    counts = {"b": 1, "a": 1, "c": 1}
    bad = expect(m.top_n(counts, 2), [("a", 1), ("b", 1)], "top_n при равных числах")
    bad = bad or expect(m.top_n(counts, 10), [("a", 1), ("b", 1), ("c", 1)], "top_n с n больше числа ключей")
    bad = bad or expect(m.top_n(counts, 0), [], "top_n(counts, 0)")
    return bad or expect(m.top_n({}, 3), [], "top_n({}, 3)")


def t_grid_basic():
    return expect(m.make_grid(2, 3), [[0, 0, 0], [0, 0, 0]], "make_grid(2, 3)")


def t_grid_independent():
    grid = m.make_grid(3, 2, ".")
    grid[0][0] = "#"
    bad = expect(grid, [["#", "."], [".", "."], [".", "."]], "make_grid(3, 2, '.') после изменения grid[0][0]")
    bad = bad or expect(m.make_grid(0, 5), [], "make_grid(0, 5)")
    return bad or expect(m.make_grid(2, 0), [[], []], "make_grid(2, 0)")


case("group-basic", t_group_basic)
case("dedupe-basic", t_dedupe_basic)
case("merge-basic", t_merge_basic)
case("top-basic", t_top_basic)
case("grid-basic", t_grid_basic)
case("group-edges", t_group_edges)
case("dedupe-edges", t_dedupe_edges)
case("merge-pure", t_merge_pure)
case("top-edges", t_top_edges)
case("grid-independent", t_grid_independent)
