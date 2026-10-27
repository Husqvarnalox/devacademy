def get_stats(ids):
    """Частоты соседних пар: {(a, b): сколько раз пара стоит рядом}. Порядок ключей - порядок первого появления (уже реализовано)."""
    counts = {}
    for pair in zip(ids, ids[1:]):
        counts[pair] = counts.get(pair, 0) + 1
    return counts


def merge(ids, pair, new_id):
    """Заменить каждое вхождение пары (слева направо, без перекрытий) на new_id. Вернуть новый список, ids не менять."""
    # TODO
    return list(ids)


def train(text, vocab_size):
    """Обучить байтовый BPE на text.

    Базовый словарь - 256 байтов, поэтому при vocab_size < 256 бросьте ValueError. Начните с list(text.encode("utf-8")).
    На каждом шаге выберите самую частую пару (при равенстве - ту, что встретилась раньше: max(stats, key=stats.get)),
    замените её на новый id (256, 257, ...) и запомните слияние. Остановитесь, когда словарь достиг vocab_size,
    пар не осталось или самая частая пара встречается меньше 2 раз.
    Верните словарь {(a, b): new_id} в порядке обучения.
    """
    # TODO
    return {}


def encode(text, merges):
    """Текст -> список id: байты UTF-8, затем повторять слияние той присутствующей пары, у которой id в merges наименьший,
    пока ни одна пара не входит в merges."""
    # TODO
    return list(text.encode("utf-8"))


def decode(ids, merges):
    """Список id -> текст: развернуть каждый id в байты и декодировать UTF-8 с errors="replace"."""
    # TODO
    return ""
