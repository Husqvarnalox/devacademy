"""Потоковая обработка CSV: валидация строк, отчёт об ошибках, агрегаты."""

import csv
import json
from decimal import Decimal

COLUMNS = ("id", "name", "amount", "date")


class RowError(ValueError):
    """Ошибка валидации строки; имя поля, в котором найдена проблема, лежит в .field."""

    def __init__(self, field, message):
        super().__init__(message)
        self.field = field


def parse_row(row):
    """row - словарь строк. Вернуть {'id': int > 0, 'name': непустая str (strip), 'amount': Decimal >= 0
    не более 2 знаков после запятой и конечное, 'date': datetime.date из 'YYYY-MM-DD'}.
    Иначе поднять RowError(поле, сообщение), проверяя поля в порядке id, name, amount, date."""
    raise NotImplementedError


def process_csv(lines, max_errors=1000):
    """Потоково прочитать CSV из итерируемого объекта строк (не превращая его в список).
    Заголовок должен быть id,name,amount,date (BOM в начале убрать), иначе ValueError.
    Вернуть (summary, errors):
      summary = {'rows_total', 'rows_ok', 'rows_bad', 'total': '123.45', 'by_month': {'2024-01': '10.50', ...}}
      errors  = [{'line': номер строки в источнике (заголовок - 1), 'field': имя поля или 'row', 'error': текст}]
    Строки с лишними или недостающими полями - ошибка поля 'row'. Пустые строки пропускаются.
    В errors хранить не более max_errors записей, но rows_bad считать все."""
    raise NotImplementedError


def write_reports(summary, errors, out_dir):
    """Создать out_dir и записать summary.json (indent=2, sort_keys, ensure_ascii=False, перевод строки в конце)
    и errors.csv (колонки line,field,error, заголовок всегда, lineterminator '\\n'), UTF-8.
    Писать во временный файл и os.replace; временных файлов не оставлять."""
    raise NotImplementedError


def run(in_path, out_dir):
    """Открыть файл (utf-8-sig, newline=''), обработать потоково, записать отчёты, вернуть summary."""
    raise NotImplementedError
