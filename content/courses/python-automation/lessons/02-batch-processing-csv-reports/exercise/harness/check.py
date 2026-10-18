import json
import os
import shutil
import tempfile
import tracemalloc
from datetime import date
from decimal import Decimal
from pathlib import Path

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import csv_report as m

HEADER = "id,name,amount,date"
GOOD = [HEADER, "1,Ann,10.50,2024-01-05", "10,Zed,0.50,2024-02-10"]
MIXED = [
    HEADER,                       # 1
    "1,Ann,10.50,2024-01-05",     # 2 ok
    "x,Bob,5,2024-01-06",         # 3 id
    "2,,5,2024-01-06",            # 4 name
    "3,Cy,-1,2024-01-07",         # 5 amount
    "4,Di,1.234,2024-01-07",      # 6 amount
    "5,Ed,2,2024-13-40",          # 7 date
    "6,Fy,3",                     # 8 row (мало полей)
    "7,Gu,4,2024-02-01,extra",    # 9 row (много полей)
    "",                           # 10 пропуск
    "8,Hi,NaN,2024-02-02",        # 11 amount
    "10,Zed,0.50,2024-02-10",     # 12 ok
    "0,Zero,1,2024-02-11",        # 13 id
]


def t_parse_row():
    got = m.parse_row({"id": " 7 ", "name": " Ann ", "amount": "12.50", "date": "2024-01-05"})
    bad = expect(got, {"id": 7, "name": "Ann", "amount": Decimal("12.50"), "date": date(2024, 1, 5)}, "parse_row(корректная строка)")
    if bad:
        return bad
    cases = [
        ({"id": "a", "name": "n", "amount": "1", "date": "2024-01-01"}, "id"),
        ({"id": "-3", "name": "n", "amount": "1", "date": "2024-01-01"}, "id"),
        ({"id": "1", "name": "  ", "amount": "1", "date": "2024-01-01"}, "name"),
        ({"id": "1", "name": "n", "amount": "abc", "date": "2024-01-01"}, "amount"),
        ({"id": "1", "name": "n", "amount": "Infinity", "date": "2024-01-01"}, "amount"),
        ({"id": "1", "name": "n", "amount": "1", "date": "01.01.2024"}, "date"),
    ]
    for row, field in cases:
        try:
            m.parse_row(row)
        except m.RowError as exc:
            if exc.field != field:
                return f"parse_row({row}): поле {exc.field!r}, ожидалось {field!r}"
        else:
            return f"parse_row({row}) должна поднять RowError"


def t_process_basic():
    summary, errors = m.process_csv(GOOD)
    want = {"rows_total": 2, "rows_ok": 2, "rows_bad": 0, "total": "11.00", "by_month": {"2024-01": "10.50", "2024-02": "0.50"}}
    return expect(summary, want, "process_csv(GOOD)[0]") or expect(errors, [], "process_csv(GOOD)[1]")


def t_write_reports():
    root = Path(tempfile.mkdtemp(dir=os.getcwd()))
    try:
        summary = {"rows_total": 1, "rows_ok": 0, "rows_bad": 1, "total": "0.00", "by_month": {}}
        errors = [{"line": 2, "field": "name", "error": "пустое имя, \"кавычки\""}]
        m.write_reports(summary, errors, root / "out" / "r")
        files = sorted(p.name for p in (root / "out" / "r").iterdir())
        if files != ["errors.csv", "summary.json"]:
            return f"в каталоге отчётов файлы {files!r}, ожидалось ['errors.csv', 'summary.json']"
        text = (root / "out" / "r" / "summary.json").read_text(encoding="utf-8")
        if json.loads(text) != summary or not text.endswith("\n") or "\n  " not in text:
            return "summary.json должен быть JSON с indent=2 и переводом строки в конце"
        raw = (root / "out" / "r" / "errors.csv").read_bytes().decode("utf-8")
        if raw != 'line,field,error\n2,name,"пустое имя, ""кавычки"""\n':
            return f"errors.csv содержит {raw!r}"
        m.write_reports(summary, [], root / "out" / "r")
        raw = (root / "out" / "r" / "errors.csv").read_text(encoding="utf-8")
        if raw != "line,field,error\n":
            return "при отсутствии ошибок в errors.csv должен остаться заголовок"
    finally:
        shutil.rmtree(root, ignore_errors=True)


