class BadLineError(ValueError):
    """Строка файла не является целым числом."""

    def __init__(self, line_number, text):
        super().__init__(f"строка {line_number}: {text!r} - не целое число")
        self.line_number = line_number
        self.text = text


def parse_int(text, default=None):
    """Преобразует text в int; при неудаче возвращает default."""
    try:
        return int(text)
    except (ValueError, TypeError):
        return default


def read_numbers(path):
    """Читает целые числа по одному в строке (UTF-8); пустые строки пропускает."""
    numbers = []
    with open(path, encoding="utf-8") as source:
        for line_number, line in enumerate(source, start=1):
            text = line.strip()
            if not text:
                continue
            try:
                numbers.append(int(text))
            except ValueError:
                raise BadLineError(line_number, text) from None
    return numbers


def write_report(path, numbers):
    """Записывает отчёт: 'Всего: N' и 'Сумма: S' (UTF-8), перезаписывая файл."""
    with open(path, "w", encoding="utf-8") as target:
        target.write(f"Всего: {len(numbers)}\n")
        target.write(f"Сумма: {sum(numbers)}\n")
