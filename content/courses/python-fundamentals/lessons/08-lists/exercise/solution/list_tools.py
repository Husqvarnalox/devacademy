def remove_duplicates(items):
    """Новый список без повторов; порядок первых вхождений сохраняется."""
    result = []
    for item in items:
        if item not in result:
            result.append(item)
    return result


def chunk(items, size):
    """Делит items на списки длиной size; последний может быть короче."""
    if size < 1:
        raise ValueError("size должен быть не меньше 1")
    return [items[i:i + size] for i in range(0, len(items), size)]


def sort_words(words):
    """Новый список: сначала короткие слова, при равной длине - по алфавиту. Исходный не меняется."""
    return sorted(words, key=lambda w: (len(w), w))


def make_grid(rows, cols, fill=0):
    """Таблица rows x cols из независимых строк, заполненная значением fill."""
    return [[fill] * cols for _ in range(rows)]
