import json
import os
import random

NONCE = os.environ["DEVACADEMY_NONCE"]

import split_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def reference(n, val_frac, test_frac, seed):
    rng = random.Random(seed)
    idx = list(range(n))
    rng.shuffle(idx)
    n_test = int(n * test_frac)
    n_val = int(n * val_frac)
    return idx[n_test + n_val:], idx[n_test:n_test + n_val], idx[:n_test]


def t_sizes_and_partition():
    for n, v, t in [(100, 0.2, 0.2), (10, 0.1, 0.3), (57, 0.25, 0.15)]:
        train, val, test = m.train_val_test_split(n, v, t, 7)
        want_test, want_val = int(n * t), int(n * v)
        if len(test) != want_test or len(val) != want_val or len(train) != n - want_test - want_val:
            return f"n={n}: размеры (train, val, test) = {(len(train), len(val), len(test))}, ожидалось {(n - want_test - want_val, want_val, want_test)}"
        union = list(train) + list(val) + list(test)
        if sorted(union) != list(range(n)):
            return f"n={n}: объединение частей должно быть ровно индексами 0..{n - 1}, каждый по одному разу"


def t_matches_reference():
    for n, v, t, seed in [(20, 0.2, 0.2, 0), (50, 0.1, 0.3, 42), (13, 0.25, 0.25, 5)]:
        got = m.train_val_test_split(n, v, t, seed)
        want = reference(n, v, t, seed)
        if [list(p) for p in got] != [list(p) for p in want]:
            return f"seed={seed}, n={n}: порядок индексов не совпадает с эталоном (shuffle от Random(seed), срезы test, val, train по порядку)"


def t_deterministic_and_seed_matters():
    a = m.train_val_test_split(30, 0.2, 0.2, 1)
    b = m.train_val_test_split(30, 0.2, 0.2, 1)
    c = m.train_val_test_split(30, 0.2, 0.2, 2)
    if a != b:
        return "один и тот же seed должен давать один и тот же результат"
    if a == c:
        return "разные seed должны давать разные разбиения"


def t_global_random_untouched():
    random.seed(12345)
    first = random.random()
    random.seed(12345)
    m.train_val_test_split(40, 0.2, 0.2, 99)
    second = random.random()
    if first != second:
        return "функция изменила состояние глобального генератора random: используйте собственный random.Random(seed)"


def t_find_leaks_clean():
    train, val, test = m.train_val_test_split(60, 0.2, 0.2, 3)
    res = m.find_leaks(train, val, test)
    if list(res) != []:
        return f"в честном разбиении утечек нет, а find_leaks вернул {res!r}"


def t_find_leaks_detects():
    cases = [
        (([0, 1, 2], [3, 4], [5, 2]), [2]),
        (([0, 1, 2], [2, 3], [3, 4, 0]), [0, 2, 3]),
        (([1, 2], [3], [4]), []),
        (([1, 1, 2], [3], [4]), []),
        (([5, 6], [6], [6, 7]), [6]),
    ]
    for args, want in cases:
        got = m.find_leaks(*args)
        if list(got) != want:
            return f"find_leaks{args} вернул {got!r}, ожидалось {want!r} (повтор внутри одной части утечкой не считается)"


def t_edge_cases():
    train, val, test = m.train_val_test_split(5, 0.0, 0.0, 1)
    if sorted(train) != [0, 1, 2, 3, 4] or list(val) != [] or list(test) != []:
        return "при нулевых долях все индексы должны уйти в train"
    train, val, test = m.train_val_test_split(0, 0.2, 0.2, 1)
    if (list(train), list(val), list(test)) != ([], [], []):
        return "при n=0 все части пусты"
    train, val, test = m.train_val_test_split(9, 0.2, 0.2, 1)
    if (len(train), len(val), len(test)) != (7, 1, 1):
        return f"int(9*0.2)=1: ожидалось (7, 1, 1), получили {(len(train), len(val), len(test))}"


case("sizes-and-partition", t_sizes_and_partition)
case("matches-reference", t_matches_reference)
case("find-leaks-clean", t_find_leaks_clean)
case("find-leaks-detects", t_find_leaks_detects)
case("deterministic", t_deterministic_and_seed_matters)
case("global-random-untouched", t_global_random_untouched)
case("edge-cases", t_edge_cases)
