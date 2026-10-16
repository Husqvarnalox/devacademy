import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import window_task as m


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


def t_window_shape_values():
    a = np.arange(6, dtype=np.int64)
    v = m.window_view(a, 3)
    if v.shape != (4, 3) or v.tolist() != [[0, 1, 2], [1, 2, 3], [2, 3, 4], [3, 4, 5]]:
        return f"ожидались 4 окна по 3 элемента, получили shape {v.shape}: {v.tolist()}"


def t_window_is_view():
    a = np.arange(10.0)
    v = m.window_view(a, 4)
    if not np.shares_memory(a, v):
        return "window_view должна быть представлением (np.shares_memory(a, v))"
    if v.flags.writeable:
        return "окна перекрываются, поэтому запись должна быть запрещена (writeable=False)"


def t_moving_average():
    a = np.array([1, 2, 3, 4, 10], dtype=np.int64)
    out = m.moving_average(a, 2)
    if out.dtype != np.float64 or out.shape != (4,) or not np.allclose(out, [1.5, 2.5, 3.5, 7.0], rtol=0, atol=1e-12):
        return f"moving_average([1,2,3,4,10], 2) -> float64 [1.5 2.5 3.5 7.], получили {out!r} {out.dtype}"


def t_moving_average_no_mutation():
    a = np.arange(8, dtype=np.int32)
    snap = a.copy()
    out = m.moving_average(a, 3)
    if not np.array_equal(a, snap) or a.dtype != np.int32:
        return "входной массив нельзя менять или приводить на месте"
    if not np.allclose(out, [1, 2, 3, 4, 5, 6], rtol=0, atol=1e-12):
        return f"ожидалось [1 2 3 4 5 6], получили {out!r}"


def t_bad_window():
    for w in (0, 6, -1):
        try:
            m.window_view(np.arange(5), w)
        except ValueError:
            continue
        return f"window_view(arange(5), {w}) должна бросать ValueError"


def t_w_equals_len_and_one():
    a = np.array([4.0, 6.0, 8.0])
    one = m.moving_average(a, 1)
    full = m.moving_average(a, 3)
    if not (np.allclose(one, a, rtol=0, atol=1e-12) and one.shape == (3,)):
        return f"w=1 должно вернуть исходные значения, получили {one!r}"
    if not (full.shape == (1,) and np.allclose(full, [6.0], rtol=0, atol=1e-12)):
        return f"w=len(a) даёт одно значение 6.0, получили {full!r}"


case("window-shape-values", t_window_shape_values)
case("window-is-view", t_window_is_view)
case("moving-average", t_moving_average)
case("moving-average-no-mutation", t_moving_average_no_mutation)
case("bad-window", t_bad_window)
case("w-edge", t_w_equals_len_and_one)
