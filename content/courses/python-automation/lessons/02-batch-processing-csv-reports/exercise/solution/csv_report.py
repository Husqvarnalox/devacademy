"""Потоковая обработка CSV: валидация строк, отчёт об ошибках, агрегаты."""

import csv
import json
import os
from collections import defaultdict
from datetime import date
from decimal import Decimal, InvalidOperation
from pathlib import Path

COLUMNS = ("id", "name", "amount", "date")
CENT = Decimal("0.01")


class RowError(ValueError):
    """Ошибка валидации строки; поле, в котором найдена проблема, лежит в .field."""

    def __init__(self, field, message):
        super().__init__(message)
        self.field = field


def parse_row(row):
    raw = (row.get("id") or "").strip()
    try:
        row_id = int(raw)
    except ValueError:
        raise RowError("id", f"id должен быть целым числом, получено {raw!r}") from None
    if row_id <= 0:
        raise RowError("id", "id должен быть положительным")
    name = (row.get("name") or "").strip()
    if not name:
        raise RowError("name", "пустое имя")
    raw = (row.get("amount") or "").strip()
    try:
        amount = Decimal(raw)
        ok = amount.is_finite() and amount >= 0 and amount == amount.quantize(CENT)
    except InvalidOperation:
        ok = False
    if not ok:
        raise RowError("amount", f"сумма должна быть неотрицательной, не более 2 знаков после запятой: {raw!r}")
    raw = (row.get("date") or "").strip()
    try:
        if len(raw) != 10:
            raise ValueError
        day = date.fromisoformat(raw)
    except ValueError:
        raise RowError("date", f"дата должна быть в формате YYYY-MM-DD: {raw!r}") from None
    return {"id": row_id, "name": name, "amount": amount, "date": day}


def _strip_bom(lines):
    first = True
    for line in lines:
        if first:
            line = line.lstrip("﻿")
            first = False
        yield line


def process_csv(lines, max_errors=1000):
    reader = csv.DictReader(_strip_bom(lines), restkey="__extra", restval=None)
    names = tuple(n.strip() for n in (reader.fieldnames or ()))
    if names != COLUMNS:
        raise ValueError(f"неверный заголовок: {names!r}")
    reader.fieldnames = list(names)
    ok = bad = 0
    total = Decimal(0)
    by_month = defaultdict(Decimal)
    errors = []

    def fail(line, field, message):
        nonlocal bad
        bad += 1
        if len(errors) < max_errors:
            errors.append({"line": line, "field": field, "error": message})

    for row in reader:
        line = reader.line_num
        if "__extra" in row:
            fail(line, "row", "слишком много полей")
        elif None in row.values():
            fail(line, "row", "не хватает полей")
        else:
            try:
                rec = parse_row(row)
            except RowError as exc:
                fail(line, exc.field, str(exc))
            else:
                ok += 1
                total += rec["amount"]
                by_month[rec["date"].isoformat()[:7]] += rec["amount"]
    summary = {
        "rows_total": ok + bad,
        "rows_ok": ok,
        "rows_bad": bad,
        "total": f"{total:.2f}",
        "by_month": {k: f"{by_month[k]:.2f}" for k in sorted(by_month)},
    }
    return summary, errors


def _atomic_write(path, write):
    tmp = path.with_name(path.name + ".tmp")
    with open(tmp, "w", encoding="utf-8", newline="") as fh:
        write(fh)
    os.replace(tmp, path)


def write_reports(summary, errors, out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    _atomic_write(out / "summary.json", lambda fh: fh.write(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True) + "\n"))

    def write_errors(fh):
        writer = csv.DictWriter(fh, fieldnames=["line", "field", "error"], lineterminator="\n")
        writer.writeheader()
        writer.writerows(errors)

    _atomic_write(out / "errors.csv", write_errors)


def run(in_path, out_dir):
    with open(in_path, encoding="utf-8-sig", newline="") as fh:
        summary, errors = process_csv(fh)
    write_reports(summary, errors, out_dir)
    return summary
