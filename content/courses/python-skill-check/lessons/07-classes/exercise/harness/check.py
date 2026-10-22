import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import classes_check as m


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


import dataclasses


def t_basic():
    v = m.Version(1, 2, 3)
    bad = expect((v.major, v.minor, v.patch), (1, 2, 3), "поля Version(1, 2, 3)")
    bad = bad or expect(tuple(dataclasses.astuple(m.Version(4))), (4, 0, 0), "Version(4): значения по умолчанию")
    bad = bad or expect(str(v), "1.2.3", "str(Version(1, 2, 3))")
    return bad or expect(m.Version.parse("10.0.7"), m.Version(10, 0, 7), "Version.parse('10.0.7')")


def t_is_dataclass():
    if not dataclasses.is_dataclass(m.Version):
        return "Version должен быть dataclass (декоратор @dataclass)"
    return expect(repr(m.Version(1, 2, 3)), "Version(major=1, minor=2, patch=3)", "repr(Version(1, 2, 3))")


def t_order():
    a, b, c = m.Version(1, 2, 3), m.Version(1, 10, 0), m.Version(2)
    if not (a < b < c and c > b and a <= a and b >= a):
        return "сравнение версий должно идти по major, minor, patch как по числам"
    got = sorted([c, a, b, m.Version(1, 2, 0)])
    return expect(got, [m.Version(1, 2, 0), a, b, c], "sorted(версий)")


def t_equal_hash():
    a, b = m.Version(1, 2, 3), m.Version(1, 2, 3)
    if a != b or a is b:
        return "версии с равными полями должны быть равны"
    if len({a, b, m.Version(1, 2, 4)}) != 2:
        return "версии должны быть хешируемыми, равные - с одним хешем"
    return expect({a: "x"}[b], "x", "версия как ключ словаря")


def t_frozen():
    v = m.Version(1, 2, 3)
    try:
        v.major = 5
    except dataclasses.FrozenInstanceError:
        return None
    except Exception as exc:
        return f"присваивание поля вызвало {type(exc).__name__}, ожидалось FrozenInstanceError"
    return "Version должен быть неизменяемым (frozen=True)"


def t_parse_errors():
    for text in ["1.2", "1.2.3.4", "a.b.c", "1.2.x", "", "1..3", "-1.0.0", "1.2.3 "]:
        try:
            got = m.Version.parse(text)
        except ValueError:
            continue
        except Exception as exc:
            return f"Version.parse({text!r}) вызвал {type(exc).__name__}, ожидался ValueError"
        return f"Version.parse({text!r}) вернул {got!r}, ожидался ValueError"


def t_bump():
    v = m.Version(1, 2, 3)
    cases = [("major", m.Version(2, 0, 0)), ("minor", m.Version(1, 3, 0)), ("patch", m.Version(1, 2, 4))]
    for part, want in cases:
        bad = expect(v.bump(part), want, f"Version(1, 2, 3).bump({part!r})")
        if bad:
            return bad
    bad = expect(v, m.Version(1, 2, 3), "исходная версия после bump")
    if bad:
        return bad
    try:
        v.bump("build")
    except ValueError:
        return None
    return "bump('build') должен вызывать ValueError"


def t_latest():
    versions = [m.Version(1, 2, 3), m.Version(1, 10, 0), m.Version(1, 9, 9)]
    bad = expect(m.latest(versions), m.Version(1, 10, 0), "latest(версии)")
    bad = bad or expect(m.latest(v for v in versions), m.Version(1, 10, 0), "latest(генератор)")
    return bad or expect(m.latest([]), None, "latest([])")


case("basic", t_basic)
case("order", t_order)
case("is-dataclass", t_is_dataclass)
case("equal-hash", t_equal_hash)
case("frozen", t_frozen)
case("parse-errors", t_parse_errors)
case("bump", t_bump)
case("latest", t_latest)
