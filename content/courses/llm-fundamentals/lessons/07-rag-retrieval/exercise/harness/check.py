import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import rag_task as m


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


# ---- независимые эталоны (не используют код ученика) ----

def ref_tokens(text):
    import re
    return re.findall(r"\w+", text.lower().replace("ё", "е"))


DOCS = [
    "Читательский билет оформляется бесплатно при предъявлении паспорта.",
    "Книги выдаются на двадцать один день, срок можно продлить один раз.",
    "За каждый просроченный день возврата книги начисляется штраф.",
    "Читальный зал открыт со вторника по субботу, в понедельник библиотека закрыта.",
    "Редкие издания выдаются только в читальном зале и на дом не выдаются.",
]


def ref_index(chunks):
    terms = sorted({t for c in chunks for t in c})
    col = {t: i for i, t in enumerate(terms)}
    n = len(chunks)
    tf = np.zeros((n, len(terms)))
    for i, c in enumerate(chunks):
        for t in c:
            tf[i, col[t]] += 1
    df = (tf > 0).sum(axis=0)
    idf = np.log((1 + n) / (1 + df)) + 1
    mat = tf * idf
    for i in range(n):
        norm = np.sqrt((mat[i] ** 2).sum())
        if norm > 0:
            mat[i] = mat[i] / norm
    return col, idf, mat


def ref_search(query, chunks, k, min_score=0.0):
    col, idf, mat = ref_index(chunks)
    q = np.zeros(len(col))
    for t in query:
        if t in col:
            q[col[t]] += 1
    q = q * idf
    if not q.any():
        return []
    q = q / np.sqrt((q ** 2).sum())
    scores = mat @ q
    pairs = [(i, float(s)) for i, s in enumerate(scores) if s > min_score]
    pairs.sort(key=lambda p: (-p[1], p[0]))
    return pairs[:k]


def t_tokenize_basic():
    got = m.tokenize("Сколько дней можно держать книгу?")
    if got != ["сколько", "дней", "можно", "держать", "книгу"]:
        return f"tokenize('Сколько дней можно держать книгу?'): получили {got!r}"
    if m.tokenize("Книга, КНИГА; книга!") != ["книга", "книга", "книга"]:
        return "регистр и знаки препинания не должны влиять на токены"
    if m.tokenize("  ") != [] or m.tokenize("") != []:
        return "строка без слов даёт пустой список"


def t_chunk_basic():
    words = list("abcdefghij")
    got = m.chunk_words(words, 4, 1)
    want = [list("abcd"), list("defg"), list("ghij")]
    if got != want:
        return f"chunk_words(10 слов, size=4, overlap=1): ожидалось {want}, получили {got!r}"
    if words != list("abcdefghij"):
        return "входной список менять нельзя"
    got = m.chunk_words(list("abcdefgh"), 3, 0)
    if got != [list("abc"), list("def"), list("gh")]:
        return f"без перекрытия последний чанк может быть короче: получили {got!r}"


def t_search_ranking():
    chunks = [ref_tokens(d) for d in DOCS]
    index = m.build_index(chunks)
    query = ref_tokens("на сколько дней выдаются книги")
    got = m.search(query, index, 3)
    want = ref_search(query, chunks, 3)
    if [i for i, _ in got] != [i for i, _ in want]:
        return f"порядок чанков: ожидалось {[i for i, _ in want]}, получили {[i for i, _ in got]}"
    if not np.allclose([s for _, s in got], [s for _, s in want], atol=1e-9):
        return f"оценки: ожидалось {[round(s, 4) for _, s in want]}, получили {[round(s, 4) for _, s in got]}"
    if not got or got[0][0] != 1:
        return "лучший чанк для вопроса про срок выдачи книг - чанк 1"
    if not all(isinstance(i, int) and isinstance(s, float) for i, s in got):
        return "элементы результата - пары (int, float)"


def t_tokenize_yo():
    if m.tokenize("Ёлка, ёж и ЁЖИК") != ["елка", "еж", "и", "ежик"]:
        return "«ё» заменяется на «е» после перевода в нижний регистр, в том числе заглавная «Ё»"
    if m.tokenize("Билет №5: 3 руб./лист, a_b") != ["билет", "5", "3", "руб", "лист", "a_b"]:
        return "слова - последовательности \\w+: цифры остаются токенами, знаки препинания отбрасываются, подчёркивание входит в слово"
    if m.tokenize("café, naïve") != ["café", "naïve"]:
        return "\\w+ работает и с не русскими буквами Юникода"


