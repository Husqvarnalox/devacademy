import math

import numpy as np

BOUNDARY = "<s>"


def build_vocab(texts):
    """Словарь символов: ["<s>"] + отсортированные уникальные символы всех текстов.

    Токен "<s>" имеет индекс 0 и служит и началом, и концом текста.
    """
    chars = sorted(set("".join(texts)))
    return [BOUNDARY] + chars


def encode_text(text, stoi):
    """Текст -> [0] + id символов + [0]. Символ, которого нет в stoi, - ValueError с этим символом в сообщении."""
    ids = [0]
    for ch in text:
        if ch not in stoi:
            raise ValueError(f"неизвестный символ {ch!r}")
        ids.append(stoi[ch])
    ids.append(0)
    return ids


def count_bigrams(texts, stoi):
    """Матрица int64 (V, V): C[a, b] - сколько раз токен b стоит сразу после a (границы <s> включены)."""
    V = len(stoi)
    C = np.zeros((V, V), dtype=np.int64)
    for text in texts:
        ids = encode_text(text, stoi)
        for a, b in zip(ids, ids[1:]):
            C[a, b] += 1
    return C


def probs_from_counts(C, alpha=1.0):
    """Строки P = (C + alpha) / сумма строки. Строка с суммой 0 (возможна при alpha=0) - равномерная 1/V."""
    S = np.asarray(C, dtype=np.float64) + alpha
    V = S.shape[1]
    sums = S.sum(axis=1, keepdims=True)
    empty = sums == 0
    return np.where(empty, 1.0 / V, S / np.where(empty, 1.0, sums))


def avg_nll(P, texts, stoi):
    """Среднее -ln P[a, b] по всем переходам всех текстов (включая последний -> <s>).

    Если какая-то вероятность равна 0, вернуть float("inf") без предупреждений. Пустой texts - ValueError.
    """
    if len(texts) == 0:
        raise ValueError("нужен хотя бы один текст")
    probs = []
    for text in texts:
        ids = encode_text(text, stoi)
        probs.extend(P[a, b] for a, b in zip(ids, ids[1:]))
    probs = np.asarray(probs, dtype=np.float64)
    if np.any(probs <= 0.0):
        return float("inf")
    return float(-np.log(probs).mean())


def perplexity(P, texts, stoi):
    """exp(avg_nll); бесконечность остаётся бесконечностью."""
    return math.exp(avg_nll(P, texts, stoi))
