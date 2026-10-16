def confusion_matrix(y_true, y_pred):
    """Бинарная матрица ошибок для меток 0/1 (положительный класс - 1).

    Возвращает кортеж (tp, fp, fn, tn).
    """
    # TODO
    return 0, 0, 0, 0


def accuracy(y_true, y_pred):
    """Доля верных ответов. Для пустого входа вернуть 0.0."""
    # TODO
    return 0.0


def precision(y_true, y_pred):
    """tp / (tp + fp). Если знаменатель 0, вернуть 0.0."""
    # TODO
    return 0.0


def recall(y_true, y_pred):
    """tp / (tp + fn). Если знаменатель 0, вернуть 0.0."""
    # TODO
    return 0.0


def f1(y_true, y_pred):
    """Среднее гармоническое precision и recall: 2*p*r / (p + r). Если p + r == 0, вернуть 0.0."""
    # TODO
    return 0.0
