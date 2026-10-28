import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import sampling_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def close(a, b, tol=1e-9):
    return np.allclose(np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64), rtol=0, atol=tol)


# Эталонная реализация (используется только для сравнения последовательностей с фиксированным seed)
def ref_softmax(logits, temperature=1.0):
    z = np.asarray(logits, dtype=np.float64) / temperature
    e = np.exp(z - z.max())
    return e / e.sum()


def ref_top_k(z, k):
    z = np.array(z, dtype=np.float64)
    if k is None or k >= z.size:
        return z
    keep = np.argsort(-z, kind="stable")[:k]
    out = np.full_like(z, -np.inf)
    out[keep] = z[keep]
    return out


def ref_top_p(probs, p):
    order = np.argsort(-probs, kind="stable")
    cum = np.cumsum(probs[order])
    n_keep = min(int(np.searchsorted(cum, p, side="left")) + 1, probs.size)
    out = np.zeros_like(probs)
    out[order[:n_keep]] = probs[order[:n_keep]]
    return out / out.sum()


def ref_sample(logits, rng, temperature=1.0, top_k=None, top_p=None):
    logits = np.asarray(logits, dtype=np.float64)
    if temperature == 0:
        return int(np.argmax(logits))
    z = ref_top_k(logits / temperature, top_k)
    probs = ref_softmax(z)
    if top_p is not None:
        probs = ref_top_p(probs, top_p)
    return int(rng.choice(len(probs), p=probs))


def ref_generate(table, start, end, rng, n, **kw):
    out, last = [], start
    for _ in range(n):
        t = ref_sample(table[last], rng, **kw)
        if t == end:
            break
        out.append(t)
        last = t
    return out


def make_table(seed=0, size=8):
    return np.random.default_rng(seed).normal(scale=2.0, size=(size, size))


SETTINGS = [
    {},
    {"temperature": 0.7},
    {"temperature": 1.5},
    {"top_k": 3},
    {"top_p": 0.8},
    {"temperature": 0.8, "top_k": 4, "top_p": 0.9},
    {"temperature": 1.2, "top_p": 0.5},
]


def t_softmax_basic():
    got = m.softmax(np.array([1.0, 2.0, 3.0]))
    want = np.exp([1.0, 2.0, 3.0]) / np.exp([1.0, 2.0, 3.0]).sum()
    if got.shape != (3,) or not close(got, want):
        return f"softmax([1, 2, 3]): ожидалось {want.round(4).tolist()}, получили {np.asarray(got).round(4).tolist()}"
    cold = m.softmax([1.0, 2.0, 3.0], temperature=0.5)
    e = np.exp([2.0, 4.0, 6.0])
    if not close(cold, e / e.sum()):
        return "temperature делит логиты: softmax(z / T); при T=0.5 распределение острее"
    hot = m.softmax([1.0, 2.0, 3.0], temperature=100.0)
    if not (hot.max() - hot.min() < 0.02 and abs(hot.sum() - 1.0) < 1e-12):
        return "при большой температуре распределение почти равномерное и суммируется в 1"


def t_filters_basic():
    z = np.array([1.0, 5.0, 3.0, 4.0])
    got = m.top_k_filter(z, 2)
    if got.tolist() != [-np.inf, 5.0, -np.inf, 4.0]:
        return f"top_k_filter([1, 5, 3, 4], 2): ожидалось [-inf, 5, -inf, 4], получили {got.tolist()}"
    if z.tolist() != [1.0, 5.0, 3.0, 4.0]:
        return "top_k_filter не должна менять входной массив"
    probs = np.array([0.5, 0.25, 0.125, 0.125])
    got = m.top_p_filter(probs, 0.7)
    if not close(got, [2 / 3, 1 / 3, 0.0, 0.0]):
        return f"top_p_filter([0.5, 0.25, 0.125, 0.125], 0.7): оставляются два токена и перенормируются в [2/3, 1/3, 0, 0], получили {got.tolist()}"
    if probs.tolist() != [0.5, 0.25, 0.125, 0.125]:
        return "top_p_filter не должна менять входной массив"


