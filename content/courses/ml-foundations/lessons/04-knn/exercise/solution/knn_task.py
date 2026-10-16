def squared_distance(a, b):
    """Квадрат евклидова расстояния между точками одинаковой размерности."""
    return sum((x - y) ** 2 for x, y in zip(a, b))


def knn_predict(train_x, train_y, query, k):
    """Метка для точки query по k ближайшим соседям.

    Соседи упорядочены по (квадрат расстояния, индекс в обучающей выборке); берутся первые k.
    Побеждает метка с наибольшим числом голосов; при равенстве - метка, чей ближайший сосед
    стоит раньше в этом порядке.
    """
    order = sorted(range(len(train_x)), key=lambda i: (squared_distance(train_x[i], query), i))
    top = order[:k]
    votes = {}
    first_rank = {}
    for rank, i in enumerate(top):
        label = train_y[i]
        votes[label] = votes.get(label, 0) + 1
        first_rank.setdefault(label, rank)
    return min(votes, key=lambda label: (-votes[label], first_rank[label]))


def knn_predict_all(train_x, train_y, queries, k):
    return [knn_predict(train_x, train_y, q, k) for q in queries]


def leave_one_out_accuracy(xs, ys, k):
    """Доля точек, метку которых верно предсказывают остальные точки (сама точка не участвует)."""
    correct = 0
    for i in range(len(xs)):
        rest_x = xs[:i] + xs[i + 1:]
        rest_y = ys[:i] + ys[i + 1:]
        if knn_predict(rest_x, rest_y, xs[i], k) == ys[i]:
            correct += 1
    return correct / len(xs)
