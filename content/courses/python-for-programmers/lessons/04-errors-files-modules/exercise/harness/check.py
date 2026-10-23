import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import errors_task as m


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


import contextlib
import importlib
import io
import os
import tempfile


def with_file(text):
    """Создать временный файл с текстом; вернуть путь (файл удаляется в конце проверки)."""
    directory = tempfile.mkdtemp()
    path = os.path.join(directory, "app.conf")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return path


def t_parse_basic():
    bad = expect(m.parse_int("42"), 42, "parse_int('42')")
    bad = bad or expect(m.parse_int("abc"), None, "parse_int('abc')")
    return bad or expect(m.parse_int("x", -1), -1, "parse_int('x', -1)")


def t_parse_edges():
    for args, want in [((" 7 ",), 7), (("-3",), -3), (("",), None), (("3.5", 0), 0), ((None, 9), 9), (("4_2",), 42)]:
        bad = expect(m.parse_int(*args), want, f"parse_int{args!r}")
        if bad:
            return bad


def t_config_basic():
    path = with_file("host=localhost\nport = 8080\n")
    return expect(m.read_config(path), {"host": "localhost", "port": "8080"}, "read_config(простой файл)")


def t_config_edges():
    path = with_file("# настройки\n\nhost = a=b\n  name = Мир  \nport=80\nport=81\n")
    bad = expect(m.read_config(path), {"host": "a=b", "name": "Мир", "port": "81"}, "read_config(комментарии, '=' в значении, повтор ключа)")
    if bad:
        return bad
    missing = os.path.join(tempfile.mkdtemp(), "no-such-file.conf")
    bad = expect(m.read_config(missing), {}, "read_config(несуществующий файл)")
    if bad:
        return bad
    broken = with_file("a=1\n\nбез равно\nb=2\n")
    try:
        m.read_config(broken)
    except ValueError as exc:
        if "строка 3" not in str(exc):
            return f"сообщение ValueError должно содержать 'строка 3', получено {str(exc)!r}"
        return None
    return "для строки без '=' ожидался ValueError"


def t_total_basic():
    return expect(m.total_valid(["1", "x", "3"]), (4, 1), "total_valid(['1', 'x', '3'])")


def t_total_edges():
    bad = expect(m.total_valid([]), (0, 0), "total_valid([])")
    bad = bad or expect(m.total_valid(["", " 5 ", "-2", "1.5", "7_0"]), (73, 2), "total_valid(['', ' 5 ', '-2', '1.5', '7_0'])")
    return bad or expect(m.total_valid(iter(["10", "z"])), (10, 1), "total_valid(итератор)")


def t_write_basic():
    path = os.path.join(tempfile.mkdtemp(), "out.txt")
    count = m.write_lines(path, ["a", "b"])
    with open(path, encoding="utf-8") as f:
        text = f.read()
    return expect((count, text), (2, "a\nb\n"), "write_lines(path, ['a', 'b'])")


def t_write_roundtrip():
    path = os.path.join(tempfile.mkdtemp(), "out.txt")
    m.write_lines(path, ["старое", "содержимое"])
    count = m.write_lines(path, ["Привет", "мир"])
    with open(path, "rb") as f:
        raw = f.read().replace(b"\r\n", b"\n")  # Windows writes \r\n in text mode
    bad = expect((count, raw), (2, "Привет\nмир\n".encode("utf-8")), "write_lines: перезапись в UTF-8")
    return bad or expect(m.write_lines(path, []), 0, "write_lines(path, [])")


def t_import_clean():
    if not callable(getattr(m, "main", None)):
        return "в модуле должна быть функция main()"
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        importlib.reload(m)
    if buffer.getvalue():
        return f"при импорте модуль напечатал {buffer.getvalue()!r}; вызов main() должен быть под if __name__ == '__main__'"


case("parse-basic", t_parse_basic)
case("config-basic", t_config_basic)
case("total-basic", t_total_basic)
case("write-basic", t_write_basic)
case("parse-edges", t_parse_edges)
case("config-edges", t_config_edges)
case("total-edges", t_total_edges)
case("write-roundtrip", t_write_roundtrip)
case("import-clean", t_import_clean)