def t_generate_greedy():
    table = np.full((4, 4), -5.0)
    table[0, 2] = 1.0
    table[2, 1] = 1.0
    table[1, 3] = 1.0
    table[3, 3] = 1.0
    rng = np.random.default_rng(0)
    got = m.generate(table, 0, 3, rng, 10, temperature=0)
    if got != [2, 1]:
        return f"greedy-генерация по цепочке 0 -> 2 -> 1 -> 3 (конец): ожидалось [2, 1], получили {got!r}"
    cyc = np.full((3, 3), -5.0)
    cyc[0, 1] = 1.0
    cyc[1, 1] = 1.0
    got = m.generate(cyc, 0, 2, rng, 4, temperature=0)
    if got != [1, 1, 1, 1]:
        return f"greedy зацикливается на токене 1; генерация обрывается после max_new_tokens=4: ожидалось [1, 1, 1, 1], получили {got!r}"
    if not all(type(t) is int for t in got):
        return "generate возвращает список обычных int"


def t_softmax_stable():
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        a = m.softmax(np.array([1000.0, 0.0]))
        b = m.softmax(np.array([-1000.0, -1001.0]))
        c = m.softmax(np.array([0.0, -np.inf, 1.0, -np.inf]))
        d = m.softmax(np.array([1000.0, 999.0, 0.0]), temperature=0.1)
    if not close(a, [1.0, 0.0]):
        return f"softmax([1000, 0]): ожидалось [1, 0] без переполнения, получили {a.tolist()}"
    if not close(b, [1 / (1 + np.exp(-1)), np.exp(-1) / (1 + np.exp(-1))]):
        return "softmax не должен зависеть от сдвига логитов: [-1000, -1001] даёт то же, что [0, -1]"
    e = np.exp(-1.0)
    if not close(c, [e / (1 + e), 0.0, 1 / (1 + e), 0.0]) or c[1] != 0.0:
        return f"значения -inf получают вероятность ровно 0: получили {c.tolist()}"
    if not np.all(np.isfinite(d)) or abs(d.sum() - 1.0) > 1e-12:
        return "softmax с малой температурой и большими логитами остаётся конечным и суммируется в 1"
    for bad in (0.0, -1.0):
        try:
            m.softmax(np.array([1.0, 2.0]), temperature=bad)
        except ValueError:
            continue
        return f"temperature={bad} должна приводить к ValueError"


def t_top_k_edge():
    z = np.array([2.0, 5.0, 5.0, 1.0, 5.0])
    if m.top_k_filter(z, 2).tolist() != [-np.inf, 5.0, 5.0, -np.inf, -np.inf]:
        return "при ничьей на границе выигрывает меньший индекс: из трёх пятёрок остаются индексы 1 и 2"
    if m.top_k_filter(z, 1).tolist() != [-np.inf, 5.0, -np.inf, -np.inf, -np.inf]:
        return "k=1 при трёх одинаковых максимумах оставляет первый"
    for k in (None, 5, 6, 100):
        got = m.top_k_filter(z, k)
        if got.tolist() != z.tolist():
            return f"k={k}: все значения остаются"
        got[0] = -7.0
        if z[0] != 2.0:
            return f"k={k}: даже без фильтрации возвращается копия"
    for k in (0, -1):
        try:
            m.top_k_filter(z, k)
        except ValueError:
            continue
        return f"k={k} должно приводить к ValueError"
    got = m.top_k_filter(np.array([1, 3, 2]), 2)
    if got.dtype != np.float64 or got.tolist() != [-np.inf, 3.0, 2.0]:
        return "целочисленные логиты приводятся к float64"
    got = m.top_k_filter(np.array([0.0, -np.inf, 1.0]), 2)
    if got.tolist() != [0.0, -np.inf, 1.0]:
        return "значения -inf остаются -inf"


