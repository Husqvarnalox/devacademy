def is_palindrome(text):
    """True, если буквы и цифры читаются одинаково в обе стороны (без учёта регистра)."""
    cleaned = [ch.lower() for ch in text if ch.isalnum()]
    return cleaned == cleaned[::-1]


def caesar(text, shift):
    """Сдвинуть латинские буквы на shift позиций по кругу."""
    result = []
    for ch in text:
        if "a" <= ch <= "z":
            base = ord("a")
        elif "A" <= ch <= "Z":
            base = ord("A")
        else:
            result.append(ch)
            continue
        result.append(chr((ord(ch) - base + shift) % 26 + base))
    return "".join(result)


def truncate(text, width, ellipsis="..."):
    """Обрезать текст до width символов вместе с многоточием."""
    if width < len(ellipsis):
        raise ValueError("width меньше длины ellipsis")
    if len(text) <= width:
        return text
    return text[:width - len(ellipsis)] + ellipsis
