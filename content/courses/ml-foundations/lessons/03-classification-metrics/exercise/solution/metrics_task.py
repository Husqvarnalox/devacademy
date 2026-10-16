def confusion_matrix(y_true, y_pred):
    """Бинарная матрица ошибок для меток 0/1 (положительный класс - 1). Возвращает (tp, fp, fn, tn)."""
    tp = fp = fn = tn = 0
    for t, p in zip(y_true, y_pred):
        if p == 1 and t == 1:
            tp += 1
        elif p == 1 and t == 0:
            fp += 1
        elif p == 0 and t == 1:
            fn += 1
        else:
            tn += 1
    return tp, fp, fn, tn


def _div(a, b):
    return a / b if b else 0.0


def accuracy(y_true, y_pred):
    tp, fp, fn, tn = confusion_matrix(y_true, y_pred)
    return _div(tp + tn, tp + fp + fn + tn)


def precision(y_true, y_pred):
    tp, fp, fn, tn = confusion_matrix(y_true, y_pred)
    return _div(tp, tp + fp)


def recall(y_true, y_pred):
    tp, fp, fn, tn = confusion_matrix(y_true, y_pred)
    return _div(tp, tp + fn)


def f1(y_true, y_pred):
    p = precision(y_true, y_pred)
    r = recall(y_true, y_pred)
    return _div(2 * p * r, p + r)
