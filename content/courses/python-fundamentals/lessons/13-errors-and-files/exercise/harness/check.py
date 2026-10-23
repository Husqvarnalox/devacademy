import json
import os
from pathlib import Path

NONCE = os.environ["DEVACADEMY_NONCE"]

import file_tools as m


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
    if actual != expected or type(actual) is not type(expected):
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


def make_file(name, content):
    path = Path(name)
    path.write_bytes(content.encode("utf-8"))
    return path


def t_parse_basic():
    for args, want in [(("42",), 42), (("-7",), -7), (("abc",), None), (("abc", 0), 0)]:
        bad = expect(m.parse_int(*args), want, f"parse_int{args}")
        if bad:
            return bad


def t_parse_edge():
    for args, want in [(("  42 ",), 42), (("3.5", -1), -1), (("", 5), 5), (("+8",), 8), (("12a", "x"), "x")]:
        bad = expect(m.parse_int(*args), want, f"parse_int{args}")
        if bad:
            return bad


def t_percent_basic():
    for args, want in [((1, 4), 25.0), ((50, 200), 25.0), ((0, 10), 0.0), ((3, 3), 100.0)]:
        got = m.percent(*args)
        if not isinstance(got, (int, float)) or abs(got - want) > 1e-9:
            return f"percent{args} вернуло {got!r}, ожидалось {want!r}"


def t_percent_zero():
    try:
        m.percent(5, 0)
    except ValueError:
        return None
    return "percent(5, 0) должна вызвать ValueError (деление на ноль не должно доходить до ZeroDivisionError)"


def t_read_basic():
    path = make_file("da_numbers_1.txt", "1\n2\n30\n")
    try:
        return expect(m.read_numbers("da_numbers_1.txt"), [1, 2, 30], "read_numbers(файл с 1, 2, 30)")
    finally:
        path.unlink(missing_ok=True)


def t_read_blank():
    path = make_file("da_numbers_2.txt", "\n  +7 \n\n-3\n   \n0")
    try:
        return expect(m.read_numbers("da_numbers_2.txt"), [7, -3, 0], "read_numbers(файл с пустыми строками и знаками)")
    finally:
        path.unlink(missing_ok=True)


def t_read_bad_line():
    path = make_file("da_numbers_3.txt", "10\n20\nпять\n40\n")
    try:
        try:
            m.read_numbers("da_numbers_3.txt")
        except ValueError as exc:
            text = str(exc)
            if "3" not in text or "пять" not in text:
                return f"сообщение ValueError должно содержать номер строки 3 и текст 'пять', получено: {text!r}"
            return None
        return "для файла с нечисловой строкой ожидалось исключение ValueError"
    finally:
        path.unlink(missing_ok=True)


def t_read_bad_line_number():
    path = make_file("da_numbers_4.txt", "1\n\n\n2.5\n")
    try:
        try:
            m.read_numbers("da_numbers_4.txt")
        except ValueError as exc:
            if "4" not in str(exc):
                return f"номер строки считается по файлу вместе с пустыми строками: ожидалось 4, сообщение: {str(exc)!r}"
            return None
        return "ожидалось ValueError для строки '2.5'"
    finally:
        path.unlink(missing_ok=True)


def t_read_missing():
    try:
        m.read_numbers("da_no_such_file.txt")
    except FileNotFoundError:
        return None
    except Exception as exc:
        return f"отсутствие файла должно приводить к FileNotFoundError, а получено {type(exc).__name__}"
    return "для несуществующего файла ожидался FileNotFoundError"


def t_write_report():
    name = "da_report.txt"
    try:
        m.write_report(name, [1, 2, 3])
        got = Path(name).read_bytes().decode("utf-8").replace("\r\n", "\n")
        bad = expect(got, "Всего: 3\nСумма: 6\n", "содержимое файла после write_report(..., [1, 2, 3])")
        if bad:
            return bad
        m.write_report(name, [])
        got = Path(name).read_bytes().decode("utf-8").replace("\r\n", "\n")
        return expect(got, "Всего: 0\nСумма: 0\n", "содержимое файла после write_report(..., [])")
    finally:
        Path(name).unlink(missing_ok=True)


def t_write_overwrites():
    name = "da_report2.txt"
    Path(name).write_bytes(("старое содержимое, которое должно исчезнуть\n" * 3).encode("utf-8"))
    try:
        m.write_report(name, [10])
        got = Path(name).read_bytes().decode("utf-8").replace("\r\n", "\n")
        return expect(got, "Всего: 1\nСумма: 10\n", "файл после перезаписи")
    finally:
        Path(name).unlink(missing_ok=True)


case("parse-basic", t_parse_basic)
case("percent-basic", t_percent_basic)
case("read-basic", t_read_basic)
case("write-report", t_write_report)
case("parse-edge", t_parse_edge)
case("percent-zero", t_percent_zero)
case("read-blank", t_read_blank)
case("read-bad-line", t_read_bad_line)
case("read-bad-line-number", t_read_bad_line_number)
case("read-missing", t_read_missing)
case("write-overwrites", t_write_overwrites)
