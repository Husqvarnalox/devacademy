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


def check_pure(func, name, data, want, approx=False):
    """Calls func on a copy, compares the result and verifies that the input was not changed."""
    original = list(data)
    given = list(data)
    got = func(given)
    if approx:
        if not isinstance(got, (int, float)) or isinstance(got, bool) or abs(got - want) > 1e-9:
            return f"{name}({original}) вернуло {got!r}, ожидалось {want!r}"
    elif got != want:
        return f"{name}({original}) вернуло {got!r}, ожидалось {want!r}"
    if given != original:
        return f"{name} изменила переданный список: было {original}, стало {given}"
    return None


def t_average_basic():
    for data, want in [([2, 4, 6], 4.0), ([1, 2], 1.5), ([5], 5.0)]:
        bad = check_pure(m.average, "average", data, want, approx=True)
        if bad:
            return bad


def t_average_edges():
    for data, want in [([], 0), ([-3, 3], 0), ([0.5, 1.5], 1.0)]:
        bad = check_pure(m.average, "average", data, want, approx=True)
        if bad:
            return bad


def t_unique_basic():
    for data, want in [([1, 2, 1, 3, 2], [1, 2, 3]), (["a", "b", "a"], ["a", "b"]), ([1, 2, 3], [1, 2, 3])]:
        bad = check_pure(m.without_duplicates, "without_duplicates", data, want)
        if bad:
            return bad


def t_unique_edges():
    for data, want in [([], []), ([7, 7, 7], [7]), ([3, 1, 3, 2, 1], [3, 1, 2])]:
        bad = check_pure(m.without_duplicates, "without_duplicates", data, want)
        if bad:
            return bad
    data = [1, 1]
    if m.without_duplicates(data) is data:
        return "without_duplicates должна вернуть новый список, а не тот же объект"


def t_top_basic():
    for data, want in [([5, 9, 1, 7, 3], [9, 7, 5]), ([10, 20, 30], [30, 20, 10]), ([4, 4, 1, 4], [4, 4, 4])]:
        bad = check_pure(m.top_three, "top_three", data, want)
        if bad:
            return bad


def t_top_edges():
    for data, want in [([], []), ([2], [2]), ([1, 2], [2, 1]), ([-1, -5, -3, -2], [-1, -2, -3])]:
        bad = check_pure(m.top_three, "top_three", data, want)
        if bad:
            return bad


def t_add_basic():
    items = ["хлеб"]
    result = m.add_unique(items, "молоко")
    if result is not True:
        return f"add_unique(['хлеб'], 'молоко') вернуло {result!r}, ожидалось True"
    if items != ["хлеб", "молоко"]:
        return f"после добавления в списке {items}, ожидалось ['хлеб', 'молоко']"
    result = m.add_unique(items, "хлеб")
    if result is not False:
        return f"add_unique повторно с 'хлеб' вернуло {result!r}, ожидалось False"
    if items != ["хлеб", "молоко"]:
        return f"повтор не должен менять список, а в нём {items}"


def t_add_same_object():
    items = []
    m.add_unique(items, 1)
    m.add_unique(items, 2)
    m.add_unique(items, 1)
    if items != [1, 2]:
        return f"функция должна менять именно переданный список; после трёх вызовов в нём {items}, ожидалось [1, 2]"


def t_add_independent():
    first = []
    second = []
    m.add_unique(first, "a")
    if second != []:
        return "изменение одного списка затронуло другой"


case("average-basic", t_average_basic)
case("unique-basic", t_unique_basic)
case("top-basic", t_top_basic)
case("add-basic", t_add_basic)
case("average-edges", t_average_edges)
case("unique-edges", t_unique_edges)
case("top-edges", t_top_edges)
case("add-same-object", t_add_same_object)
case("add-independent", t_add_independent)
