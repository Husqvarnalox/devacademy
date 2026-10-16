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
    if actual != expected:
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
    for args, want in [(("  42 ",), 42), (("3.5", -1), -1), (("", 5), 5), ((None, 9), 9), (("0x10", "x"), "x"), (("+8",), 8)]:
        bad = expect(m.parse_int(*args), want, f"parse_int{args}")
        if bad:
            return bad


def t_read_basic():
    path = make_file("da_numbers_1.txt", "1\n2\n30\n")
    try:
        return expect(m.read_numbers(path), [1, 2, 30], "read_numbers(файл с 1, 2, 30)")
    finally:
        path.unlink(missing_ok=True)


def t_read_bad_line():
    path = make_file("da_numbers_2.txt", "10\n20\nпять\n40\n")
    try:
        try:
            m.read_numbers(path)
        except m.BadLineError as exc:
            if not isinstance(exc, ValueError):
                return "BadLineError должен наследоваться от ValueError"
            if getattr(exc, "line_number", None) != 3 or getattr(exc, "text", None) != "пять":
                return f"у исключения должны быть атрибуты line_number=3 и text='пять', получено {getattr(exc, 'line_number', None)!r}, {getattr(exc, 'text', None)!r}"
            return None
        return "для файла с нечисловой строкой ожидалось исключение BadLineError"
    finally:
        path.unlink(missing_ok=True)


def t_read_blank_and_signs():
    path = make_file("da_numbers_3.txt", "\n  +7 \n\n-3\n   \n0")
    try:
        bad = expect(m.read_numbers(path), [7, -3, 0], "read_numbers(файл с пустыми строками и знаками)")
        if bad:
            return bad
    finally:
        path.unlink(missing_ok=True)
    path = make_file("da_numbers_4.txt", "1\n\n\n2.5\n")
    try:
        try:
            m.read_numbers(path)
        except m.BadLineError as exc:
            return expect(exc.line_number, 4, "line_number при пустых строках перед ошибкой (нумерация физических строк с 1)")
        return "ожидалось BadLineError для строки '2.5'"
    finally:
        path.unlink(missing_ok=True)


def t_read_missing():
    try:
        m.read_numbers("da_no_such_file.txt")
    except FileNotFoundError:
        return None
    except Exception as exc:
        return f"отсутствующий файл должен давать FileNotFoundError, а не {type(exc).__name__}"
    return "для отсутствующего файла ожидалось FileNotFoundError (его не нужно перехватывать)"


def t_write_basic():
    path = Path("da_report_1.txt")
    try:
        m.write_report(path, [1, 2, 3])
        return expect(path.read_bytes().decode("utf-8"), "Всего: 3\nСумма: 6\n", "содержимое отчёта")
    finally:
        path.unlink(missing_ok=True)


def t_write_overwrite():
    path = make_file("da_report_2.txt", "старое содержимое, которое длиннее нового отчёта и не должно остаться\n" * 3)
    try:
        m.write_report(path, [])
        bad = expect(path.read_bytes().decode("utf-8"), "Всего: 0\nСумма: 0\n", "содержимое отчёта для пустого списка")
        if bad:
            return bad
        m.write_report(str(path), [-5, 5, 10])
        return expect(path.read_bytes().decode("utf-8"), "Всего: 3\nСумма: 10\n", "содержимое после повторной записи")
    finally:
        path.unlink(missing_ok=True)


case("parse-int-basic", t_parse_basic)
case("read-numbers-basic", t_read_basic)
case("read-numbers-bad-line", t_read_bad_line)
case("write-report-basic", t_write_basic)
case("parse-int-edge", t_parse_edge)
case("read-numbers-blank-and-signs", t_read_blank_and_signs)
case("read-numbers-missing-file", t_read_missing)
case("write-report-overwrite", t_write_overwrite)
