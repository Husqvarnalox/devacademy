def safe_div(a, b):
    """a / b или None при делении на ноль."""
    try:
        return a / b
    except ZeroDivisionError:
        return None


def parse_percent(text):
    """Превратить '45%' в долю 0.45 или вызвать ValueError."""
    body = text.strip()
    if not body.endswith("%"):
        raise ValueError(f"нет знака %: {text!r}")
    value = float(body[:-1])
    if not 0 <= value <= 100:
        raise ValueError(f"процент вне диапазона: {value}")
    return value / 100


def load_numbers(path, strict=False):
    """Прочитать числа из файла; плохие строки пропустить или (strict) вызвать ValueError."""
    numbers = []
    with open(path, encoding="utf-8") as f:
        for number, raw in enumerate(f, start=1):
            line = raw.strip()
            if not line:
                continue
            try:
                numbers.append(float(line))
            except ValueError:
                if strict:
                    raise ValueError(f"строка {number}: не число {line!r}") from None
    return numbers
