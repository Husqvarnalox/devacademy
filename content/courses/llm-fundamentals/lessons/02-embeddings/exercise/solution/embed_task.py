import numpy as np


def lookup(E, ids):
    """Строки матрицы эмбеддингов E (V, d) по списку id -> копия float64 формы (len(ids), d).

    Любой id < 0 или >= V - ValueError (отрицательные индексы NumPy молча считали бы с конца).
    """
    E = np.asarray(E, dtype=np.float64)
    ids = np.asarray(ids, dtype=np.int64)
    if ids.size and (ids.min() < 0 or ids.max() >= E.shape[0]):
        raise ValueError(f"id вне диапазона 0..{E.shape[0] - 1}")
    return E[ids].copy()


def l2_normalize(X, eps=1e-12):
    """Разделить каждую строку X на её евклидову норму (для 1-D - весь вектор).

    Строки с нормой меньше eps остаются нулевыми; nan и предупреждений быть не должно.
    """
    X = np.asarray(X, dtype=np.float64)
    norms = np.linalg.norm(X, axis=-1, keepdims=True)
    tiny = norms < eps
    return np.where(tiny, 0.0, X / np.where(tiny, 1.0, norms))


def cosine_matrix(A, B):
    """Матрица косинусных сходств (n, m) между строками A (n, d) и B (m, d), обрезанная до [-1, 1].

    Если одна из строк нулевая, сходство с ней равно 0.
    """
    S = l2_normalize(A) @ l2_normalize(B).T
    return np.clip(S, -1.0, 1.0)


def top_k(query, M, k):
    """Индексы k строк M, самых похожих на 1-D query по косинусу, по убыванию сходства.

    Ничьи - меньший индекс первым (np.argsort(-sims, kind="stable")). k > n - все n строк; k <= 0 - пустой массив.
    Результат - int64.
    """
    sims = cosine_matrix(np.asarray(query, dtype=np.float64)[None, :], M)[0]
    if k <= 0:
        return np.empty(0, dtype=np.int64)
    order = np.argsort(-sims, kind="stable")
    return order[:k].astype(np.int64)


def mean_pool(H, mask):
    """Среднее H (B, T, d) по позициям, где mask (B, T) равна 1 -> (B, d).

    Строка с нулевой маской даёт нули без предупреждений. Значения H на позициях с маской 0 не влияют на результат.
    """
    H = np.asarray(H, dtype=np.float64)
    keep = np.asarray(mask) != 0
    summed = np.where(keep[:, :, None], H, 0.0).sum(axis=1)
    counts = keep.sum(axis=1, keepdims=True)
    return summed / np.maximum(counts, 1)
