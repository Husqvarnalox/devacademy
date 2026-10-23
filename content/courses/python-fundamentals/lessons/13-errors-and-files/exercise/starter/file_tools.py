def parse_int(text, default=None):
    """Вернуть int(text); если строку нельзя преобразовать в целое, вернуть default."""
    pass


def percent(part, total):
    """Вернуть, сколько процентов part составляет от total. Если total равно 0, вызвать ValueError."""
    pass


def read_numbers(path):
    """Прочитать целые числа из файла (UTF-8), по одному в строке; пустые строки пропустить.
    Если строка - не число, вызвать ValueError, в сообщении указав номер строки (с 1, считая пустые) и её текст."""
    pass


def write_report(path, numbers):
    """Записать в файл (UTF-8, с перезаписью) строки «Всего: N» и «Сумма: S»."""
    pass
