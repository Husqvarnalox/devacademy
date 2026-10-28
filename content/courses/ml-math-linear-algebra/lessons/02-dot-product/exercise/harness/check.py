import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import similarity_task as m


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


def near(a, b, tol=1e-9):
    return isinstance(a, (int, float)) and abs(a - b) <= tol


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def t_dot_norm():
    got = m.dot([1.0, 2.0, 3.0], [4.0, 5.0, 6.0])
    if not near(got, 32.0):
        return f"dot([1, 2, 3], [4, 5, 6]) должна вернуть 32, получили {got!r}"
    got = m.dot([3.0, 4.0], [4.0, -3.0])
    if not near(got, 0.0):
        return f"векторы [3, 4] и [4, -3] ортогональны, dot должна вернуть 0, получили {got!r}"
    for p, want in ((1, 7.0), (2, 5.0), (math.inf, 4.0)):
        got = m.norm([3.0, -4.0], p)
        if not near(got, want):
            return f"norm([3, -4], p={p}) должна вернуть {want}, получили {got!r}"
    got = m.norm([5.0, 12.0], 2)
    if not near(got, 13.0):
        return f"длина вектора [5, 12] равна 13, получили {got!r}"
    got = m.norm([3.0, 4.0])
    if not near(got, 5.0):
        return f"norm([3, 4]) по умолчанию (p=2) должна вернуть 5, получили {got!r}"


def t_cosine():
    got = m.cosine_similarity([1.0, 0.0, 2.0, 1.0], [2.0, 0.0, 4.0, 2.0])
    if not near(got, 1.0):
        return f"векторы одного направления дают косинус 1, получили {got!r}"
    got = m.cosine_similarity([1.0, 0.0, 2.0, 1.0], [-1.0, 0.0, -2.0, -1.0])
    if not near(got, -1.0):
        return f"противоположные векторы дают косинус -1, получили {got!r}"
    got = m.cosine_similarity([1.0, 0.0], [1.0, 1.0])
    if not near(got, math.sqrt(0.5)):
        return f"угол между [1, 0] и [1, 1] равен 45 градусов, косинус {math.sqrt(0.5):.6f}, получили {got!r}"
    got = m.cosine_similarity([1.0, 2.0], [2.0, -1.0])
    if not near(got, 0.0):
        return f"ортогональные векторы дают косинус 0, получили {got!r}"
    big = m.cosine_similarity([10.0, 0.0], [3.0, 0.0])
    if not near(big, 1.0):
        return "косинус не зависит от длины векторов"


def t_top_k():
    docs = [[0.0, 1.0], [1.0, 0.1], [1.0, 1.0], [-1.0, 0.0]]
    got = m.top_k_similar([1.0, 0.0], docs, 2)
    if got != [1, 2]:
        return f"два ближайших к [1, 0] из четырёх документов: индексы [1, 2], получили {got!r}"
    got = m.top_k_similar([1.0, 0.0], docs, 4)
    if got != [1, 2, 0, 3]:
        return f"порядок по убыванию косинуса: [1, 2, 0, 3], получили {got!r}"


def t_errors():
    if not raises_value_error(m.dot, [1.0, 2.0], [1.0]):
        return "dot для векторов разной длины должна выбросить ValueError"
    if not raises_value_error(m.cosine_similarity, [1.0, 2.0], [1.0]):
        return "cosine_similarity для векторов разной длины должна выбросить ValueError"
    if not raises_value_error(m.cosine_similarity, [0.0, 0.0], [1.0, 2.0]):
        return "cosine_similarity с нулевым вектором должна выбросить ValueError, а не ZeroDivisionError"
    if not raises_value_error(m.cosine_similarity, [1.0, 2.0], [0.0, 0.0]):
        return "cosine_similarity со вторым нулевым вектором должна выбросить ValueError"
    if not raises_value_error(m.norm, [1.0, 2.0], 3):
        return "norm с p = 3 должна выбросить ValueError"
    if not raises_value_error(m.norm, [1.0, 2.0], 0):
        return "norm с p = 0 должна выбросить ValueError"
    if not raises_value_error(m.top_k_similar, [0.0, 0.0], [[1.0, 0.0]], 1):
        return "top_k_similar с нулевым запросом должна выбросить ValueError"


