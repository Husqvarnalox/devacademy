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


from typing import Any, Literal, Optional, TypedDict, Union

import typing_task as m


def all_cases(cases, label="check_type"):
    for value, tp, want in cases:
        got = m.check_type(value, tp)
        if got is not want:
            return f"{label}({value!r}, {tp!r}) вернуло {got!r}, ожидалось {want!r}"
    return None


def t_check_simple():
    return all_cases([
        (1, int, True), ("a", int, False), ("a", str, True), (None, type(None), True),
        (None, None, True), (3, int | str, True), (2.5, int | str, False),
        (None, Optional[int], True), (4, Optional[int], True), ("x", Optional[int], False),
        ("x", Union[int, str], True), (1.5, Union[int, str], False),
    ])


def t_check_containers():
    return all_cases([
        ([1, 2], list[int], True), ([1, "a"], list[int], False), ([], list[int], True),
        ((1, 2), list[int], False), ({"a": 1}, dict[str, int], True), ({"a": "b"}, dict[str, int], False),
        ({1: 1}, dict[str, int], False), ((1, 2, 3), tuple[int, ...], True), ((1, "a"), tuple[int, ...], False),
        ((1, "a"), tuple[int, str], True), ((1, "a", 2), tuple[int, str], False), ((1,), tuple[int, str], False),
        ({1, 2}, set[int], True), ({1, "a"}, set[int], False),
    ])


def t_check_literal_any_none():
    return all_cases([
        ("a", Literal["a", "b"], True), ("c", Literal["a", "b"], False), (1, Literal[1], True),
        (True, Literal[1], False), (1, Literal[True], False), (object(), Any, True), (None, Any, True),
        (None, int | None, True), (None, Optional[str], True), (0, Optional[str], False),
    ])


def t_check_nested():
    return all_cases([
        ([{"a": 1, "b": None}], list[dict[str, int | None]], True),
        ([{"a": 1, "b": "x"}], list[dict[str, int | None]], False),
        ({"k": [(1, "a"), (2, "b")]}, dict[str, list[tuple[int, str]]], True),
        ({"k": [(1, 2)]}, dict[str, list[tuple[int, str]]], False),
        ([[1], [2, 3]], list[list[int]], True), ([[1], ["x"]], list[list[int]], False),
        ([1, "a"], list[int | str], True), ([1, 2.0], list[int | str], False),
    ])


def f(a: int, b: str = "x", c=None, *rest: int, **extra: str):
    return a


def t_bad_arguments_basic():
    bad = expect(m.bad_arguments(f, "1", b=2), ["a", "b"], "bad_arguments(f, '1', b=2)")
    if bad:
        return bad
    return expect(m.bad_arguments(f, 1, "ok"), [], "bad_arguments(f, 1, 'ok')")


def t_bad_arguments_order():
    def g(x: int, y: str, z: float = 0.0, w=None):
        pass

    bad = expect(m.bad_arguments(g, 1.5, y=3), ["x", "y"], "порядок сигнатуры")
    if bad:
        return bad
    bad = expect(m.bad_arguments(g, 1, "a"), [], "значения по умолчанию не проверяются")
    if bad:
        return bad
    bad = expect(m.bad_arguments(g, 1, "a", w="любое"), [], "параметр без аннотации игнорируется")
    if bad:
        return bad
    bad = expect(m.bad_arguments(f, 1, "a", 5, "bad", k=1), [], "*args/**kwargs не проверяются")
    if bad:
        return bad
    try:
        m.bad_arguments(g)
    except TypeError:
        return None
    return "вызов без обязательных аргументов должен давать TypeError (от bind)"


def t_strict_basic():
    @m.strict
    def add(a: int, b: int) -> int:
        return a + b

    bad = expect(add(1, 2), 3, "add(1, 2)")
    if bad:
        return bad
    try:
        add("x", 2)
    except TypeError as exc:
        if "a" not in str(exc):
            return f"в сообщении TypeError должно быть имя параметра a: {exc}"
    else:
        return "add('x', 2) должен поднимать TypeError"


def t_strict_return():
    @m.strict
    def broken(a: int) -> str:
        """Документация."""
        return a

    try:
        broken(1)
    except TypeError:
        pass
    else:
        return "результат неверного типа должен давать TypeError"
    if broken.__name__ != "broken" or broken.__doc__ != "Документация.":
        return "метаданные не сохранены (functools.wraps)"

    @m.strict
    def noop(a: int) -> None:
        return None

    bad = expect(noop(1), None, "функция с -> None")
    if bad:
        return bad

    @m.strict
    def untyped(a, b=2):
        return a + b

    return expect(untyped(1), 3, "функция без аннотаций")


def t_first_generic():
    params = getattr(m.first, "__type_params__", None)
    if params is None or len(params) != 1 or params[0].__name__ != "T":
        return "first должна быть объявлена как def first[T](...): ожидается один параметр типа T в __type_params__"
    return expect(m.first([5, 6], 0), 5, "first([5, 6], 0)")


def t_first_behavior():
    bad = expect(m.first([], "d"), "d", "first([], 'd')")
    if bad:
        return bad
    bad = expect(m.first(["a"], "d"), "a", "first(['a'], 'd')")
    if bad:
        return bad
    return expect(m.first([None], 1), None, "first([None], 1)")


class Movie(TypedDict):
    title: str
    year: int


class Opt(TypedDict, total=False):
    note: str
    rating: float


def t_typeddict_basic():
    if m.matches_typeddict({"title": "A", "year": 1999}, Movie) is not True:
        return "корректный словарь должен соответствовать Movie"
    if m.matches_typeddict({"title": "A", "year": "1999"}, Movie) is not False:
        return "year должен быть int"


def t_typeddict_keys():
    if m.matches_typeddict({"title": "A"}, Movie) is not False:
        return "отсутствует обязательный ключ year"
    if m.matches_typeddict({"title": "A", "year": 1, "extra": 1}, Movie) is not False:
        return "лишний ключ extra должен приводить к False"
    if m.matches_typeddict({}, Opt) is not True:
        return "для total=False пустой словарь допустим"
    if m.matches_typeddict({"rating": 4.5}, Opt) is not True:
        return "необязательный ключ rating=4.5 допустим"
    if m.matches_typeddict({"rating": "high"}, Opt) is not False:
        return "rating должен быть float"


case("check-simple", t_check_simple)
case("check-containers", t_check_containers)
case("bad-arguments-basic", t_bad_arguments_basic)
case("strict-basic", t_strict_basic)
case("first-generic", t_first_generic)
case("typeddict-basic", t_typeddict_basic)
case("check-literal-any-none", t_check_literal_any_none)
case("check-nested", t_check_nested)
case("bad-arguments-order", t_bad_arguments_order)
case("strict-return", t_strict_return)
case("typeddict-keys", t_typeddict_keys)
case("first-behavior", t_first_behavior)
