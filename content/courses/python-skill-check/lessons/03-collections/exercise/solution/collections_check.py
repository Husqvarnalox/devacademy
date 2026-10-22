def word_freq(text):
    """Подсчитать слова (без учёта регистра и знаков .,!?;: по краям)."""
    counts = {}
    for raw in text.split():
        word = raw.strip(".,!?;:").lower()
        if word:
            counts[word] = counts.get(word, 0) + 1
    return counts


def invert(mapping):
    """Вернуть значение -> отсортированный список ключей."""
    result = {}
    for key, value in mapping.items():
        result.setdefault(value, []).append(key)
    for keys in result.values():
        keys.sort()
    return result


def common(a, b):
    """Вернуть отсортированный список общих элементов без повторов."""
    return sorted(set(a) & set(b))
