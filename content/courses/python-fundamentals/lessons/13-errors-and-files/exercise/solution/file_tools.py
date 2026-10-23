def parse_int(text, default=None):
    """Вернуть int(text); если строку нельзя преобразовать в целое, вернуть default."""
    try:
        return int(text)
    except ValueError:
        return default


def percent(part, total):
    """Вернуть, сколько процентов part составляет от total. Если total равно 0, вызвать ValueError."""
    if total == 0:
        raise ValueError("total must not be zero")
    return part / total * 100


def read_numbers(path):
    """Прочитать целые числа из файла (UTF-8), по одному в строке; пустые строки пропустить.
    Если строка - не число, вызвать ValueError, в сообщении указав номер строки (с 1, считая пустые) и её текст."""
    numbers = []
    line_number = 0
    with open(path, encoding="utf-8") as source:
        for line in source:
            line_number = line_number + 1
            text = line.strip()
            if text == "":
                continue
            try:
                numbers.append(int(text))
            except ValueError:
                raise ValueError(f"строка {line_number}: {text!r} - не целое число")
    return numbers


def write_report(path, numbers):
    """Записать в файл (UTF-8, с перезаписью) строки «Всего: N» и «Сумма: S»."""
    with open(path, "w", encoding="utf-8") as target:
        target.write(f"Всего: {len(numbers)}\n")
        target.write(f"Сумма: {sum(numbers)}\n")