def t_bad_rows():
    summary, errors = m.process_csv(MIXED)
    got = [(e["line"], e["field"]) for e in errors]
    want = [(3, "id"), (4, "name"), (5, "amount"), (6, "amount"), (7, "date"), (8, "row"), (9, "row"), (11, "amount"), (13, "id")]
    bad = expect(got, want, "ошибки (line, field)")
    if bad:
        return bad
    if any(not isinstance(e["error"], str) or not e["error"] for e in errors):
        return "у каждой ошибки должен быть непустой текст"
    return expect(
        summary,
        {"rows_total": 11, "rows_ok": 2, "rows_bad": 9, "total": "11.00", "by_month": {"2024-01": "10.50", "2024-02": "0.50"}},
        "summary для файла с ошибками",
    )


def t_bom_and_header():
    summary, _ = m.process_csv(["﻿" + GOOD[0]] + GOOD[1:])
    if summary["rows_ok"] != 2:
        return "BOM в начале первой строки должен убираться"
    for bad_lines in ([], ["a,b,c"], ["id,name,amount"]):
        try:
            m.process_csv(bad_lines)
        except ValueError:
            continue
        return f"process_csv({bad_lines!r}) должен поднять ValueError (неверный или пустой заголовок)"
    # поле в кавычках с переводом строки внутри: строка остаётся одной записью
    summary, errors = m.process_csv([HEADER, '1,"Ann', 'Lee",5,2024-01-01', "2,,5,2024-01-01"])
    return expect((summary["rows_ok"], [e["line"] for e in errors]), (1, [4]), "многострочное поле (line - последняя физическая строка записи)")


def t_max_errors():
    lines = [HEADER] + [f"x{i},n,1,2024-01-01" for i in range(50)] + ["1,ok,2.00,2024-03-01"]
    summary, errors = m.process_csv(lines, max_errors=5)
    if len(errors) != 5 or errors[0]["line"] != 2:
        return f"в errors должно быть ровно 5 первых ошибок, получено {len(errors)}"
    return expect((summary["rows_bad"], summary["rows_ok"], summary["total"]), (50, 1, "2.00"), "счётчики при усечении")


def t_streaming():
    def gen():
        yield HEADER
        for i in range(1, 20001):
            yield f"{i},name{i},{i % 100}.25,2024-0{i % 9 + 1}-15"

    tracemalloc.start()
    try:
        summary, errors = m.process_csv(gen())
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    if summary["rows_ok"] != 20000 or errors:
        return f"неверный результат на большом потоке: {summary['rows_ok']} строк, {len(errors)} ошибок"
    if peak > 500_000:
        return f"пиковая память {peak // 1000} КБ: входной поток нельзя превращать в список (list(lines), readlines, read)"


def t_run_file():
    root = Path(tempfile.mkdtemp(dir=os.getcwd()))
    try:
        src = root / "in.csv"
        src.write_bytes("﻿id,name,amount,date\r\n1,Анна,5.10,2024-05-01\r\n2,,1,2024-05-02\r\n".encode("utf-8"))
        summary = m.run(src, root / "out")
        bad = expect(summary["rows_ok"], 1, "run(...)['rows_ok']")
        if bad:
            return bad
        errs = (root / "out" / "errors.csv").read_text(encoding="utf-8").splitlines()
        return expect(errs[0] + "|" + errs[1].split(",")[0] + "|" + errs[1].split(",")[1], "line,field,error|3|name", "errors.csv после run")
    finally:
        shutil.rmtree(root, ignore_errors=True)


case("parse-row", t_parse_row)
case("process-basic", t_process_basic)
case("write-reports", t_write_reports)
case("bad-rows", t_bad_rows)
case("bom-and-header", t_bom_and_header)
case("max-errors", t_max_errors)
case("streaming", t_streaming)
case("run-file", t_run_file)
