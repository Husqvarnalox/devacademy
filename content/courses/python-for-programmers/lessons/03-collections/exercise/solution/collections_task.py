def group_by_first(words):
    """Сгруппировать слова по первой букве (в нижнем регистре)."""
    groups = {}
    for word in words:
        if not word:
            continue
        groups.setdefault(word[0].lower(), []).append(word)
    return groups


def dedupe(items):
    """Вернуть новый список без повторов с сохранением порядка."""
    seen = set()
    result = []
    for item in items:
        if item not in seen:
            seen.add(item)
            result.append(item)
    return result


def merge_counts(a, b):
    """Вернуть новый словарь с суммой значений по ключам."""
    merged = dict(a)
    for key, value in b.items():
        merged[key] = merged.get(key, 0) + value
    return merged


def top_n(counts, n):
    """Вернуть n пар (ключ, число) по убыванию числа, затем по ключу."""
    ordered = sorted(counts.items(), key=lambda pair: (-pair[1], pair[0]))
    return ordered[:n]


def make_grid(rows, cols, fill=0):
    """Вернуть rows независимых строк по cols элементов."""
    return [[fill] * cols for _ in range(rows)]
