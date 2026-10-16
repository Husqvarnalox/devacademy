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


import datetime
import json
from decimal import Decimal

import convert_task as m


def t_csv_json_basic():
    text = 'name,city\n"Ли, Анна",Москва\nBob,Paris\n'
    out = m.csv_to_json(text)
    bad = expect(
        json.loads(out),
        [{"name": "Ли, Анна", "city": "Москва"}, {"name": "Bob", "city": "Paris"}],
        "csv_to_json(...) после json.loads",
    )
    if bad:
        return bad
    if "\\u" in out:
        return "кириллица должна остаться буквами (ensure_ascii=False)"


def t_csv_edge():
    text = 'a,b\r\n"x ""q"" y","line1\nline2"\r\n1\r\n'
    bad = expect(
        json.loads(m.csv_to_json(text)),
        [{"a": 'x "q" y', "b": "line1\nline2"}, {"a": "1", "b": ""}],
        "кавычки, перевод строки в поле, короткая строка",
    )
    if bad:
        return bad
    bad = expect(json.loads(m.csv_to_json("a,b\n")), [], "только заголовок")
    if bad:
        return bad
    bad = expect(json.loads(m.csv_to_json("")), [], "пустой текст")
    if bad:
        return bad
    try:
        m.csv_to_json("a,b\n1,2,3\n")
    except ValueError:
        return None
    return "строка длиннее заголовка должна давать ValueError"


def t_json_csv_basic():
    got = m.json_to_csv('[{"a": 1, "b": "x"}, {"a": 2, "b": "y", "c": 3.5}]')
    return expect(got, "a,b,c\n1,x,\n2,y,3.5\n", "json_to_csv(...)")


def t_json_csv_edge():
    src = [
        {"n": 'say "hi", ok', "t": True},
        {"n": "строка1\nстрока2", "t": False, "z": None},
        {"extra": "Привет"},
    ]
    got = m.json_to_csv(json.dumps(src, ensure_ascii=False))
    import csv
    import io

    rows = list(csv.reader(io.StringIO(got, newline="")))
    expected = [
        ["n", "t", "z", "extra"],
        ['say "hi", ok', "true", "", ""],
        ["строка1\nстрока2", "false", "", ""],
        ["", "", "", "Привет"],
    ]
    bad = expect(rows, expected, "разбор результата json_to_csv обратно через csv.reader")
    if bad:
        return bad
    if '"say ""hi"", ok"' not in got:
        return "значение с запятой и кавычками должно быть экранировано по правилам CSV"
    return expect(m.json_to_csv("[]"), "", "json_to_csv('[]')")


def t_stable_basic():
    bad = expect(m.dumps_stable({"b": [1, 2], "a": "я"}), '{"a":"я","b":[1,2]}', "dumps_stable({'b': [1, 2], 'a': 'я'})")
    if bad:
        return bad
    return expect(m.dumps_stable([]), "[]", "dumps_stable([])")


def t_stable_default():
    obj = {
        "d": datetime.date(2024, 3, 1),
        "dt": datetime.datetime(2024, 3, 1, 12, 30),
        "m": Decimal("19.90"),
        "s": {3, 1, 2},
        "f": frozenset({"b", "a"}),
    }
    bad = expect(
        m.dumps_stable(obj),
        '{"d":"2024-03-01","dt":"2024-03-01T12:30:00","f":["a","b"],"m":"19.90","s":[1,2,3]}',
        "dumps_stable со своими типами",
    )
    if bad:
        return bad
    try:
        m.dumps_stable({"x": b"bytes"})
    except TypeError:
        return None
    return "bytes должен давать TypeError"


def t_pack_basic():
    bad = expect(m.pack_message(1, "hi"), b"\x01\x00\x00\x00\x02hi", "pack_message(1, 'hi')")
    if bad:
        return bad
    bad = expect(m.pack_message(255, "я"), b"\xff\x00\x00\x00\x02\xd1\x8f", "pack_message(255, 'я') (длина в байтах)")
    if bad:
        return bad
    return expect(m.unpack_message(m.pack_message(7, "Привет, мир")), (7, "Привет, мир"), "круговое преобразование")


def t_unpack_errors():
    for data in (b"", b"\x01\x00\x00", b"\x01\x00\x00\x00\x05abc", b"\x01\x00\x00\x00\x01abc"):
        try:
            m.unpack_message(data)
        except ValueError:
            continue
        return f"unpack_message({data!r}) должен поднимать ValueError"
    for version in (-1, 256):
        try:
            m.pack_message(version, "x")
        except ValueError:
            continue
        return f"pack_message({version}, ...) должен поднимать ValueError"
    return expect(m.unpack_message(b"\x00\x00\x00\x00\x00"), (0, ""), "пустой текст")


def t_sum_prices():
    got = m.sum_prices('[{"price": 0.1}, {"price": 0.2}, {"price": 3}]')
    bad = expect(got, Decimal("3.3"), "sum_prices([0.1, 0.2, 3])")
    if bad:
        return bad
    if not isinstance(got, Decimal):
        return "результат должен быть Decimal"
    return expect(m.sum_prices("[]"), Decimal(0), "sum_prices('[]')")


case("csv-json-basic", t_csv_json_basic)
case("json-csv-basic", t_json_csv_basic)
case("stable-basic", t_stable_basic)
case("pack-basic", t_pack_basic)
case("csv-edge", t_csv_edge)
case("json-csv-edge", t_json_csv_edge)
case("stable-default", t_stable_default)
case("unpack-errors", t_unpack_errors)
case("sum-prices", t_sum_prices)
