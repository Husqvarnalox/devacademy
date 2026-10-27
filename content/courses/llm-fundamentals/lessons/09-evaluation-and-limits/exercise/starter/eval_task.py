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
    # TODO
    return ""


def exact_match(pred, refs):
    """1.0, если нормализованный pred равен хотя бы одному нормализованному эталону из refs, иначе 0.0.

    Пустой список эталонов - ValueError.
    """
    # TODO
    return 0.0


def token_f1(pred, refs):
    """Максимум по эталонам F1 между токенами ответа и эталона (токены - слова нормализованной строки).

    Пересечение считается как мультимножество (collections.Counter): слово, встретившееся дважды в ответе и
    один раз в эталоне, засчитывается один раз. Оба списка токенов пустые - 1.0, пустой только один - 0.0.
    Пустой список эталонов - ValueError.
    """
    # TODO
    return 0.0


def check_citations(answer, n_sources):
    """Найти в ответе ссылки вида [N] и проверить их по числу источников.

    Возвращает словарь {"cited": отсортированный список уникальных номеров ссылок (int),
    "invalid": отсортированный список тех из них, что не лежат в диапазоне 1..n_sources,
    "ok": True, если есть хотя бы одна ссылка и нет некорректных}.
    Ссылки ищутся регулярным выражением \\[(\\d+)\\].
    """
    # TODO
    return {"cited": [], "invalid": [], "ok": False}


def bootstrap_ci(scores, n_boot=1000, seed=0, level=0.95):
    """Бутстрэп-доверительный интервал для среднего -> (low, high), оба float.

    rng = np.random.default_rng(seed); means = rng.choice(scores, size=(n_boot, n)).mean(axis=1) - один вызов
    choice, где n = len(scores); границы - np.quantile(means, [(1 - level) / 2, 1 - (1 - level) / 2]).
    Пустой список оценок - ValueError.
    """
    # TODO
    return (0.0, 0.0)


def evaluate(examples, predictions):
    """Сводные метрики по набору.

    examples - список словарей {"refs": [эталоны], "n_sources": int}, predictions - список строк того же размера.
    Возвращает {"em": среднее exact_match, "f1": среднее token_f1, "citations_ok": доля ответов, у которых
    check_citations(...)["ok"] истинно, "n": число примеров}. Списки разной длины - ValueError.
    Для пустых списков - ValueError.
    """
    # TODO
    return {"em": 0.0, "f1": 0.0, "citations_ok": 0.0, "n": 0}
