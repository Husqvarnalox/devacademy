import re

import numpy as np


def tokenize(text):
    """Строка -> список токенов: нижний регистр, «ё» заменить на «е», слова по регулярному выражению \\w+."""
    return re.findall(r"\w+", text.lower().replace("ё", "е"))


def chunk_words(words, size, overlap):
    """Список слов -> список чанков (списков слов) длиной до size с перекрытием overlap."""
    if size <= 0 or overlap < 0 or overlap >= size:
        raise ValueError("нужно size > 0 и 0 <= overlap < size")
    if not words:
        return []
    step = size - overlap
    chunks, start = [], 0
    while True:
        chunks.append(list(words[start:start + size]))
        if start + size >= len(words):
            break
        start += step
    return chunks


def build_index(chunks):
    """Список чанков (списков токенов) -> {"vocab": {term: col}, "idf": (V,), "matrix": (n, V)} с TF-IDF и L2-нормировкой строк."""
    terms = sorted({t for chunk in chunks for t in chunk})
    vocab = {t: i for i, t in enumerate(terms)}
    n = len(chunks)
    tf = np.zeros((n, len(terms)), dtype=np.float64)
    for row, chunk in enumerate(chunks):
        for t in chunk:
            tf[row, vocab[t]] += 1.0
    df = (tf > 0).sum(axis=0)
    idf = np.log((1.0 + n) / (1.0 + df)) + 1.0
    matrix = tf * idf
    norms = np.linalg.norm(matrix, axis=1, keepdims=True)
    matrix = np.divide(matrix, norms, out=np.zeros_like(matrix), where=norms > 0)
    return {"vocab": vocab, "idf": idf, "matrix": matrix}


def search(query_tokens, index, k, min_score=0.0):
    """Лучшие k чанков для запроса: список (индекс чанка, косинусная близость) со score > min_score."""
    vocab = index["vocab"]
    q = np.zeros(len(vocab), dtype=np.float64)
    for t in query_tokens:
        if t in vocab:
            q[vocab[t]] += 1.0
    q = q * index["idf"]
    norm = np.linalg.norm(q)
    if k <= 0 or norm == 0:
        return []
    scores = index["matrix"] @ (q / norm)
    found = [(i, float(s)) for i, s in enumerate(scores) if s > min_score]
    found.sort(key=lambda pair: (-pair[1], pair[0]))
    return found[:k]


def recall_at_k(retrieved, relevant, k):
    """Средняя по запросам доля найденных релевантных чанков среди первых k результатов."""
    if len(retrieved) != len(relevant):
        raise ValueError("retrieved и relevant должны быть одной длины")
    values = []
    for found, rel in zip(retrieved, relevant):
        rel = set(rel)
        if not rel:
            continue
        values.append(len(set(found[:k]) & rel) / len(rel))
    if not values:
        raise ValueError("нет запросов с непустым множеством релевантных чанков")
    return sum(values) / len(values)
