import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import typing_check as m


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


import collections.abc
import types
import typing


def hints(func):
    try:
        return typing.get_type_hints(func)
    except Exception as exc:
        raise AssertionError(f"get_type_hints({func.__name__}) не сработал: {type(exc).__name__}: {exc}")


def t_first_basic():
    for args, want in [(([5, 6], 0), 5), (([], "x"), "x"), (("abc", "z"), "a"), (((), None), None)]:
        bad = expect(m.first_or(*args), want, f"first_or{args!r}")
        if bad:
            return bad


def t_first_edges():
    bad = expect(m.first_or([0], 9), 0, "first_or([0], 9): ложный элемент не равен отсутствию")
    bad = bad or expect(m.first_or([[], [1]], None), [], "first_or([[], [1]], None)")
    return bad or expect(m.first_or((7, 8), 0), 7, "first_or((7, 8), 0)")


def t_hints_first():
    h = hints(m.first_or)
    missing = [k for k in ("items", "default", "return") if k not in h]
    if missing:
        return f"нет аннотаций: {', '.join(missing)}"
    t = h["default"]
    if not isinstance(t, typing.TypeVar):
        return f"default должен иметь тип-параметр T, а не {t!r}"
    if h["return"] is not t:
        return "результат first_or должен иметь тот же параметр типа T, что и default"
    if t not in typing.get_args(h["items"]):
        return f"items должен быть Sequence[T], получено {h['items']!r}"
    if typing.get_origin(h["items"]) is not collections.abc.Sequence:
        return f"items должен быть Sequence[T], получено {h['items']!r}"


def t_group_basic():
    got = m.group_by(["apple", "avocado", "banana", "blueberry", "cherry"], key=lambda s: s[0])
    want = {"a": ["apple", "avocado"], "b": ["banana", "blueberry"], "c": ["cherry"]}
    bad = expect(got, want, "group_by(слова, key=первая буква)")
    return bad or expect(list(got), ["a", "b", "c"], "порядок ключей group_by")


def t_group_edges():
    bad = expect(m.group_by([], len), {}, "group_by([], len)")
    bad = bad or expect(m.group_by(range(6), lambda n: n % 3), {0: [0, 3], 1: [1, 4], 2: [2, 5]}, "group_by(range(6), n % 3)")
    return bad or expect(m.group_by((w for w in ["bb", "a", "cc"]), len), {2: ["bb", "cc"], 1: ["a"]}, "group_by(генератор, len)")


def t_hints_group():
    h = hints(m.group_by)
    missing = [k for k in ("items", "key", "return") if k not in h]
    if missing:
        return f"нет аннотаций: {', '.join(missing)}"
    ret = h["return"]
    if typing.get_origin(ret) is not dict or len(typing.get_args(ret)) != 2:
        return f"результат должен быть dict[K, list[T]], получено {ret!r}"
    k, values = typing.get_args(ret)
    if not isinstance(k, typing.TypeVar) or typing.get_origin(values) is not list:
        return f"результат должен быть dict[K, list[T]], получено {ret!r}"
    t = typing.get_args(values)[0]
    if not isinstance(t, typing.TypeVar) or t is k:
        return f"в dict[K, list[T]] K и T - разные параметры типа, получено {ret!r}"
    if typing.get_origin(h["key"]) is not collections.abc.Callable:
        return f"key должен быть Callable[[T], K], получено {h['key']!r}"
    args, result = typing.get_args(h["key"])
    if list(args) != [t] or result is not k:
        return f"key должен быть Callable[[T], K] с теми же T и K, что в результате, получено {h['key']!r}"
    if t not in typing.get_args(h["items"]):
        return f"items должен быть Iterable[T], получено {h['items']!r}"


def t_pair_basic():
    bad = expect(m.parse_pair("a=1"), ("a", 1), "parse_pair('a=1')")
    return bad or expect(m.parse_pair(" width = 42 "), ("width", 42), "parse_pair(' width = 42 ')")


def t_pair_edges():
    for text, want in [("", None), ("abc", None), ("=5", None), ("a=", None), ("a=x", None), ("a=1.5", None),
                       ("k=-3", ("k", -3)), ("a=b=1", None), ("  =  ", None)]:
        bad = expect(m.parse_pair(text), want, f"parse_pair({text!r})")
        if bad:
            return bad


def t_hints_pair():
    h = hints(m.parse_pair)
    if h.get("text") is not str:
        return f"text должен быть аннотирован как str, получено {h.get('text')!r}"
    if "return" not in h:
        return "нет аннотации результата"
    ret = h["return"]
    if typing.get_origin(ret) not in (typing.Union, types.UnionType):
        return f"результат должен быть объединением tuple[str, int] | None, получено {ret!r}"
    args = typing.get_args(ret)
    if type(None) not in args or tuple[str, int] not in args:
        return f"результат должен быть tuple[str, int] | None, получено {ret!r}"


case("first-basic", t_first_basic)
case("group-basic", t_group_basic)
case("pair-basic", t_pair_basic)
case("hints-first", t_hints_first)
case("first-edges", t_first_edges)
case("group-edges", t_group_edges)
case("pair-edges", t_pair_edges)
case("hints-group", t_hints_group)
case("hints-pair", t_hints_pair)
