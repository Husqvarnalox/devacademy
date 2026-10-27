import json
import os
import re
import unicodedata

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import eval_task as m


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


def raises(fn, *args, **kwargs):
    try:
        fn(*args, **kwargs)
    except ValueError:
        return True
    return False


def close(a, b, tol=1e-9):
    return abs(a - b) <= tol


# ---------- эталонная реализация (независимая от решения) ----------


def ref_norm(s):
    chars = []
    for ch in s.lower():
        if ch == "ё":
            ch = "е"
        if unicodedata.category(ch)[0] == "P":
            ch = " "
        chars.append(ch)
    return " ".join("".join(chars).split())


def ref_f1_pair(a, b):
    if not a and not b:
        return 1.0
    if not a or not b:
        return 0.0
    left = {}
    for w in a:
        left[w] = left.get(w, 0) + 1
    common = 0
    for w in b:
        if left.get(w, 0) > 0:
            left[w] -= 1
            common += 1
    if common == 0:
        return 0.0
    p, r = common / len(a), common / len(b)
    return 2 * p * r / (p + r)


def ref_f1(pred, refs):
    return max(ref_f1_pair(ref_norm(pred).split(), ref_norm(r).split()) for r in refs)


def ref_em(pred, refs):
    return 1.0 if any(ref_norm(pred) == ref_norm(r) for r in refs) else 0.0


def ref_cit(answer, n):
    cited = sorted(set(int(x) for x in re.findall(r"\[(\d+)\]", answer)))
    bad = [c for c in cited if c < 1 or c > n]
    return {"cited": cited, "invalid": bad, "ok": bool(cited) and not bad}


def ref_ci(scores, n_boot=1000, seed=0, level=0.95):
    arr = np.array(scores, dtype=float)
    rng = np.random.default_rng(seed)
    means = rng.choice(arr, size=(n_boot, len(arr))).mean(axis=1)
    lo, hi = np.quantile(means, [(1 - level) / 2, 1 - (1 - level) / 2])
    return float(lo), float(hi)


# ---------- тесты ----------


def t_normalize_basic():
    cases = {
        "  Москва, столица!  ": "москва столица",
        "Привет,   МИР": "привет мир",
        "Ёлка": "елка",
        "": "",
    }
    for src, want in cases.items():
        got = m.normalize_answer(src)
        if got != want:
            return f"normalize_answer({src!r}): ожидалось {want!r}, получили {got!r}"


def t_em_f1_basic():
    refs = ["Москва"]
    if m.exact_match("москва!", refs) != 1.0:
        return "регистр и знаки препинания не должны влиять на exact_match"
    if m.exact_match("Санкт-Петербург", refs) != 0.0:
        return "разные ответы - exact_match равен 0.0"
    got = m.token_f1("кошка сидит на окне", ["кошка на окне"])
    if not close(got, 6 / 7):
        return f"token_f1: p = 3/4, r = 1, F1 = 6/7 = 0.857143, получили {got!r}"
    if m.token_f1("синий", ["красный"]) != 0.0:
        return "нет общих слов - F1 равен 0.0"


def t_citations_basic():
    got = m.check_citations("Книги выдают на 21 день [1], штраф 5 рублей в день [2].", 3)
    if got != {"cited": [1, 2], "invalid": [], "ok": True}:
        return f"корректные ссылки: получили {got!r}"
    got = m.check_citations("Ответ без ссылок.", 3)
    if got != {"cited": [], "invalid": [], "ok": False}:
        return f"ответ без ссылок не считается подтверждённым: получили {got!r}"
    got = m.check_citations("Так написано в источнике [5].", 3)
    if got != {"cited": [5], "invalid": [5], "ok": False}:
        return f"ссылка на несуществующий источник: получили {got!r}"


def t_normalize_edge():
    cases = {
        "Ёж и ёлка; ЁЖИК": "еж и елка ежик",
        "\tдва\nслова через  пробелы ": "два слова через пробелы",
        "«Привет», - сказал он...": "привет сказал он",
        "кто-то_где-то": "кто то где то",
        "???": "",
        "(a)[b]{c}": "a b c",
        "цена: 5$ + 3%": "цена 5$ + 3",
        "Ёё": "ее",
        "ТОЧКА.": "точка",
        "   ": "",
    }
    for src, want in cases.items():
        got = m.normalize_answer(src)
        if got != want:
            return (
                f"normalize_answer({src!r}): ожидалось {want!r}, получили {got!r} "
                "(знаки категории P заменяются пробелом, символы категории S - например $ и + - остаются)"
            )
    src = "Два   слова!"
    m.normalize_answer(src)
    if src != "Два   слова!":
        return "строка-аргумент не должна изменяться"


