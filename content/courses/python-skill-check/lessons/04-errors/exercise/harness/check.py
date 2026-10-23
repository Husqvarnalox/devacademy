import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import errors_check as m


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


import os
import tempfile


def with_file(text):
    directory = tempfile.mkdtemp()
    path = os.path.join(directory, "data.txt")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return path


def raises(func, *args, **kwargs):
    try:
        func(*args, **kwargs)
    except ValueError as exc:
        return exc
    except Exception as exc:
        return f"получено {type(exc).__name__} вместо ValueError"
    return None


def t_div():
    bad = expect(m.safe_div(6, 3), 2, "safe_div(6, 3)")
    bad = bad or expect(m.safe_div(1, 0), None, "safe_div(1, 0)")
    return bad or expect(m.safe_div(0, 5), 0, "safe_div(0, 5)")


def t_percent():
    got = m.parse_percent("45%")
    if not isinstance(got, float) or abs(got - 0.45) > 1e-12:
        return f"parse_percent('45%') вернуло {got!r}, ожидалось 0.45"
    got = m.parse_percent(" 7.5 % ")
    if abs(got - 0.075) > 1e-12:
        return f"parse_percent(' 7.5 % ') вернуло {got!r}, ожидалось 0.075"


def t_percent_errors():
    for text in ["45", "abc%", "", "%", "101%", "-1%", "5 percent"]:
        result = raises(m.parse_percent, text)
        if not isinstance(result, ValueError):
            return f"parse_percent({text!r}) должна вызвать ValueError" + (f" ({result})" if isinstance(result, str) else "")
    for text, want in [("0%", 0.0), ("100%", 1.0)]:
        got = m.parse_percent(text)
        if abs(got - want) > 1e-12:
            return f"parse_percent({text!r}) вернуло {got!r}, ожидалось {want!r}"


def t_load_basic():
    path = with_file("1\n2.5\n\n-3\n")
    return expect(m.load_numbers(path), [1.0, 2.5, -3.0], "load_numbers(файл с пустой строкой)")


def t_load_skip():
    path = with_file("1\nabc\n\n4\n")
    return expect(m.load_numbers(path), [1.0, 4.0], "load_numbers(плохая строка, strict=False)")


def t_load_strict():
    path = with_file("1\n\nabc\n4\n")
    result = raises(m.load_numbers, path, strict=True)
    if not isinstance(result, ValueError):
        return "strict=True: для плохой строки ожидался ValueError" + (f" ({result})" if isinstance(result, str) else "")
    if "строка 3" not in str(result):
        return f"сообщение должно содержать 'строка 3', получено {str(result)!r}"
    return expect(m.load_numbers(with_file("1\n2\n"), strict=True), [1.0, 2.0], "load_numbers(корректный файл, strict=True)")


def t_load_missing():
    missing = os.path.join(tempfile.mkdtemp(), "none.txt")
    try:
        m.load_numbers(missing)
    except FileNotFoundError:
        return None
    except Exception as exc:
        return f"получено {type(exc).__name__} вместо FileNotFoundError"
    return "для несуществующего файла ожидался FileNotFoundError, а не результат"


case("div", t_div)
case("percent", t_percent)
case("load-basic", t_load_basic)
case("percent-errors", t_percent_errors)
case("load-skip", t_load_skip)
case("load-strict", t_load_strict)
case("load-missing", t_load_missing)
