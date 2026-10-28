import math

import numpy as np

BOUNDARY = "<s>"


def build_vocab(texts):
    """Словарь символов: ["<s>"] + отсортированные уникальные символы всех текстов.

    Токен "<s>" имеет индекс 0 и служит и началом, и концом текста.
    """
    # TODO
    return [BOUNDARY]


def encode_text(text, stoi):
    """Текст -> [0] + id символов + [0]. Символ, которого нет в stoi, - ValueError с этим символом в сообщении."""
    # TODO
    return [0]


def count_bigrams(texts, stoi):
    """Матрица int64 (V, V): C[a, b] - сколько раз токен b стоит сразу после a (границы <s> включены)."""
    # TODO
    V = len(stoi)
    return np.zeros((V, V), dtype=np.int64)


def probs_from_counts(C, alpha=1.0):
    """Строки P = (C + alpha) / сумма строки. Строка с суммой 0 (возможна при alpha=0) - равномерная 1/V."""
    # TODO
    return np.asarray(C, dtype=np.float64)


def avg_nll(P, texts, stoi):
    """Среднее -ln P[a, b] по всем переходам всех текстов (включая последний -> <s>).

    Если какая-то вероятность равна 0, вернуть float("inf") без предупреждений. Пустой texts - ValueError.
    """
    # TODO
    return 0.0


def perplexity(P, texts, stoi):
    """exp(avg_nll); бесконечность остаётся бесконечностью."""
    # TODO
    return 1.0