def t_em_edge():
    if m.exact_match("в 1998 году", ["1998", "в 1998 году"]) != 1.0:
        return "достаточно совпасть с любым из эталонов"
    if m.exact_match("1998 год", ["в 1998 году"]) != 0.0:
        return "exact_match требует полного совпадения нормализованных строк"
    if m.exact_match("", [""]) != 1.0 or m.exact_match("!!!", [""]) != 1.0:
        return "после нормализации пустые строки равны"
    if m.exact_match("", ["ответ"]) != 0.0:
        return "пустой ответ не совпадает с непустым эталоном"
    got = m.exact_match("Елка", ["Ёлка"])
    if got != 1.0:
        return "ё и е считаются одной буквой"
    if not raises(m.exact_match, "x", []):
        return "пустой список эталонов - ValueError"
    if not isinstance(m.exact_match("a", ["a"]), float):
        return "результат - float (1.0 или 0.0)"
    if m.exact_match("москва 1", ["москва"]) != 0.0:
        return "exact_match не вырезает номера ссылок: «москва [1]» и «москва» различаются"


def t_f1_values():
    tests = [
        ("да да нет", ["да нет"], 0.8),
        ("красный дом", ["дом", "красный большой дом"], 0.8),
        ("a b c d", ["a b c d"], 1.0),
        ("a b", ["c d"], 0.0),
        ("Москва, столица России", ["столица России"], 0.8),
        ("a", ["a a a"], 0.5),
    ]
    for pred, refs, want in tests:
        got = m.token_f1(pred, refs)
        if not close(got, want):
            return f"token_f1({pred!r}, {refs!r}): ожидалось {want}, получили {got!r}"
    pred, refs = "один два три три", ["три два два"]
    got, want = m.token_f1(pred, refs), ref_f1(pred, refs)
    if not close(got, want):
        return f"пересечение считается как мультимножество: ожидалось {want:.6f}, получили {got!r}"


def t_f1_edge():
    if m.token_f1("", [""]) != 1.0 or m.token_f1(".", [""]) != 1.0:
        return "оба набора токенов пусты - F1 равен 1.0"
    if m.token_f1("", ["ответ"]) != 0.0 or m.token_f1("ответ", ["?"]) != 0.0:
        return "пуст ровно один набор токенов - F1 равен 0.0"
    if m.token_f1("ответ", ["", "ответ"]) != 1.0:
        return "берётся максимум по эталонам, пустой эталон не мешает"
    if not raises(m.token_f1, "x", []):
        return "пустой список эталонов - ValueError"
    got = m.token_f1("Ёж, ёлка", ["еж елка"])
    if not close(got, 1.0):
        return "нормализация применяется и к ответу, и к эталонам"
    for pred, refs in [("a b c", ["b c d e"]), ("x y z z", ["z z z y"]), ("a", ["b", "a b"])]:
        got, want = m.token_f1(pred, refs), ref_f1(pred, refs)
        if not close(got, want):
            return f"token_f1({pred!r}, {refs!r}): ожидалось {want:.6f}, получили {got!r}"


def t_citations_edge():
    cases = [
        ("[1] и [3] и [1]", 3, {"cited": [1, 3], "invalid": [], "ok": True}),
        ("[0]", 3, {"cited": [0], "invalid": [0], "ok": False}),
        ("[2][10][2]", 5, {"cited": [2, 10], "invalid": [10], "ok": False}),
        ("[10] [9] [8]", 10, {"cited": [8, 9, 10], "invalid": [], "ok": True}),
        ("[01] и [007]", 3, {"cited": [1, 7], "invalid": [7], "ok": False}),
        ("[ 1 ] [a] [1,2] [] (1)", 3, {"cited": [], "invalid": [], "ok": False}),
        ("[1]", 0, {"cited": [1], "invalid": [1], "ok": False}),
        ("", 2, {"cited": [], "invalid": [], "ok": False}),
        ("[3] [1] [2]", 3, {"cited": [1, 2, 3], "invalid": [], "ok": True}),
    ]
    for text, n, want in cases:
        got = m.check_citations(text, n)
        if got != want:
            return f"check_citations({text!r}, {n}): ожидалось {want!r}, получили {got!r}"
        if not all(type(x) is int for x in got["cited"]) or type(got["ok"]) is not bool:
            return "номера ссылок - int, поле ok - bool"


def t_bootstrap_basic():
    scores = [1.0, 0.0, 1.0, 1.0, 0.5, 0.0, 1.0, 0.25, 0.75, 1.0]
    got = m.bootstrap_ci(scores)
    want = ref_ci(scores)
    if not (isinstance(got, tuple) and len(got) == 2 and close(got[0], want[0], 1e-12) and close(got[1], want[1], 1e-12)):
        return f"при seed = 0 и n_boot = 1000 ожидалось {want!r}, получили {got!r}"
    if not all(isinstance(x, float) for x in got):
        return "границы интервала - float"
    low, high = got
    mean = sum(scores) / len(scores)
    if not (low < mean < high):
        return f"интервал {got!r} должен содержать среднее {mean}"
    if m.bootstrap_ci(scores, seed=0) != m.bootstrap_ci(scores, seed=0):
        return "один и тот же seed даёт один и тот же интервал"
    if m.bootstrap_ci(scores, seed=1) == m.bootstrap_ci(scores, seed=2):
        return "разные seed дают разные случайные выборки"


