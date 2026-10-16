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


from datetime import datetime, timezone

import time_task as m

BERLIN = "Europe/Berlin"
NY = "America/New_York"


def t_parse_basic():
    dt = m.parse_iso("2024-03-10T12:30:00+02:00")
    if dt.utcoffset() is None:
        return "parse_iso должен возвращать aware datetime"
    bad = expect(m.to_utc_iso(dt), "2024-03-10T10:30:00Z", "to_utc_iso(parse_iso('2024-03-10T12:30:00+02:00'))")
    if bad:
        return bad
    bad = expect(m.to_utc_iso(m.parse_iso("2024-03-10T10:30:00Z")), "2024-03-10T10:30:00Z", "Z на входе")
    if bad:
        return bad
    return expect(m.to_utc_iso(m.parse_iso("2024-01-01T00:00:00-05:00")), "2024-01-01T05:00:00Z", "отрицательное смещение")


def t_parse_errors():
    for text in ("2024-03-10T12:30:00", "not a date", ""):
        try:
            m.parse_iso(text)
        except ValueError:
            continue
        return f"parse_iso({text!r}) должен поднимать ValueError"
    try:
        m.to_utc_iso(datetime(2024, 3, 10, 12, 0))
    except ValueError:
        pass
    else:
        return "to_utc_iso(naive) должен поднимать ValueError"
    for call in (
        lambda: m.convert_zone("2024-03-10T12:00:00Z", "Mars/Base"),
        lambda: m.local_to_utc("2024-03-10T12:00:00", "Mars/Base"),
        lambda: m.local_to_utc("2024-03-10T12:00:00+01:00", BERLIN),
    ):
        try:
            call()
        except ValueError:
            continue
        return "неизвестный пояс или время со смещением должны давать ValueError"


def t_convert_basic():
    bad = expect(m.convert_zone("2024-07-01T12:00:00Z", BERLIN), "2024-07-01T14:00:00+02:00", "convert_zone летом")
    if bad:
        return bad
    bad = expect(m.convert_zone("2024-01-15T12:00:00+03:00", NY), "2024-01-15T04:00:00-05:00", "convert_zone зимой")
    if bad:
        return bad
    return expect(m.convert_zone("2024-07-01T12:00:00+02:00", "UTC"), "2024-07-01T10:00:00+00:00", "convert_zone в UTC")


def t_convert_dst():
    bad = expect(m.convert_zone("2024-03-31T00:30:00Z", BERLIN), "2024-03-31T01:30:00+01:00", "до перехода")
    if bad:
        return bad
    bad = expect(m.convert_zone("2024-03-31T01:30:00Z", BERLIN), "2024-03-31T03:30:00+02:00", "после перехода (02:xx пропущено)")
    if bad:
        return bad
    return expect(m.convert_zone("2024-10-27T01:30:00Z", BERLIN), "2024-10-27T02:30:00+01:00", "осенний переход")


def t_local_basic():
    bad = expect(m.local_to_utc("2024-07-01T14:00:00", BERLIN), "2024-07-01T12:00:00Z", "local_to_utc летом")
    if bad:
        return bad
    return expect(m.local_to_utc("2024-01-15T04:00:00", NY), "2024-01-15T09:00:00Z", "local_to_utc зимой")


def t_local_gap_fold():
    for text, zone, what in (
        ("2024-03-31T02:30:00", BERLIN, "несуществующее время"),
        ("2024-10-27T02:30:00", BERLIN, "повторяющееся время"),
        ("2024-03-10T02:30:00", NY, "несуществующее время в Нью-Йорке"),
        ("2024-11-03T01:30:00", NY, "повторяющееся время в Нью-Йорке"),
    ):
        try:
            got = m.local_to_utc(text, zone)
        except ValueError:
            continue
        return f"local_to_utc({text!r}, {zone!r}) - {what}: вернуло {got!r}, ожидался ValueError"
    return expect(m.local_to_utc("2024-03-31T03:00:00", BERLIN), "2024-03-31T01:00:00Z", "время сразу после перехода")


def t_wall_basic():
    bad = expect(m.add_wall_days("2024-05-10T09:15:00", BERLIN, 3), "2024-05-13T09:15:00+02:00", "add_wall_days без перехода")
    if bad:
        return bad
    bad = expect(m.elapsed_hours("2024-05-10T09:00:00", "2024-05-11T09:00:00", BERLIN), 24.0, "elapsed_hours за сутки")
    if bad:
        return bad
    return expect(m.elapsed_hours("2024-05-10T09:00:00", "2024-05-10T10:30:00", BERLIN), 1.5, "elapsed_hours 1.5 часа")


def t_wall_dst():
    bad = expect(
        m.add_wall_days("2024-03-30T12:00:00", BERLIN, 1),
        "2024-03-31T12:00:00+02:00",
        "add_wall_days через весенний переход",
    )
    if bad:
        return bad
    bad = expect(
        m.add_wall_days("2024-10-26T12:00:00", BERLIN, 1),
        "2024-10-27T12:00:00+01:00",
        "add_wall_days через осенний переход",
    )
    if bad:
        return bad
    return expect(m.add_wall_days("2024-03-10T12:00:00", NY, -1), "2024-03-09T12:00:00-05:00", "отрицательные дни, Нью-Йорк")


def t_elapsed_dst():
    bad = expect(m.elapsed_hours("2024-03-30T12:00:00", "2024-03-31T12:00:00", BERLIN), 23.0, "сутки через весенний переход")
    if bad:
        return bad
    bad = expect(m.elapsed_hours("2024-10-26T12:00:00", "2024-10-27T12:00:00", BERLIN), 25.0, "сутки через осенний переход")
    if bad:
        return bad
    return expect(m.elapsed_hours("2024-03-10T00:00:00", "2024-03-10T12:00:00", NY), 11.0, "половина суток в день перехода (Нью-Йорк)")


case("parse-basic", t_parse_basic)
case("convert-basic", t_convert_basic)
case("local-basic", t_local_basic)
case("wall-basic", t_wall_basic)
case("parse-errors", t_parse_errors)
case("convert-dst", t_convert_dst)
case("local-gap-fold", t_local_gap_fold)
case("wall-dst", t_wall_dst)
case("elapsed-dst", t_elapsed_dst)
