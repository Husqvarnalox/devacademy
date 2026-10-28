def get_stats(ids):
    """Частоты соседних пар: {(a, b): сколько раз пара стоит рядом}. Порядок ключей - порядок первого появления."""
    counts = {}
    for pair in zip(ids, ids[1:]):
        counts[pair] = counts.get(pair, 0) + 1
    return counts


def merge(ids, pair, new_id):
    """Заменить каждое вхождение пары (слева направо, без перекрытий) на new_id. Вернуть новый список."""
    out = []
    i = 0
    while i < len(ids):
        if i + 1 < len(ids) and ids[i] == pair[0] and ids[i + 1] == pair[1]:
            out.append(new_id)
            i += 2
        else:
            out.append(ids[i])
            i += 1
    return out


def train(text, vocab_size):
    """Обучить байтовый BPE: вернуть словарь слияний {(a, b): new_id} в порядке обучения (new_id = 256, 257, ...)."""
    if vocab_size < 256:
        raise ValueError("vocab_size должен быть не меньше 256: базовый словарь - 256 байтов")
    ids = list(text.encode("utf-8"))
    merges = {}
    for new_id in range(256, vocab_size):
        stats = get_stats(ids)
        if not stats:
            break
        pair = max(stats, key=stats.get)
        if stats[pair] < 2:
            break
        ids = merge(ids, pair, new_id)
        merges[pair] = new_id
    return merges


def encode(text, merges):
    """Текст -> список id: байты UTF-8, затем слияния в порядке их обучения (сначала пара с наименьшим id)."""
    ids = list(text.encode("utf-8"))
    while len(ids) >= 2:
        stats = get_stats(ids)
        pair = min(stats, key=lambda p: merges.get(p, float("inf")))
        if pair not in merges:
            break
        ids = merge(ids, pair, merges[pair])
    return ids


def decode(ids, merges):
    """Список id -> текст: развернуть каждый id в байты и декодировать UTF-8 (неполные символы заменить на U+FFFD)."""
    vocab = {i: bytes([i]) for i in range(256)}
    for (a, b), idx in merges.items():
        vocab[idx] = vocab[a] + vocab[b]
    return b"".join(vocab[i] for i in ids).decode("utf-8", errors="replace")
