import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import ndarray_task as m


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


def t_describe_basic():
    a = np.arange(12, dtype=np.int32).reshape(3, 4)
    d = m.describe(a)
    want = {"shape": (3, 4), "dtype": "int32", "itemsize": 4, "nbytes": 48, "strides": (16, 4), "c_contiguous": True}
    if not isinstance(d, dict) or set(d) != set(want):
        return f"ожидались ключи {sorted(want)}, получили {sorted(d) if isinstance(d, dict) else d!r}"
    for key, val in want.items():
        if d[key] != val or type(d[key]) is not type(val):
            return f"describe(...)[{key!r}]: ожидалось {val!r} ({type(val).__name__}), получили {d[key]!r} ({type(d[key]).__name__})"


def t_scale_array():
    a = np.array([1, 2, 3], dtype=np.int64)
    out = m.scale_all(a, 2.5)
    if not isinstance(out, np.ndarray) or out.dtype != np.float64 or out.shape != (3,):
        return f"нужен ndarray float64 формы (3,), получили {type(out).__name__} {getattr(out, 'dtype', None)} {getattr(out, 'shape', None)}"
    if not np.allclose(out, [2.5, 5.0, 7.5], rtol=0, atol=1e-12):
        return f"ожидалось [2.5 5. 7.5], получили {out!r}"
    if a.dtype != np.int64 or list(a) != [1, 2, 3]:
        return "входной массив нельзя менять"


def t_c_strides_2d():
    got = m.c_strides((3, 4), 8)
    if got != (32, 8) or not all(type(x) is int for x in got):
        return f"c_strides((3, 4), 8) должно быть (32, 8) из int, получили {got!r}"
    if m.c_strides((5,), 4) != (4,):
        return f"c_strides((5,), 4) должно быть (4,), получили {m.c_strides((5,), 4)!r}"


def t_scale_list():
    out = m.scale_all([1, 2, 3], 2)
    if not isinstance(out, np.ndarray):
        return f"для списка нужно вернуть ndarray, получили {type(out).__name__} (list * 2 повторяет список!)"
    if out.shape != (3,) or out.dtype != np.float64 or not np.allclose(out, [2.0, 4.0, 6.0], rtol=0, atol=1e-12):
        return f"scale_all([1, 2, 3], 2) должно дать float64 [2. 4. 6.], получили {out!r} {out.dtype} {out.shape}"
    nested = m.scale_all([[1, 2], [3, 4]], 0.5)
    if nested.shape != (2, 2) or not np.allclose(nested, [[0.5, 1.0], [1.5, 2.0]], rtol=0, atol=1e-12):
        return f"для вложенного списка форма должна сохраниться: {nested!r}"


def t_describe_noncontiguous():
    base = np.arange(24, dtype=np.float64).reshape(4, 6)
    d = m.describe(base[:, ::2])
    if d["shape"] != (4, 3) or d["strides"] != (48, 16) or d["c_contiguous"] is not False:
        return f"для base[:, ::2] ожидалось shape (4, 3), strides (48, 16), c_contiguous False; получили {d['shape']}, {d['strides']}, {d['c_contiguous']}"
    t = m.describe(base.T)
    if t["strides"] != (8, 48) or t["c_contiguous"] is not False or t["nbytes"] != 192:
        return f"для base.T ожидалось strides (8, 48), c_contiguous False, nbytes 192; получили {t}"


def t_c_strides_nd():
    for shape, item in [((2, 3, 4), 2), ((), 8), ((1, 7), 1), ((4, 1, 5), 4)]:
        want = np.empty(shape, dtype=f"V{item}").strides
        got = m.c_strides(shape, item)
        if tuple(got) != tuple(want):
            return f"c_strides({shape}, {item}): ожидалось {want}, получили {got}"


def t_describe_itemsizes():
    for dt in ("uint8", "float32", "complex128", "bool"):
        a = np.zeros((2, 5), dtype=dt)
        d = m.describe(a)
        if d["dtype"] != dt or d["itemsize"] != a.itemsize or d["nbytes"] != 10 * a.itemsize:
            return f"dtype {dt}: получили {d}"


case("describe-basic", t_describe_basic)
case("scale-array", t_scale_array)
case("c-strides-2d", t_c_strides_2d)
case("scale-list", t_scale_list)
case("describe-noncontiguous", t_describe_noncontiguous)
case("c-strides-nd", t_c_strides_nd)
case("describe-itemsizes", t_describe_itemsizes)