def t_chunk_edges():
    w = list(range(7))
    if m.chunk_words(w, 3, 2) != [[0, 1, 2], [1, 2, 3], [2, 3, 4], [3, 4, 5], [4, 5, 6]]:
        return "overlap = size - 1: шаг 1, чанки с позиций 0..4"
    if m.chunk_words(w, 10, 4) != [w]:
        return "если size не меньше длины списка, получается один чанк"
    if m.chunk_words(w, 7, 3) != [w]:
        return "чанк, дошедший до конца списка, последний: второй чанк, целиком лежащий внутри первого, не нужен"
    if m.chunk_words(list(range(6)), 4, 2) != [[0, 1, 2, 3], [2, 3, 4, 5]]:
        return "6 слов, size = 4, overlap = 2: чанки [0..3] и [2..5], третьего чанка нет"
    if m.chunk_words([], 3, 1) != []:
        return "пустой список слов даёт []"
    if m.chunk_words([9], 1, 0) != [[9]]:
        return "одно слово при size = 1 - один чанк"
    for args in ((0, 0), (-1, 0), (3, -1), (3, 3), (3, 5)):
        try:
            m.chunk_words(w, *args)
        except ValueError:
            pass
        else:
            return f"chunk_words(size={args[0]}, overlap={args[1]}) должна бросать ValueError"
    try:
        m.chunk_words([], 3, 3)
    except ValueError:
        pass
    else:
        return "некорректные size и overlap дают ValueError даже для пустого списка"


def t_index_idf():
    chunks = [["a", "b"], ["a", "c"], ["a"]]
    index = m.build_index(chunks)
    if list(index["vocab"].items()) != [("a", 0), ("b", 1), ("c", 2)]:
        return f"vocab: термы по алфавиту с номерами 0, 1, 2; получили {index['vocab']!r}"
    idf = np.asarray(index["idf"])
    want = np.array([1.0, np.log(4 / 2) + 1, np.log(4 / 2) + 1])
    if idf.shape != (3,) or not np.allclose(idf, want, atol=1e-12):
        return f"idf = ln((1 + N) / (1 + df)) + 1: ожидалось {want.round(6).tolist()}, получили {idf.tolist()}"
    mat = np.asarray(index["matrix"])
    if mat.shape != (3, 3) or mat.dtype != np.float64:
        return f"matrix: форма (3, 3) и тип float64, получили {mat.shape} {mat.dtype}"
    row0 = np.array([1.0, want[1], 0.0])
    row0 = row0 / np.linalg.norm(row0)
    if not np.allclose(mat[0], row0, atol=1e-12):
        return f"строка 0: tf * idf и нормировка, ожидалось {row0.round(6).tolist()}, получили {mat[0].tolist()}"
    if not np.allclose(mat[2], [1.0, 0.0, 0.0]):
        return "чанк из одного терма после нормировки - единичный вектор"
    rep = m.build_index([["x", "x", "y"]])
    if not np.allclose(rep["matrix"][0], np.array([2.0, 1.0]) / np.sqrt(5), atol=1e-12):
        return "tf - сырое число вхождений: терм, встретившийся дважды, получает вес 2 до нормировки (при N = 1 idf = 1)"


def t_index_norm():
    chunks = [ref_tokens(d) for d in DOCS] + [[]]
    mat = np.asarray(m.build_index(chunks)["matrix"])
    norms = np.linalg.norm(mat, axis=1)
    if not np.allclose(norms[:-1], 1.0, atol=1e-12):
        return f"каждая непустая строка должна иметь L2-норму 1, получили {norms[:-1].round(6).tolist()}"
    if np.any(mat[-1] != 0) or not np.all(np.isfinite(mat)):
        return "пустой чанк даёт нулевую строку без nan"
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        empty = m.build_index([])
    if np.asarray(empty["matrix"]).shape[0] != 0 or empty["vocab"] != {}:
        return "для пустого списка чанков индекс пуст"


def t_search_ties():
    chunks = [["кот"], ["пес"], ["кот"], ["кот"], ["пес"]]
    index = m.build_index(chunks)
    got = m.search(["кот"], index, 10)
    if got != [(0, 1.0), (2, 1.0), (3, 1.0)]:
        return f"равные оценки упорядочиваются по возрастанию номера чанка; ожидалось [(0, 1.0), (2, 1.0), (3, 1.0)], получили {got!r}"
    if m.search(["кот"], index, 2) != [(0, 1.0), (2, 1.0)]:
        return "не более k результатов, при ничьей остаются чанки с меньшими номерами"
    if m.search(["кот"], index, 0) != [] or m.search(["кот"], index, 1) != [(0, 1.0)]:
        return "k = 0 даёт [], k = 1 - один лучший результат"