def t_norm_edge():
    for p in (1, 2, math.inf):
        got = m.norm([], p)
        if not near(got, 0.0):
            return f"норма пустого вектора (p={p}) равна 0.0, получили {got!r}"
    got = m.norm([-7.0], math.inf)
    if not near(got, 7.0):
        return f"L-бесконечность вектора [-7] равна 7 (модуль), получили {got!r}"
    got = m.norm([1, -2, 2])
    if not near(got, 3.0):
        return f"длина целочисленного вектора [1, -2, 2] равна 3, получили {got!r}"
    got = m.dot([1, 2, 3], [4, 5, 6])
    if not near(got, 32.0):
        return f"dot должна принимать целые числа, получили {got!r}"
    if not near(m.dot([], []), 0.0):
        return "скалярное произведение пустых векторов равно 0"
    got = m.norm([0.5, -1.5, 2.0], 1)
    if not near(got, 4.0):
        return f"L1-норма [0.5, -1.5, 2] равна 4, получили {got!r}"


def t_top_k_edge():
    q = [1.0, 0.0]
    # равные сходства: меньший индекс первым
    docs = [[0.0, 1.0], [2.0, 0.0], [2.0, 0.0], [2.0, 0.0]]
    got = m.top_k_similar(q, docs, 3)
    if got != [1, 2, 3]:
        return f"при равных сходствах выигрывает меньший индекс: [1, 2, 3], получили {got!r}"
    # нулевые векторы пропускаются
    docs = [[0.0, 0.0], [0.0, 1.0], [1.0, 1.0]]
    got = m.top_k_similar(q, docs, 3)
    if got != [2, 1]:
        return f"нулевой вектор (индекс 0) пропускается: ожидали [2, 1], получили {got!r}"
    # k больше числа кандидатов
    got = m.top_k_similar(q, [[1.0, 0.0]], 10)
    if got != [0]:
        return f"при k больше числа векторов возвращаются все: [0], получили {got!r}"
    if m.top_k_similar(q, [[1.0, 0.0]], 0) != [] or m.top_k_similar(q, [[1.0, 0.0]], -2) != []:
        return "для k <= 0 нужно вернуть []"
    if m.top_k_similar(q, [], 3) != []:
        return "для пустого списка векторов нужно вернуть []"
    # сравнение с независимым расчётом на детерминированных данных
    vs = [[((i * 7 + j * 3) % 11) - 5.0 for j in range(4)] for i in range(12)]
    qq = [1.0, -2.0, 3.0, 0.5]
    want = []
    for i, v in enumerate(vs):
        nv = math.sqrt(sum(a * a for a in v))
        if nv == 0.0:
            continue
        c = sum(a * b for a, b in zip(qq, v)) / (math.sqrt(sum(a * a for a in qq)) * nv)
        want.append((-c, i))
    want = [i for _, i in sorted(want)][:5]
    got = m.top_k_similar(qq, vs, 5)
    if got != want:
        return f"пять ближайших на проверочных данных: ожидали {want}, получили {got}"


def t_no_mutation():
    u, v = [1.0, 2.0], [3.0, 4.0]
    docs = [[1.0, 0.0], [0.0, 1.0]]
    m.dot(u, v)
    m.norm(u, 1)
    m.cosine_similarity(u, v)
    m.top_k_similar(u, docs, 1)
    if u != [1.0, 2.0] or v != [3.0, 4.0] or docs != [[1.0, 0.0], [0.0, 1.0]]:
        return "функции не должны изменять входные списки"
    r = m.top_k_similar(u, docs, 2)
    if not isinstance(r, list) or not all(isinstance(i, int) for i in r):
        return "top_k_similar возвращает список целых индексов"


case("dot-norm", t_dot_norm)
case("cosine", t_cosine)
case("top-k", t_top_k)
case("errors", t_errors)
case("norm-edge", t_norm_edge)
case("top-k-edge", t_top_k_edge)
case("no-mutation", t_no_mutation)
