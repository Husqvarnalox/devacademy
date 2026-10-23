def initial(name):
    """Первая буква имени заглавной и точка: 'анна' -> 'А.'. Для пустого имени - пустая строка."""
    name = name.strip()
    if name == "":
        return ""
    return name[0].upper() + "."


def is_palindrome(text):
    """True, если текст читается одинаково в обе стороны без учёта регистра и пробелов."""
    cleaned = text.lower().replace(" ", "")
    return cleaned == cleaned[::-1]


def count_vowels(text):
    """Число гласных русских букв (а е ё и о у ы э ю я) в тексте, регистр не важен."""
    count = 0
    for ch in text.lower():
        if ch in "аеёиоуыэюя":
            count = count + 1
    return count


def mask_number(number):
    """Заменить звёздочками все символы, кроме последних четырёх: '1234567890' -> '******7890'."""
    if len(number) <= 4:
        return number
    return "*" * (len(number) - 4) + number[-4:]