def t_search_min_score():
    chunks = [ref_tokens(d) for d in DOCS]
    index = m.build_index(chunks)
    query = ref_tokens("сколько стоит штраф за просрочку книги")
    base = m.search(query, index, 5)
    want = ref_search(query, chunks, 5)
    if [i for i, _ in base] != [i for i, _ in want]:
        return "порядок результатов должен совпасть с эталоном"
    top = base[0][1]
    if m.search(query, index, 5, min_score=top) != [p for p in base if p[1] > top]:
        return "порог строгий: оценка, равная min_score, в результат не входит"
    if m.search(query, index, 5, min_score=2.0) != []:
        return "если ни одна оценка не выше порога, ответ - пустой список (повод отказаться отвечать)"
    cut = (base[0][1] + base[1][1]) / 2
    got = m.search(query, index, 5, min_score=cut)
    if [i for i, _ in got] != [base[0][0]]:
        return "порог отсекает чанки с оценкой не выше min_score"
    if any(s <= 0 for _, s in base):
        return "чанки с нулевой оценкой не входят в результат при min_score = 0"


def t_search_unknown():
    index = m.build_index([["кот", "спит"], ["пес", "лает"]])
    if m.search(["самолет", "ракета"], index, 3) != []:
        return "если в запросе нет ни одного известного терма, ответ - []"
    if m.search([], index, 3) != []:
        return "пустой запрос даёт []"
    a = m.search(["кот", "самолет"], index, 3)
    b = m.search(["кот"], index, 3)
    if a != b or [i for i, _ in a] != [0]:
        return "неизвестные термы игнорируются и не меняют оценки известных"
    rep = m.search(["кот", "кот"], index, 3)
    if [i for i, _ in rep] != [0] or abs(rep[0][1] - b[0][1]) > 1e-12:
        return "повтор терма в запросе не меняет косинус для одного терма"
    two = m.build_index([["a", "b"], ["a"]])
    s = m.search(["a", "b"], two, 2)
    want = ref_search(["a", "b"], [["a", "b"], ["a"]], 2)
    if [i for i, _ in s] != [i for i, _ in want] or not np.allclose([x for _, x in s], [x for _, x in want], atol=1e-12):
        return "оценка - косинус между нормированным вектором запроса (частоты * idf) и строкой matrix"


def t_recall_values():
    retrieved = [[0, 1, 2], [3, 4, 5], [6, 7, 8]]
    relevant = [{0}, {5}, {9}]
    got = m.recall_at_k(retrieved, relevant, 3)
    if abs(got - 2 / 3) > 1e-12:
        return f"recall@3: ожидалось 2/3, получили {got!r}"
    if abs(m.recall_at_k(retrieved, relevant, 1) - 1 / 3) > 1e-12:
        return "recall@1: найден только первый запрос, ожидалось 1/3"
    if m.recall_at_k([[1, 2, 3]], [{1, 2, 3, 4}], 2) != 0.5:
        return "k меньше числа релевантных: среди первых 2 найдено 2 из 4 релевантных, recall = 0.5"
    if m.recall_at_k([[1, 2, 3]], [[1, 2, 3, 4]], 3) != 0.75:
        return "relevant можно передать и списком; 3 из 4 -> 0.75"
    if m.recall_at_k([[1, 1, 2]], [{1, 2}], 3) != 1.0:
        return "повторы в retrieved не должны завышать recall: множество пересекается с relevant"
    if m.recall_at_k([[5, 6]], [{5, 6}], 10) != 1.0 or m.recall_at_k([[7]], [{5}], 0) != 0.0:
        return "k больше длины списка допустим; k = 0 даёт 0"


def t_recall_edges():
    got = m.recall_at_k([[1], [2], [3]], [{1}, set(), {9}], 1)
    if got != 0.5:
        return f"запрос с пустым множеством релевантных пропускается (не считается за 0): ожидалось 0.5, получили {got!r}"
    for retrieved, relevant in (([[1]], [set()]), ([], []), ([[1], [2]], [set(), set()])):
        try:
            m.recall_at_k(retrieved, relevant, 3)
        except ValueError:
            pass
        else:
            return "если не осталось запросов с релевантными чанками, ожидался ValueError"
    try:
        m.recall_at_k([[1]], [{1}, {2}], 1)
    except ValueError:
        pass
    else:
        return "разная длина retrieved и relevant - ValueError"


case("tokenize-basic", t_tokenize_basic)
case("chunk-basic", t_chunk_basic)
case("search-ranking", t_search_ranking)
case("tokenize-yo", t_tokenize_yo)
case("chunk-edges", t_chunk_edges)
case("index-idf", t_index_idf)
case("index-norm", t_index_norm)
case("search-ties", t_search_ties)
case("search-min-score", t_search_min_score)
case("search-unknown", t_search_unknown)
case("recall-values", t_recall_values)
case("recall-edges", t_recall_edges)