def t_bootstrap_edge():
    got = m.bootstrap_ci([0.7] * 12)
    if got[0] != got[1] or not close(got[0], 0.7):
        return f"одинаковые оценки дают интервал нулевой ширины, получили {got!r}"
    got = m.bootstrap_ci([0.25])
    if not close(got[0], 0.25) or not close(got[1], 0.25):
        return "одна оценка - интервал нулевой ширины"
    scores = list(np.random.default_rng(5).random(40))
    for kwargs in ({"n_boot": 300, "seed": 7}, {"level": 0.8}, {"level": 0.5, "seed": 3}, {"n_boot": 1}):
        got, want = m.bootstrap_ci(scores, **kwargs), ref_ci(scores, **kwargs)
        if not (close(got[0], want[0], 1e-12) and close(got[1], want[1], 1e-12)):
            return f"параметры {kwargs}: ожидалось {want!r}, получили {got!r}"
    narrow, wide = m.bootstrap_ci(scores, level=0.5), m.bootstrap_ci(scores, level=0.95)
    if not (wide[0] <= narrow[0] <= narrow[1] <= wide[1]):
        return "интервал для level = 0.5 лежит внутри интервала для level = 0.95 при том же seed"
    arr = np.array(scores)
    before = arr.copy()
    got = m.bootstrap_ci(arr)
    if not np.array_equal(arr, before):
        return "входной массив не должен изменяться"
    if not close(got[0], ref_ci(scores)[0], 1e-12):
        return "на вход допустим как список, так и массив NumPy"
    if not raises(m.bootstrap_ci, []):
        return "пустой список оценок - ValueError"


def t_evaluate_aggregate():
    examples = [{"refs": ["Москва"], "n_sources": 2}, {"refs": ["красный"], "n_sources": 1}]
    got = m.evaluate(examples, ["Москва", "красный [1]"])
    want = {"em": 0.5, "f1": 5 / 6, "citations_ok": 0.5, "n": 2}
    if set(got) != set(want) or got["n"] != 2 or not all(close(got[k], want[k]) for k in ("em", "f1", "citations_ok")):
        return f"ожидалось {want!r}, получили {got!r}"
    examples = [
        {"refs": ["1998", "в 1998 году"], "n_sources": 3},
        {"refs": ["нет данных"], "n_sources": 1},
        {"refs": ["красная", "красный"], "n_sources": 2},
        {"refs": ["тихий океан"], "n_sources": 4},
        {"refs": ["Ёж"], "n_sources": 2},
    ]
    preds = ["в 1998 году", "нет данных [1] [2]", "красный", "Атлантический океан [4]", "еж [2]"]
    got = m.evaluate(examples, preds)
    want = {
        "em": sum(ref_em(p, e["refs"]) for e, p in zip(examples, preds)) / 5,
        "f1": sum(ref_f1(p, e["refs"]) for e, p in zip(examples, preds)) / 5,
        "citations_ok": sum(ref_cit(p, e["n_sources"])["ok"] for e, p in zip(examples, preds)) / 5,
        "n": 5,
    }
    if got["n"] != 5 or not all(close(got[k], want[k]) for k in ("em", "f1", "citations_ok")):
        return f"ожидалось {want!r}, получили {got!r}"
    if not isinstance(got["n"], int):
        return "n - целое число"


def t_evaluate_errors():
    ex = [{"refs": ["a"], "n_sources": 1}]
    if not raises(m.evaluate, ex, []):
        return "разное число примеров и ответов - ValueError"
    if not raises(m.evaluate, ex * 2, ["a"]):
        return "разное число примеров и ответов - ValueError"
    if not raises(m.evaluate, [], []):
        return "пустой набор - ValueError"
    if not raises(m.evaluate, [{"refs": [], "n_sources": 1}], ["a"]):
        return "пустой список эталонов у примера - ValueError"
    same = m.evaluate(ex, ["a [1]"])
    if same["citations_ok"] != 1.0 or same["n"] != 1:
        return f"один пример: получили {same!r}"


case("normalize-basic", t_normalize_basic)
case("em-f1-basic", t_em_f1_basic)
case("citations-basic", t_citations_basic)
case("normalize-edge", t_normalize_edge)
case("em-edge", t_em_edge)
case("f1-values", t_f1_values)
case("f1-edge", t_f1_edge)
case("citations-edge", t_citations_edge)
case("bootstrap-basic", t_bootstrap_basic)
case("bootstrap-edge", t_bootstrap_edge)
case("evaluate-aggregate", t_evaluate_aggregate)
case("evaluate-errors", t_evaluate_errors)
