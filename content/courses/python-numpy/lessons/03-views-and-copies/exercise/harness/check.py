import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import views_task as m


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


def t_row_view():
    a = np.arange(12).reshape(3, 4)
    r = m.row_view(a, 1)
    if not isinstance(r, np.ndarray) or r.shape != (4,) or r.dtype != a.dtype or list(r) != [4, 5, 6, 7]:
        return f"row_view(a, 1) должно дать строку [4 5 6 7], получили {r!r}"
    if not np.shares_memory(a, r):
        return "row_view должен вернуть представление: np.shares_memory(a, результат) должно быть True"


def t_step_view():
    a = np.arange(10)
    v = m.step_view(a, 3)
    if list(v) != [0, 3, 6, 9] or not np.shares_memory(a, v):
        return f"step_view(arange(10), 3) -> представление [0 3 6 9], получили {v!r}, shares_memory={np.shares_memory(a, v)}"


def t_positives_copy():
    a = np.array([3, -1, 0, 5, -2, 7])
    p = m.positives_copy(a)
    if list(p) != [3, 5, 7] or p.dtype != a.dtype:
        return f"ожидалось [3 5 7], получили {p!r}"
    if np.shares_memory(a, p):
        return "positives_copy должна возвращать копию"


def t_clip_negative_basic():
    a = np.array([[1.0, -2.0], [-3.0, 4.0]])
    c = m.clip_negative(a)
    if c.dtype != a.dtype or c.shape != a.shape or not np.allclose(c, [[1.0, 0.0], [0.0, 4.0]], rtol=0, atol=0):
        return f"ожидалось [[1, 0], [0, 4]], получили {c!r}"


def t_clip_negative_no_mutation():
    a = np.array([5, -1, -7, 2])
    snapshot = a.copy()
    c = m.clip_negative(a)
    if not np.array_equal(a, snapshot):
        return f"вход был изменён: было {snapshot}, стало {a} (c = a или c = a[...] - это не копия, это представление)"
    if np.shares_memory(a, c):
        return "результат не должен делить память с входом"
    if list(c) != [5, 0, 0, 2]:
        return f"ожидалось [5 0 0 2], получили {c!r}"


def t_view_writes_through():
    a = np.zeros((3, 4), dtype=np.int64)
    m.row_view(a, 2)[:] = 9
    if not (a[2] == 9).all() or (a[:2] != 0).any():
        return f"запись в row_view должна менять строку 2 исходного массива, получили {a.tolist()}"
    b = np.arange(8)
    m.step_view(b, 2)[:] = -1
    if list(b) != [-1, 1, -1, 3, -1, 5, -1, 7]:
        return f"запись в step_view должна менять каждый второй элемент, получили {b.tolist()}"


def t_reorder_copy():
    a = np.arange(12).reshape(3, 4)
    snap = a.copy()
    r = m.reorder_columns(a, [3, 0, 2])
    if r.shape != (3, 3) or r.tolist() != [[3, 0, 2], [7, 4, 6], [11, 8, 10]]:
        return f"reorder_columns(a, [3, 0, 2]) -> {r.tolist()}"
    if np.shares_memory(a, r):
        return "fancy-индексирование даёт копию; результат не должен делить память с a"
    r[:] = -5
    if not np.array_equal(a, snap):
        return "запись в результат изменила исходный массив"


def t_noncontiguous_input():
    base = np.arange(24).reshape(4, 6)
    sub = base[:, ::2]           # не C-непрерывное представление
    c = m.clip_negative(sub - 10)
    if c.shape != (4, 3) or (c < 0).any():
        return f"форма и значения для непрерывного/срезанного входа: {c!r}"
    if not np.array_equal(base, np.arange(24).reshape(4, 6)):
        return "базовый массив изменён"
    r = m.row_view(sub, 1)
    if not np.shares_memory(base, r) or list(r) != [6, 8, 10]:
        return f"row_view от среза должен давать представление базового массива: {r!r}"


case("row-view", t_row_view)
case("step-view", t_step_view)
case("positives-copy", t_positives_copy)
case("clip-negative-basic", t_clip_negative_basic)
case("clip-negative-no-mutation", t_clip_negative_no_mutation)
case("view-writes-through", t_view_writes_through)
case("reorder-copy", t_reorder_copy)
case("noncontiguous-input", t_noncontiguous_input)
