def is_palindrome(text):
    """Палиндром без учёта регистра и всех символов, кроме букв и цифр."""
    cleaned = [ch.lower() for ch in text if ch.isalnum()]
    return cleaned == cleaned[::-1]


def initials(full_name):
    """'Иван Петрович Сидоров' -> 'И.П.С.'"""
    return "".join(word[0].upper() + "." for word in full_name.split())


def format_row(name, qty, price):
    """Строка таблицы: имя слева (10), количество справа (4), цена справа (8, два знака)."""
    return f"{name:<10}|{qty:>4}|{price:>8.2f}"


def reverse_words(text):
    """Меняет порядок слов; пробелы между словами нормализуются до одного."""
    return " ".join(reversed(text.split()))
