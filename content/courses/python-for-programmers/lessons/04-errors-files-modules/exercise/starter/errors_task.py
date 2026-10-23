def parse_int(text, default=None):
    """Вернуть int(text) или default, если это не число."""
    pass


def read_config(path):
    """Прочитать файл 'ключ = значение' в словарь; нет файла - пустой словарь."""
    pass


def total_valid(lines):
    """Вернуть (сумма целых, сколько строк пропущено)."""
    pass


def write_lines(path, lines):
    """Записать строки в файл (UTF-8) и вернуть их число."""
    pass


def main():
    print(total_valid(["1", "x", "3"]))


main()
