def parse_int(text, default=None):
    """Вернуть int(text) или default, если это не число."""
    try:
        return int(text)
    except (ValueError, TypeError):
        return default


def read_config(path):
    """Прочитать файл 'ключ = значение' в словарь; нет файла - пустой словарь."""
    config = {}
    try:
        with open(path, encoding="utf-8") as f:
            for number, raw in enumerate(f, start=1):
                line = raw.strip()
                if not line or line.startswith("#"):
                    continue
                if "=" not in line:
                    raise ValueError(f"строка {number}: нет '='")
                key, value = line.split("=", 1)
                config[key.strip()] = value.strip()
    except FileNotFoundError:
        return {}
    return config


def total_valid(lines):
    """Вернуть (сумма целых, сколько строк пропущено)."""
    total = 0
    skipped = 0
    for line in lines:
        number = parse_int(line)
        if number is None:
            skipped += 1
        else:
            total += number
    return total, skipped


def write_lines(path, lines):
    """Записать строки в файл (UTF-8) и вернуть их число."""
    count = 0
    with open(path, "w", encoding="utf-8") as f:
        for line in lines:
            f.write(line + "\n")
            count += 1
    return count


def main():
    print(total_valid(["1", "x", "3"]))


if __name__ == "__main__":
    main()
