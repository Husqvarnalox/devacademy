import numpy as np


def normalize_rows(X):
    """Разделите каждую строку массива X (n, d) на её L2-норму.

    Нулевые строки остаются нулевыми (без nan и предупреждений). Результат - новый массив типа float.
    """
    X = np.asarray(X, dtype=float)
    norms = np.linalg.norm(X, axis=1, keepdims=True)
    safe = np.where(norms == 0.0, 1.0, norms)      # 0 заменяем на 1: нулевая строка останется нулевой
    return X / safe


def cosine_similarity_matrix(A, B):
    """Матрица (n, m) косинусов между строками A (n, d) и строками B (m, d).

    Если одна из строк нулевая, все косинусы с ней равны 0.
    """
    return normalize_rows(A) @ normalize_rows(B).T


def nearest(Q, X, k):
    """Для каждой строки Q (q, d) индексы k самых похожих по косинусу строк X (m, d).

    Вернуть целочисленный массив (q, k); в каждой строке индексы идут по убыванию сходства,
    при равенстве - по возрастанию индекса (устойчивая сортировка). Считайте, что k <= m.
    """
    S = cosine_similarity_matrix(Q, X)
    return np.argsort(-S, axis=1, kind="stable")[:, :k]
