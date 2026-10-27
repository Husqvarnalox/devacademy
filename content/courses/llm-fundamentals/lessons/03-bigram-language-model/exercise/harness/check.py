import json
import math
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import bigram_task as m


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


NAMES = ["анна", "иван", "мария", "марина", "ирина", "ольга", "олег", "игорь", "инна", "антон", "андрей", "нина"]


def ref_stoi(texts):
    return {c: i for i, c in enumerate(["<s>"] + sorted(set("".join(texts))))}


def ref_counts(texts, stoi):
    C = np.zeros((len(stoi), len(stoi)), dtype=np.int64)
    for t in texts:
        ids = [0] + [stoi[c] for c in t] + [0]
        for a, b in zip(ids, ids[1:]):
            C[a, b] += 1
    return C


def ref_nll(P, texts, stoi):
    total, n = 0.0, 0
    for t in texts:
        ids = [0] + [stoi[c] for c in t] + [0]
        for a, b in zip(ids, ids[1:]):
            total += -math.log(P[a, b])
            n += 1
    return total / n


def t_vocab_encode():
    vocab = m.build_vocab(["анна", "иван"])
    if vocab != ["<s>", "а", "в", "и", "н"]:
        return f"build_vocab(['анна', 'иван']): ожидалось ['<s>', 'а', 'в', 'и', 'н'], получили {vocab!r}"
    stoi = {c: i for i, c in enumerate(vocab)}
    got = m.encode_text("ива", stoi)
    if got != [0, 3, 2, 1, 0]:
        return f"encode_text('ива'): ожидалось [0, 3, 2, 1, 0], получили {got!r}"
    if m.encode_text("", stoi) != [0, 0]:
        return "пустая строка кодируется как [0, 0]"
    if m.build_vocab(["ба", "аб"]) != ["<s>", "а", "б"]:
        return "символы сортируются, повторы убираются"


def t_count_bigrams():
    texts = ["аба", "аа"]
    stoi = {"<s>": 0, "а": 1, "б": 2}
    C = m.count_bigrams(texts, stoi)
    want = [[0, 2, 0], [2, 1, 1], [0, 1, 0]]
    if C.shape != (3, 3) or C.tolist() != want:
        return f"count_bigrams(['аба', 'аа']): ожидалось {want}, получили {C.tolist()}"


def t_probs_perplexity():
    stoi = {"<s>": 0, "а": 1}
    C = m.count_bigrams(["аа"], stoi)
    P = m.probs_from_counts(C, alpha=0)
    if not np.allclose(P, [[0.0, 1.0], [0.5, 0.5]], atol=1e-12):
        return f"probs_from_counts без сглаживания для 'аа': ожидалось [[0, 1], [0.5, 0.5]], получили {P.tolist()}"
    P1 = m.probs_from_counts(C, alpha=1.0)
    if not np.allclose(P1, [[1 / 3, 2 / 3], [2 / 4, 2 / 4]], atol=1e-12):
        return f"со сглаживанием alpha=1: ожидалось [[1/3, 2/3], [1/2, 1/2]], получили {P1.tolist()}"
    nll = m.avg_nll(P, ["аа"], stoi)
    want = 2 * math.log(2) / 3
    if not isinstance(nll, float) or abs(nll - want) > 1e-9:
        return f"avg_nll для 'аа': переходы <s>->а, а->а, а-><s>; ожидалось {want:.6f}, получили {nll!r}"
    pp = m.perplexity(P, ["аа"], stoi)
    if abs(pp - 2 ** (2 / 3)) > 1e-9:
        return f"perplexity = exp(avg_nll): ожидалось {2 ** (2 / 3):.6f}, получили {pp!r}"


def t_encode_errors():
    stoi = {"<s>": 0, "а": 1}
    try:
        m.encode_text("аб", stoi)
    except ValueError as exc:
        if "б" not in str(exc):
            return f"сообщение ValueError должно называть неизвестный символ «б», получили {exc!s}"
    else:
        return "неизвестный символ должен приводить к ValueError"
    try:
        m.count_bigrams(["аа", "ав"], stoi)
    except ValueError:
        pass
    else:
        return "count_bigrams с неизвестным символом должна бросать ValueError"


def t_counts_dtype():
    stoi = ref_stoi(NAMES)
    C = m.count_bigrams(NAMES, stoi)
    if C.dtype != np.int64 or C.shape != (len(stoi), len(stoi)):
        return f"счётчики: int64 формы ({len(stoi)}, {len(stoi)}), получили {C.dtype} {C.shape}"
    if not np.array_equal(C, ref_counts(NAMES, stoi)):
        return "матрица счётчиков не совпала с эталоном на списке имён"
    if int(C.sum()) != sum(len(t) + 1 for t in NAMES):
        return "сумма счётчиков равна числу переходов: сумма len(текст) + 1"
    if int(C[:, 0].sum()) != len(NAMES) or int(C[0].sum()) != len(NAMES):
        return "в каждом тексте один переход из <s> и один в <s>"
    if m.count_bigrams([], stoi).sum() != 0:
        return "без текстов все счётчики нулевые"


