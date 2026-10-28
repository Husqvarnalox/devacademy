import re
import unicodedata
from collections import Counter

import numpy as np


def normalize_answer(s):
    """Строка -> нормализованная строка для сравнения ответов.

    Шаги: нижний регистр, "ё" -> "е", каждый символ, у которого unicodedata.category(ch) начинается с "P"
    (знаки препинания), заменяется пробелом, затем подряд идущие пробельные символы схлопываются в один пробел,
    пробелы по краям убираются.
    Пример: "  Москва, столица!  " -> "москва столица".
    """
    s = s.lower().replace("ё", "е")
    s = "".join(" " if unicodedata.category(ch).startswith("P") else ch for ch in s)
    return " ".join(s.split())


def exact_match(pred, refs):
    """1.0, если нормализованный pred равен хотя бы одному нормализованному эталону из refs, иначе 0.0.

    Пустой список эталонов - ValueError.
    """
    if not refs:
        raise ValueError("refs must not be empty")
    p = normalize_answer(pred)
    return 1.0 if any(p == normalize_answer(r) for r in refs) else 0.0


def _f1(pred_tokens, ref_tokens):
    if not pred_tokens and not ref_tokens:
        return 1.0
    if not pred_tokens or not ref_tokens:
        return 0.0
    common = sum((Counter(pred_tokens) & Counter(ref_tokens)).values())
    if common == 0:
        return 0.0
    precision = common / len(pred_tokens)
    recall = common / len(ref_tokens)
    return 2 * precision * recall / (precision + recall)


def token_f1(pred, refs):
    """Максимум по эталонам F1 между токенами ответа и эталона (токены - слова нормализованной строки).

    Пересечение считается как мультимножество (collections.Counter): слово, встретившееся дважды в ответе и
    один раз в эталоне, засчитывается один раз. Оба списка токенов пустые - 1.0, пустой только один - 0.0.
    Пустой список эталонов - ValueError.
    """
    if not refs:
        raise ValueError("refs must not be empty")
    p = normalize_answer(pred).split()
    return max(_f1(p, normalize_answer(r).split()) for r in refs)


def check_citations(answer, n_sources):
    """Найти в ответе ссылки вида [N] и проверить их по числу источников.

    Возвращает словарь {"cited": отсортированный список уникальных номеров ссылок (int),
    "invalid": отсортированный список тех из них, что не лежат в диапазоне 1..n_sources,
    "ok": True, если есть хотя бы одна ссылка и нет некорректных}.
    Ссылки ищутся регулярным выражением \\[(\\d+)\\].
    """
    cited = sorted({int(x) for x in re.findall(r"\[(\d+)\]", answer)})
    invalid = [c for c in cited if not 1 <= c <= n_sources]
    return {"cited": cited, "invalid": invalid, "ok": bool(cited) and not invalid}


def bootstrap_ci(scores, n_boot=1000, seed=0, level=0.95):
    """Бутстрэп-доверительный интервал для среднего -> (low, high), оба float.

    rng = np.random.default_rng(seed); means = rng.choice(scores, size=(n_boot, n)).mean(axis=1) - один вызов
    choice, где n = len(scores); границы - np.quantile(means, [(1 - level) / 2, 1 - (1 - level) / 2]).
    Пустой список оценок - ValueError.
    """
    scores = np.asarray(scores, dtype=float)
    if scores.size == 0:
        raise ValueError("scores must not be empty")
    rng = np.random.default_rng(seed)
    means = rng.choice(scores, size=(n_boot, scores.size)).mean(axis=1)
    low, high = np.quantile(means, [(1 - level) / 2, 1 - (1 - level) / 2])
    return float(low), float(high)


def evaluate(examples, predictions):
    """Сводные метрики по набору.

    examples - список словарей {"refs": [эталоны], "n_sources": int}, predictions - список строк того же размера.
    Возвращает {"em": среднее exact_match, "f1": среднее token_f1, "citations_ok": доля ответов, у которых
    check_citations(...)["ok"] истинно, "n": число примеров}. Списки разной длины - ValueError.
    Для пустых списков - ValueError.
    """
    if len(examples) != len(predictions):
        raise ValueError("examples and predictions must have the same length")
    if not examples:
        raise ValueError("examples must not be empty")
    n = len(examples)
    em = sum(exact_match(p, e["refs"]) for e, p in zip(examples, predictions)) / n
    f1 = sum(token_f1(p, e["refs"]) for e, p in zip(examples, predictions)) / n
    cit = sum(check_citations(p, e["n_sources"])["ok"] for e, p in zip(examples, predictions)) / n
    return {"em": em, "f1": f1, "citations_ok": cit, "n": n}
