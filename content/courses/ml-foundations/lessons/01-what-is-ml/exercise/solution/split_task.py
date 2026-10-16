import random


def train_val_test_split(n, val_frac, test_frac, seed):
    """Детерминированно делит индексы 0..n-1 на обучающую, валидационную и тестовую части."""
    rng = random.Random(seed)
    idx = list(range(n))
    rng.shuffle(idx)
    n_test = int(n * test_frac)
    n_val = int(n * val_frac)
    test = idx[:n_test]
    val = idx[n_test:n_test + n_val]
    train = idx[n_test + n_val:]
    return train, val, test


def find_leaks(train, val, test):
    """Возвращает отсортированный список индексов, которые попали больше чем в одну часть."""
    counts = {}
    for part in (train, val, test):
        for i in set(part):
            counts[i] = counts.get(i, 0) + 1
    return sorted(i for i, c in counts.items() if c > 1)