def t_probs_rows():
    stoi = ref_stoi(NAMES)
    C = ref_counts(NAMES, stoi)
    for alpha in (0.0, 0.1, 1.0, 5.0):
        with warnings.catch_warnings():
            warnings.simplefilter("error")
            P = m.probs_from_counts(C, alpha=alpha)
        if P.dtype != np.float64 or P.shape != C.shape:
            return f"alpha={alpha}: ожидался float64 той же формы, что C, получили {P.dtype} {P.shape}"
        if not np.allclose(P.sum(axis=1), 1.0, atol=1e-12):
            return f"alpha={alpha}: каждая строка P должна суммироваться в 1"
    want = (C + 0.5) / (C + 0.5).sum(axis=1, keepdims=True)
    if not np.allclose(m.probs_from_counts(C, alpha=0.5), want, atol=1e-12):
        return "P = (C + alpha) / сумма строки"
    if C.min() != 0 or m.probs_from_counts(C, alpha=1.0).min() <= 0:
        return "при alpha > 0 все вероятности положительны"


def t_probs_zero_row():
    C = np.array([[0, 0, 0], [1, 1, 2], [0, 0, 0]], dtype=np.int64)
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        P = m.probs_from_counts(C, alpha=0)
    want = [[1 / 3, 1 / 3, 1 / 3], [0.25, 0.25, 0.5], [1 / 3, 1 / 3, 1 / 3]]
    if not np.all(np.isfinite(P)) or not np.allclose(P, want, atol=1e-12):
        return f"строка со счётчиками 0 при alpha=0 становится равномерной 1/V: ожидалось {want}, получили {P.tolist()}"
    if C.dtype != np.int64 or C.sum() != 4:
        return "probs_from_counts не должна менять входную матрицу"


def t_nll_inf():
    stoi = {"<s>": 0, "а": 1, "б": 2}
    P = m.probs_from_counts(m.count_bigrams(["аа", "аб"], stoi), alpha=0)
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        v = m.avg_nll(P, ["бб"], stoi)
        pp = m.perplexity(P, ["бб"], stoi)
        ok = m.avg_nll(P, ["аа"], stoi)
    if v != float("inf") or not isinstance(v, float):
        return f"переход с вероятностью 0 даёт float('inf') без предупреждений, получили {v!r}"
    if pp != float("inf"):
        return f"perplexity от бесконечного avg_nll - inf, получили {pp!r}"
    if not math.isfinite(ok):
        return "если все вероятности положительны, значение конечно"


def t_nll_empty():
    stoi = {"<s>": 0, "а": 1}
    P = m.probs_from_counts(np.ones((2, 2), dtype=np.int64))
    for fn in (m.avg_nll, m.perplexity):
        try:
            fn(P, [], stoi)
        except ValueError:
            continue
        return f"{fn.__name__} при пустом списке текстов должна бросать ValueError"
    one = m.avg_nll(P, [""], stoi)
    if abs(one - math.log(2)) > 1e-12:
        return "пустая строка - один переход <s> -> <s>; при равномерной P это ln 2"


def t_perplexity_uniform():
    for texts in (["ааб", "в", "гд"], ["abc" * 4, "zyx"], ["а"]):
        chars = sorted(set("".join(texts)))
        vocab = ["<s>"] + chars
        stoi = {c: i for i, c in enumerate(vocab)}
        P = np.full((len(vocab), len(vocab)), 1.0 / len(vocab))
        pp = m.perplexity(P, texts, stoi)
        if abs(pp - len(vocab)) > 1e-9:
            return f"равномерная модель по словарю из V={len(vocab)} токенов имеет перплексию V, получили {pp!r}"


def t_smoothing():
    train = NAMES
    held = ["нина", "лиана", "ирма", "олеся"]
    stoi = ref_stoi(train + held)
    C = ref_counts(train, stoi)
    P0 = m.probs_from_counts(C, alpha=0)
    P1 = m.probs_from_counts(C, alpha=1.0)
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        raw = m.avg_nll(P0, held, stoi)
        smooth = m.avg_nll(P1, held, stoi)
    if raw != float("inf"):
        return "в тестовых именах есть биграммы, которых не было в обучении: без сглаживания avg_nll = inf"
    want = ref_nll(P1, held, stoi)
    if not (math.isfinite(smooth) and abs(smooth - want) < 1e-9):
        return f"со сглаживанием alpha=1 ожидалось {want:.6f}, получили {smooth!r}"
    for alpha in (0.05, 0.3, 3.0):
        P = m.probs_from_counts(C, alpha=alpha)
        if abs(m.avg_nll(P, held, stoi) - ref_nll(P, held, stoi)) > 1e-9:
            return f"alpha={alpha}: avg_nll не совпала с эталоном"
    tr0 = m.avg_nll(P0, train, stoi)
    tr1 = m.avg_nll(P1, train, stoi)
    if not tr0 < tr1:
        return "на обучающих данных без сглаживания NLL меньше (модель подогнана под счётчики), чем со сглаживанием"
    if abs(m.perplexity(P1, held, stoi) - math.exp(want)) > 1e-8 * math.exp(want):
        return "perplexity должна равняться exp(avg_nll)"


case("vocab-encode", t_vocab_encode)
case("count-bigrams", t_count_bigrams)
case("probs-perplexity", t_probs_perplexity)
case("encode-errors", t_encode_errors)
case("counts-dtype", t_counts_dtype)
case("probs-rows", t_probs_rows)
case("probs-zero-row", t_probs_zero_row)
case("nll-inf", t_nll_inf)
case("nll-empty", t_nll_empty)
case("perplexity-uniform", t_perplexity_uniform)
case("smoothing", t_smoothing)
