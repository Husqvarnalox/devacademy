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


import logstats as m

LOG = [
    "2024-01-05 INFO web started",
    "2024-01-05 ERROR db connection timeout",
    "2024-01-05 ERROR web 500 on /pay",
    "2024-01-06 WARN db slow query",
    "2024-01-06 ERROR db deadlock detected",
    "2024-01-06 ERROR cache miss storm",
]


def t_parse_basic():
    e = m.parse("2024-01-05 WARN web slow  response")
    bad = expect(tuple(e), ("2024-01-05", "WARN", "web", "slow  response"), "parse(...) как кортеж")
    if bad:
        return bad
    if e.level != "WARN" or e.service != "web" or e.date != "2024-01-05" or e.message != "slow  response":
        return "поля Entry должны называться date, level, service, message"
    if not isinstance(e, tuple):
        return "Entry должен быть namedtuple (подкласс tuple)"


def t_parse_edge():
    bad = expect(m.parse("d INFO api").message, "", "parse('d INFO api').message")
    if bad:
        return bad
    for line in ("", "d INFO"):
        try:
            m.parse(line)
        except ValueError:
            continue
        return f"parse({line!r}) должен поднимать ValueError"


def t_count_levels():
    got = m.count_levels(LOG)
    bad = expect(dict(got), {"INFO": 1, "ERROR": 4, "WARN": 1}, "count_levels(LOG)")
    if bad:
        return bad
    if got["NOPE"] != 0:
        return "для отсутствующего уровня Counter должен давать 0"


def t_top_errors():
    bad = expect(m.top_error_services(LOG, 1), [("db", 2)], "top_error_services(LOG, 1)")
    if bad:
        return bad
    return expect(m.top_error_services(LOG, 10), [("db", 2), ("web", 1), ("cache", 1)], "top_error_services(LOG, 10)")


def t_top_tie_order():
    lines = ["d ERROR b x", "d ERROR a x", "d ERROR a x", "d ERROR b x", "d ERROR c x"]
    return expect(m.top_error_services(lines, 3), [("b", 2), ("a", 2), ("c", 1)], "top_error_services с равными счётчиками")


def t_group_basic():
    got = m.messages_by_service(LOG)
    return expect(
        got,
        {
            "web": ["started", "500 on /pay"],
            "db": ["connection timeout", "slow query", "deadlock detected"],
            "cache": ["miss storm"],
        },
        "messages_by_service(LOG)",
    )


def t_group_plain_dict():
    got = m.messages_by_service(LOG)
    if type(got) is not dict:
        return f"нужен обычный dict, получен {type(got).__name__}"
    try:
        got["nope"]
    except KeyError:
        pass
    else:
        return "чтение отсутствующего ключа должно поднимать KeyError"
    if "nope" in got:
        return "чтение не должно вставлять ключ"
    return expect(m.messages_by_service([]), {}, "messages_by_service([])")


def t_last_lines():
    bad = expect(m.last_lines(["a", "b", "c"], 2), ["b", "c"], "last_lines(['a','b','c'], 2)")
    if bad:
        return bad
    bad = expect(m.last_lines(["a", "b"], 5), ["a", "b"], "last_lines(['a','b'], 5)")
    if bad:
        return bad
    bad = expect(m.last_lines(["a", "b"], 0), [], "last_lines(['a','b'], 0)")
    if bad:
        return bad
    return expect(m.last_lines(iter("abcd"), 3), ["b", "c", "d"], "last_lines(iter('abcd'), 3)")


case("parse-basic", t_parse_basic)
case("count-levels", t_count_levels)
case("top-errors", t_top_errors)
case("group-basic", t_group_basic)
case("parse-edge", t_parse_edge)
case("top-tie-order", t_top_tie_order)
case("group-plain-dict", t_group_plain_dict)
case("last-lines", t_last_lines)
