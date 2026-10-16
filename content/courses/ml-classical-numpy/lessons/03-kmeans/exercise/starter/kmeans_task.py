import numpy as np


def sq_dists(X, centers):
    """Матрица квадратов расстояний (n, k) через broadcasting (уже реализовано)."""
    diff = X[:, None, :] - centers[None, :, :]
    return np.einsum("nkd,nkd->nk", diff, diff)


def kmeans_pp_init(X, k, rng):
    """k-means++ (rng - np.random.Generator).

    Первый центр: X[rng.integers(n)]. Каждый следующий: индекс rng.choice(n, p=d2 / d2.sum()), где d2 - квадрат расстояния
    до ближайшего из уже выбранных центров. Если все d2 равны 0, выбирайте rng.integers(n). Верните копию строк X формы (k, d).
    """
    # TODO
    return np.asarray(X, dtype=np.float64)[:k].copy()


def assign(X, centers):
    """Индекс ближайшего центра для каждой точки: int64 формы (n,). При равенстве - меньший индекс."""
    # TODO
    return np.zeros(X.shape[0], dtype=np.int64)


def update(X, labels, centers):
    """Новые центры - средние по кластерам. Пустой кластер сохраняет прежний центр (не nan!). Верните float64 (k, d)."""
    # TODO
    return centers


def inertia(X, centers, labels):
    """Сумма квадратов расстояний от точек до центров их кластеров. Верните float."""
    # TODO
    return 0.0


def kmeans(X, k, seed=0, max_iter=100, tol=1e-6):
    """Алгоритм Ллойда с инициализацией k-means++ из np.random.default_rng(seed).

    Итерация: labels = assign -> new = update -> сдвиг = максимум по центрам евклидова расстояния между new и старыми.
    Остановка, когда сдвиг <= tol (после этой итерации) или выполнено max_iter итераций.
    Верните (centers, labels, inertia, n_iter): labels и inertia пересчитайте по итоговым центрам, n_iter - число выполненных итераций.
    """
    # TODO
    centers = kmeans_pp_init(X, k, np.random.default_rng(seed))
    labels = np.zeros(X.shape[0], dtype=np.int64)
    return centers, labels, 0.0, 0
