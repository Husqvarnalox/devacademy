import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import list_tools as m


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


def t_dedupe_basic():
    for items, want in [([1, 2, 1, 3, 2], [1, 2, 3]), (["b", "a", "b"], ["b", "a"]), ([], []), ([5, 5, 5], [5])]:
        bad = expect(m.remove_duplicates(items), want, f"remove_duplicates({items})")
        if bad:
            return bad


def t_dedupe_edge():
    data = [[1], [1], [2], [1]]
    bad = expect(m.remove_duplicates(data), [[1], [2]], f"remove_duplicates({data})")
    if bad:
        return bad
    original = [3, 1, 3, 1]
    snapshot = list(original)
    result = m.remove_duplicates(original)
    if original != snapshot:
        return "исходный список изменён - функция должна вернуть новый список"
    if result is original:
        return "функция вернула тот же объект, ожидался новый список"
    return expect(m.remove_duplicates([1, 1.0, True]), [1], "remove_duplicates([1, 1.0, True])")


def t_chunk_basic():
    for args, want in [(([1, 2, 3, 4, 5], 2), [[1, 2], [3, 4], [5]]), (([1, 2, 3, 4], 2), [[1, 2], [3, 4]]), ((list("abc"), 1), [["a"], ["b"], ["c"]])]:
        bad = expect(m.chunk(*args), want, f"chunk{args}")
        if bad:
            return bad


def t_chunk_edge():
    bad = expect(m.chunk([1, 2], 5), [[1, 2]], "chunk([1, 2], 5)")
    if bad:
        return bad
    bad = expect(m.chunk([], 3), [], "chunk([], 3)")
    if bad:
        return bad
    for size in (0, -2):
        try:
            m.chunk([1, 2, 3], size)
        except ValueError:
            continue
        except Exception as exc:
            return f"chunk(..., {size}) бросило {type(exc).__name__}, ожидался ValueError"
        return f"chunk([1, 2, 3], {size}) должно бросать ValueError"
    source = [1, 2, 3, 4]
    parts = m.chunk(source, 2)
    parts[0][0] = 99
    if source != [1, 2, 3, 4]:
        return "части должны быть независимыми копиями: изменение части затронуло исходный список"


def t_sort_basic():
    words = ["pear", "fig", "banana", "kiwi"]
    return expect(m.sort_words(words), ["fig", "kiwi", "pear", "banana"], f"sort_words({words})")


def t_sort_no_mutation():
    words = ["bb", "a", "ab", "b"]
    snapshot = list(words)
    result = m.sort_words(words)
    if result is None:
        return "sort_words вернула None - вероятно, использован list.sort() вместо sorted()"
    if words != snapshot:
        return f"исходный список изменён: {words}"
    return expect(result, ["a", "b", "ab", "bb"], f"sort_words({snapshot})")


def t_grid_basic():
    bad = expect(m.make_grid(2, 3), [[0, 0, 0], [0, 0, 0]], "make_grid(2, 3)")
    if bad:
        return bad
    bad = expect(m.make_grid(1, 2, "x"), [["x", "x"]], "make_grid(1, 2, 'x')")
    if bad:
        return bad
    return expect(m.make_grid(0, 3), [], "make_grid(0, 3)")


def t_grid_independent():
    grid = m.make_grid(3, 2)
    grid[0][0] = 7
    if grid != [[7, 0], [0, 0], [0, 0]]:
        return f"после grid[0][0] = 7 получили {grid!r}: строки таблицы - один и тот же список"
    if len({id(row) for row in grid}) != 3:
        return "строки должны быть тремя разными объектами"


case("dedupe-basic", t_dedupe_basic)
case("chunk-basic", t_chunk_basic)
case("sort-words-basic", t_sort_basic)
case("grid-basic", t_grid_basic)
case("dedupe-edge", t_dedupe_edge)
case("chunk-edge", t_chunk_edge)
case("sort-words-no-mutation", t_sort_no_mutation)
case("grid-independent-rows", t_grid_independent)
