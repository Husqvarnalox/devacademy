import numpy as np


def add_bias(X):
    """Добавляет слева столбец единиц (уже реализовано)."""
    X = np.asarray(X, dtype=np.float64)
    return np.hstack([np.ones((X.shape[0], 1)), X])


def sigmoid(z):
    """Устойчивая сигмоида 1 / (1 + e^-z) для массива z: без переполнения и предупреждений при |z| порядка 1000."""
    # TODO
    return np.asarray(z, dtype=np.float64)


def log_loss(z, y):
    """Средняя логистическая потеря по ЛОГИТАМ z: mean(log(1 + e^z) - y * z). Используйте np.logaddexp. Верните float."""
    # TODO
    return 0.0


def gradient(Xb, y, w):
    """Градиент средней потери по w: Xb^T (sigmoid(Xb w) - y) / n. Xb уже со смещением."""
    # TODO
    return np.zeros_like(w)


def fit(X, y, lr, epochs):
    """Пакетный градиентный спуск, начало w = 0. Верните w формы (d + 1,), w[0] - смещение."""
    # TODO
    return np.zeros(X.shape[1] + 1)


def predict(X, w, threshold=0.5):
    """Метки 0/1 (int64): вероятность sigmoid(Xb w) >= threshold."""
    # TODO
    return np.zeros(X.shape[0], dtype=np.int64)
