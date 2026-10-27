import numpy as np


def update_centroids(X, labels, old_centroids):
    """Шаг k-means: новый центр кластера j - среднее строк X с labels == j.

    X - массив (n, d), labels - целые числа от 0 до k - 1 длины n, old_centroids - массив (k, d).
    Верните новый массив (k, d) типа float. Если в кластер j не попало ни одной точки,
    строка j равна old_centroids[j]. Входные массивы не изменяйте.
    """
    X = np.asarray(X, dtype=float)
    labels = np.asarray(labels)
    result = np.array(old_centroids, dtype=float)      # копия
    for j in range(len(result)):
        members = X[labels == j]
        if len(members) > 0:
            result[j] = members.mean(axis=0)
    return result


def momentum_step(w, velocity, grad, lr, beta):
    """Шаг градиентного спуска с моментом.

    new_velocity = beta * velocity + grad; new_w = w - lr * new_velocity.
    Верните кортеж (new_w, new_velocity) - новые массивы, входные не изменяйте.
    """
    new_velocity = beta * velocity + grad
    return w - lr * new_velocity, new_velocity
