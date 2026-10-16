import math


def mse(w, b, xs, ys):
    """Средний квадрат ошибки модели y = w*x + b."""
    n = len(xs)
    return sum((w * x + b - y) ** 2 for x, y in zip(xs, ys)) / n


def fit(xs, ys, lr, epochs):
    """Пакетный градиентный спуск для y = w*x + b. Начало: w = b = 0.0."""
    n = len(xs)
    w = 0.0
    b = 0.0
    for _ in range(epochs):
        dw = 0.0
        db = 0.0
        for x, y in zip(xs, ys):
            err = w * x + b - y
            dw += 2.0 * err * x / n
            db += 2.0 * err / n
        w -= lr * dw
        b -= lr * db
    return w, b


def fit_scaled(xs, ys, lr, epochs):
    """Стандартизует x, обучает модель и возвращает (w, b) в исходном масштабе."""
    n = len(xs)
    mean = sum(xs) / n
    std = math.sqrt(sum((x - mean) ** 2 for x in xs) / n)
    if std == 0.0:
        return 0.0, sum(ys) / n
    zs = [(x - mean) / std for x in xs]
    w_s, b_s = fit(zs, ys, lr, epochs)
    w = w_s / std
    b = b_s - w_s * mean / std
    return w, b
