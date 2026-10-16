import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import knn_task as m


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


def expect(got, want, call):
    if got != want:
        return f"{call} вернул {got!r}, ожидалось {want!r}"
    return None


# Две группы на плоскости, целочисленные координаты
TX = [(0, 0), (0, 1), (1, 0), (5, 5), (5, 6), (6, 5)]
TY = ["a", "a", "a", "b", "b", "b"]

# Одномерные данные с «выбросом»: точка 4 помечена b среди a-шек
LX = [(0,), (1,), (2,), (3,), (4,), (5,), (10,), (11,), (12,), (13,)]
LY = ["a", "a", "a", "a", "b", "a", "b", "b", "b", "b"]


def t_distance():
    got = m.squared_distance((0, 0), (3, 4))
    bad = expect(got, 25, "squared_distance((0,0),(3,4))")
    if bad:
        return bad
    bad = expect(m.squared_distance((1, 2, 3), (1, 2, 3)), 0, "squared_distance одинаковых точек")
    if bad:
        return bad
    return expect(m.squared_distance((-1,), (2,)), 9, "squared_distance((-1,),(2,))")


def t_basic_classes():
    for q, want in [((0, 0), "a"), ((1, 1), "a"), ((6, 6), "b"), ((4, 5), "b")]:
        bad = expect(m.knn_predict(TX, TY, q, 3), want, f"knn_predict(..., {q}, k=3)")
        if bad:
            return bad


def t_k_equals_one():
    # ближайший сосед точки (2.9,) - (3,), метка a; для (4.2,) - (4,), метка b (выброс)
    tx = [(x,) for x in (0, 1, 2, 3, 4)]
    ty = ["a", "a", "a", "a", "b"]
    bad = expect(m.knn_predict(tx, ty, (4.2,), 1), "b", "knn_predict(k=1) у выброса")
    if bad:
        return bad
    return expect(m.knn_predict(tx, ty, (4.2,), 3), "a", "knn_predict(k=3) сглаживает выброс")


def t_distance_tie_uses_index():
    # Точки 0 и 1 на одинаковом расстоянии от (0,0): индекс 0 идёт раньше
    tx = [(1, 0), (0, 1), (5, 5)]
    ty = ["x", "y", "z"]
    return expect(m.knn_predict(tx, ty, (0, 0), 1), "x", "при равных расстояниях сосед с меньшим индексом идёт первым, knn_predict k=1")


def t_vote_tie_nearest_wins():
    # k=4: два голоса a и два голоса b; ближайший сосед имеет метку b
    tx = [(1,), (2,), (3,), (4,), (100,)]
    ty = ["a", "a", "b", "b", "a"]
    # query = 3.4: расстояния^2: 5.76, 1.96, 0.16, 0.36, 9561.96 -> порядок: idx2 (b), idx3 (b), idx1 (a), idx0 (a)
    bad = expect(m.knn_predict(tx, ty, (3.4,), 4), "b", "ничья голосов (2:2), ближайший сосед с меткой b")
    if bad:
        return bad
    # query = 1.5: порядок idx0 (a), idx1 (a), idx2 (b), idx3 (b)
    return expect(m.knn_predict(tx, ty, (1.5,), 4), "a", "ничья голосов (2:2), ближайший сосед с меткой a")


def t_vote_tie_three_way():
    tx = [(0,), (1,), (2,), (3,), (4,), (5,)]
    ty = ["c", "a", "b", "b", "a", "c"]
    # query = 2.4: порядок idx2 (b), idx3 (b), idx1(a), idx4(a), idx0(c), idx5(c): k=6 -> 2:2:2, первым идёт b
    return expect(m.knn_predict(tx, ty, (2.4,), 6), "b", "ничья трёх меток 2:2:2")


def t_k_larger_than_train():
    tx = [(0,), (1,), (10,)]
    ty = ["p", "p", "q"]
    bad = expect(m.knn_predict(tx, ty, (9.0,), 10), "p", "k больше размера выборки: используются все точки, побеждает p (2:1)")
    return bad


def t_predict_all():
    queries = [(0, 0), (6, 6), (1, 1), (5, 4)]
    return expect(list(m.knn_predict_all(TX, TY, queries, 3)), ["a", "b", "a", "b"], "knn_predict_all")


def t_loo():
    res = []
    for k in (1, 3, 5):
        res.append(m.leave_one_out_accuracy(LX, LY, k))
    want = [0.8, 0.9, 0.9]
    for k, got, w in zip((1, 3, 5), res, want):
        if not isinstance(got, (int, float)) or not math.isfinite(got) or abs(got - w) > 1e-9:
            return f"leave_one_out_accuracy(k={k}) вернула {got!r}, ожидалось {w}"


def t_inputs_not_mutated():
    tx = [(2, 2), (0, 0), (1, 1)]
    ty = ["b", "a", "a"]
    tx_copy, ty_copy = list(tx), list(ty)
    m.knn_predict(tx, ty, (0.4, 0.4), 2)
    m.leave_one_out_accuracy(tx, ty, 1)
    if tx != tx_copy or ty != ty_copy:
        return "функции не должны изменять переданные списки (не сортируйте обучающие данные на месте)"


case("distance", t_distance)
case("basic-classes", t_basic_classes)
case("k-one-vs-three", t_k_equals_one)
case("distance-tie-index", t_distance_tie_uses_index)
case("vote-tie-nearest", t_vote_tie_nearest_wins)
case("vote-tie-three-way", t_vote_tie_three_way)
case("k-larger-than-train", t_k_larger_than_train)
case("predict-all", t_predict_all)
case("leave-one-out", t_loo)
case("inputs-not-mutated", t_inputs_not_mutated)
