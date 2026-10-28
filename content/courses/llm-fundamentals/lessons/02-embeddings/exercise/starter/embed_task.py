import numpy as np


def lookup(E, ids):
    """Строки матрицы эмбеддингов E (V, d) по списку id -> копия float64 формы (len(ids), d).

    Любой id < 0 или >= V - ValueError (отрицательные индексы NumPy молча считали бы с конца).
    """
    # TODO
    return np.zeros((len(ids), np.asarray(E).shape[1]))


def l2_normalize(X, eps=1e-12):
    """Разделить каждую строку X на её евклидову норму (для 1-D - весь вектор).

    Строки с нормой меньше eps остаются нулевыми; nan и предупреждений быть не должно.
    """
    # TODO
    return np.array(X, dtype=np.float64)


def cosine_matrix(A, B):
    """Матрица косинусных сходств (n, m) между строками A (n, d) и B (m, d), обрезанная до [-1, 1].

    Если одна из строк нулевая, сходство с ней равно 0.
    """
    # TODO
    return np.asarray(A, dtype=np.float64) @ np.asarray(B, dtype=np.float64).T


def top_k(query, M, k):
    """Индексы k строк M, самых похожих на 1-D query по косинусу, по убыванию сходства.

    Ничьи - меньший индекс первым (np.argsort(-sims, kind="stable")). k > n - все n строк; k <= 0 - пустой массив.
    Результат - int64.
    """
    # TODO
    return np.arange(max(k, 0), dtype=np.int64)


def mean_pool(H, mask):
    """Среднее H (B, T, d) по позициям, где mask (B, T) равна 1 -> (B, d).

    Строка с нулевой маской даёт нули без предупреждений. Значения H на позициях с маской 0 не влияют на результат.
    """
    # TODO
    return np.asarray(H, dtype=np.float64).mean(axis=1)