def t_top_p_edge():
    probs = np.array([0.1, 0.5, 0.25, 0.125, 0.025])
    one = m.top_p_filter(probs, 1e-9)
    if not close(one, [0, 1, 0, 0, 0]):
        return f"крошечное p оставляет ровно один самый вероятный токен, получили {one.tolist()}"
    allp = m.top_p_filter(probs, 1.0)
    if not close(allp, probs):
        return f"p=1 оставляет все токены с ненулевой вероятностью без изменений, получили {allp.tolist()}"
    exact = m.top_p_filter(np.array([0.5, 0.25, 0.125, 0.125]), 0.875)
    if not close(exact, [0.5 / 0.875, 0.25 / 0.875, 0.125 / 0.875, 0.0]):
        return f"граница включается (сумма >= p): p=0.875 оставляет три токена, получили {exact.tolist()}"
    tie = m.top_p_filter(np.array([0.25, 0.25, 0.25, 0.25]), 0.5)
    if not close(tie, [0.5, 0.5, 0, 0]):
        return f"при равных вероятностях выигрывают меньшие индексы: ожидалось [0.5, 0.5, 0, 0], получили {tie.tolist()}"
    zeros = m.top_p_filter(np.array([0.0, 0.6, 0.0, 0.4]), 1.0)
    if not close(zeros, [0, 0.6, 0, 0.4]) or abs(zeros.sum() - 1.0) > 1e-12:
        return "нулевые вероятности остаются нулевыми"
    mid = m.top_p_filter(probs, 0.8)
    kept = np.array([0.5, 0.25, 0.125])
    want = np.zeros(5)
    want[[1, 2, 3]] = kept / kept.sum()
    if not close(mid, want):
        return f"p=0.8 оставляет токены 1, 2, 3 (сумма 0.875) и перенормирует, получили {mid.tolist()}"
    for bad in (0.0, -0.1, 1.5):
        try:
            m.top_p_filter(probs, bad)
        except ValueError:
            continue
        return f"p={bad} должно приводить к ValueError"


def t_sample_reference():
    table = make_table(0)
    for seed in (0, 1, 2, 3):
        for kw in SETTINGS:
            logits = table[seed % 8]
            a = [m.sample_next(logits, np.random.default_rng(seed), **kw) for _ in range(1)]
            b = [ref_sample(logits, np.random.default_rng(seed), **kw)]
            if a != b:
                return f"sample_next(seed={seed}, {kw}) вернула {a[0]}, эталон {b[0]}; порядок: logits / temperature -> top_k -> softmax -> top_p -> rng.choice"
    rng_a, rng_b = np.random.default_rng(11), np.random.default_rng(11)
    for kw in SETTINGS:
        for i in range(40):
            logits = table[i % 8]
            got, want = m.sample_next(logits, rng_a, **kw), ref_sample(logits, rng_b, **kw)
            if got != want or type(got) is not int:
                return f"последовательность выборов с общим генератором и настройками {kw} разошлась на шаге {i}: {got!r} вместо {want!r}"


