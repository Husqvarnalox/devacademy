import numpy as np


def sq_dists(X, centers):
    """Матрица квадратов расстояний (n, k) через broadcasting."""
    diff = X[:, None, :] - centers[None, :, :]
    return np.einsum("nkd,nkd->nk", diff, diff)


def kmeans_pp_init(X, k, rng):
    """k-means++: первый центр - случайная точка, остальные выбираются с вероятностью, пропорциональной D^2."""
    X = np.asarray(X, dtype=np.float64)
    n = X.shape[0]
    centers = np.empty((k, X.shape[1]))
    centers[0] = X[rng.integers(n)]
    d2 = sq_dists(X, centers[:1])[:, 0]
    for i in range(1, k):
        total = d2.sum()
        if total > 0:
            idx = rng.choice(n, p=d2 / total)
        else:
            idx = rng.integers(n)
        centers[i] = X[idx]
        d2 = np.minimum(d2, sq_dists(X, centers[i:i + 1])[:, 0])
    return centers


def assign(X, centers):
    """Индекс ближайшего центра для каждой точки: int64 (n,). При равенстве - меньший индекс."""
    return np.argmin(sq_dists(X, centers), axis=1).astype(np.int64)


def update(X, labels, centers):
    """Новые центры - средние по кластерам; пустой кластер сохраняет прежний центр."""
    new = centers.astype(np.float64, copy=True)
    for j in range(centers.shape[0]):
        mask = labels == j
        if mask.any():
            new[j] = X[mask].mean(axis=0)
    return new


def inertia(X, centers, labels):
    """Сумма квадратов расстояний от точек до центров их кластеров (float)."""
    diff = X - centers[labels]
    return float(np.sum(diff * diff))


def kmeans(X, k, seed=0, max_iter=100, tol=1e-6):
    """Алгоритм Ллойда. Остановка: максимальный сдвиг центра <= tol или достигнуто max_iter.

    Возвращает (centers, labels, inertia, n_iter); labels и inertia согласованы с итоговыми центрами.
    """
    X = np.asarray(X, dtype=np.float64)
    rng = np.random.default_rng(seed)
    centers = kmeans_pp_init(X, k, rng)
    n_iter = 0
    for _ in range(max_iter):
        n_iter += 1
        labels = assign(X, centers)
        new = update(X, labels, centers)
        shift = np.sqrt(np.max(np.sum((new - centers) ** 2, axis=1)))
        centers = new
        if shift <= tol:
            break
    labels = assign(X, centers)
    return centers, labels, inertia(X, centers, labels), n_iter