def t_temperature_zero():
    logits = np.array([0.5, 2.0, 2.0, -1.0])
    rng = np.random.default_rng(42)
    state = rng.bit_generator.state
    got = m.sample_next(logits, rng, temperature=0, top_k=2, top_p=0.1)
    if got != 1:
        return f"temperature=0 - argmax (первый максимум), top_k и top_p игнорируются: ожидалось 1, получили {got!r}"
    if rng.bit_generator.state != state:
        return "при temperature=0 генератор rng использоваться не должен: его состояние изменилось"
    table = make_table(5)
    for i in range(8):
        r = np.random.default_rng(i)
        if m.sample_next(table[i], r, top_k=1) != int(np.argmax(table[i])):
            return "top_k=1 совпадает с greedy"
    for bad in ({"temperature": -0.5}, {"top_k": 0}, {"top_k": -2}, {"top_p": 0.0}, {"top_p": 1.2}):
        try:
            m.sample_next(logits, np.random.default_rng(0), **bad)
        except ValueError:
            continue
        return f"параметры {bad} должны приводить к ValueError"
    tiny = [m.sample_next(table[2], np.random.default_rng(s), top_p=1e-9) for s in range(20)]
    if set(tiny) != {int(np.argmax(table[2]))}:
        return "крошечное top_p оставляет один токен - самый вероятный"
    ninf = np.array([-np.inf, 0.0, -np.inf, 0.5])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        picks = {m.sample_next(ninf, np.random.default_rng(s)) for s in range(60)}
    if picks - {1, 3}:
        return f"токены с логитом -inf выбираться не могут, выбирались {sorted(picks)}"


def t_sample_distribution():
    logits = np.array([2.0, 1.0, 0.0, -1.0, 0.5])
    cases = [
        ({}, ref_softmax(logits)),
        ({"temperature": 0.7}, ref_softmax(logits, 0.7)),
        ({"top_k": 3}, ref_softmax(ref_top_k(logits, 3))),
        ({"top_p": 0.8}, ref_top_p(ref_softmax(logits), 0.8)),
    ]
    for kw, want in cases:
        rng = np.random.default_rng(123)
        n = 5000
        counts = np.zeros(len(logits))
        for _ in range(n):
            counts[m.sample_next(logits, rng, **kw)] += 1
        freq = counts / n
        if np.abs(freq - want).max() > 0.03:
            return f"настройки {kw}: частоты {freq.round(3).tolist()} расходятся с ожидаемыми {want.round(3).tolist()} больше чем на 0.03"


def t_generate_stops():
    table = make_table(1, size=6)
    for seed in (0, 1, 2, 3, 4):
        for kw in SETTINGS:
            got = m.generate(table, 0, 5, np.random.default_rng(seed), 25, **kw)
            want = ref_generate(table, 0, 5, np.random.default_rng(seed), 25, **kw)
            if got != want:
                return f"generate(seed={seed}, {kw}): ожидалось {want}, получили {got}"
    stop = np.full((3, 3), -50.0)
    stop[0, 1] = 0.0
    stop[1, 2] = 0.0
    got = m.generate(stop, 0, 2, np.random.default_rng(0), 10)
    if got != [1]:
        return f"end_id не добавляется в результат и останавливает генерацию: ожидалось [1], получили {got!r}"
    if m.generate(stop, 0, 1, np.random.default_rng(0), 10) != []:
        return "если первым выбран end_id, результат пуст"
    loop = np.full((3, 3), -50.0)
    loop[0, 1] = 0.0
    loop[1, 1] = 0.0
    if m.generate(loop, 0, 2, np.random.default_rng(0), 7) != [1] * 7:
        return "после max_new_tokens токенов генерация останавливается"
    if m.generate(loop, 0, 2, np.random.default_rng(0), 0) != []:
        return "max_new_tokens=0 даёт пустой список"
    r1, r2 = np.random.default_rng(9), np.random.default_rng(9)
    if m.generate(table, 0, 5, r1, 15, temperature=0.9, top_k=4) != m.generate(table, 0, 5, r2, 15, temperature=0.9, top_k=4):
        return "один и тот же seed даёт одну и ту же последовательность"


case("softmax-basic", t_softmax_basic)
case("filters-basic", t_filters_basic)
case("generate-greedy", t_generate_greedy)
case("softmax-stable", t_softmax_stable)
case("top-k-edge", t_top_k_edge)
case("top-p-edge", t_top_p_edge)
case("sample-reference", t_sample_reference)
case("temperature-zero", t_temperature_zero)
case("sample-distribution", t_sample_distribution)
case("generate-stops", t_generate_stops)
