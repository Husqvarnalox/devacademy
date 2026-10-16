import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import dtype_task as m


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


def t_wrap_add_basic():
    out = m.wrap_add([1, 2, 3], [10, 20, 30], "int16")
    if not isinstance(out, np.ndarray) or out.dtype != np.int16 or out.shape != (3,):
        return f"нужен ndarray int16 формы (3,), получили {getattr(out, 'dtype', type(out))} {getattr(out, 'shape', None)}"
    if list(out) != [11, 22, 33]:
        return f"ожидалось [11, 22, 33], получили {out!r}"


def t_widen_add_basic():
    out = m.widen_add(np.array([1, 2], dtype=np.int32), np.array([3, 4], dtype=np.int32))
    if not isinstance(out, np.ndarray) or out.dtype != np.int64 or list(out) != [4, 6]:
        return f"ожидался int64 [4, 6], получили {out!r} {getattr(out, 'dtype', None)}"


def t_to_uint8_basic():
    out = m.to_uint8([0.0, 0.5, 1.0])
    if not isinstance(out, np.ndarray) or out.dtype != np.uint8 or list(out) != [0, 128, 255]:
        return f"to_uint8([0, 0.5, 1]) должно дать uint8 [0, 128, 255], получили {out!r} {getattr(out, 'dtype', None)} (0.5*255 = 127.5 округляется до 128)"


def t_promoted_basic():
    got = m.promoted_dtype("int8", "int16")
    if got != "int16" or not isinstance(got, str):
        return f"promoted_dtype('int8', 'int16') должно быть строкой 'int16', получили {got!r}"
    if m.promoted_dtype("float32", "float64") != "float64":
        return "float32 + float64 даёт float64"


def t_widen_add_overflow():
    a = np.array([200, 255, 100], dtype=np.uint8)
    b = np.array([100, 255, 50], dtype=np.uint8)
    out = m.widen_add(a, b)
    if out.dtype != np.int64 or list(out) != [300, 510, 150]:
        return f"uint8 200+100 должно давать 300 без переполнения; получили {out!r} {out.dtype}"
    if a.dtype != np.uint8 or list(a) != [200, 255, 100]:
        return "входные массивы нельзя менять"
    i8 = m.widen_add(np.array([127], dtype=np.int8), np.array([127], dtype=np.int8))
    if list(i8) != [254] or i8.dtype != np.int64:
        return f"int8 127+127 должно дать 254 (int64), получили {i8!r}"


def t_wrap_add_overflow():
    out = m.wrap_add([100, 120, -100], [100, 10, -100], "int8")
    if out.dtype != np.int8 or list(out) != [-56, -126, 56]:
        return f"int8: 100+100 -> -56, 120+10 -> -126, -100-100 -> 56; получили {out!r} {out.dtype}"
    out = m.wrap_add([250, 255], [10, 1], "uint8")
    if out.dtype != np.uint8 or list(out) != [4, 0]:
        return f"uint8: 250+10 -> 4, 255+1 -> 0; получили {out!r} {out.dtype}"
    big = 2 ** 63 - 1
    out = m.wrap_add([big], [1], "int64")
    if out.dtype != np.int64 or int(out[0]) != -(2 ** 63):
        return f"int64: 2**63-1 + 1 заворачивается в -2**63, получили {out!r}"


def t_to_uint8_clip_round():
    x = np.array([-0.1, 0.0, 0.002, 0.5, 0.999, 1.0, 1.2, 7.0])
    out = m.to_uint8(x)
    want = [0, 0, 1, 128, 255, 255, 255, 255]
    if out.dtype != np.uint8 or out.shape != x.shape:
        return f"нужен uint8 той же формы, получили {out.dtype} {out.shape}"
    if list(out) != want:
        return f"ожидалось {want}, получили {list(out)} (астип без округления обрезает дробную часть, а значения вне [0, 1] нужно сначала ограничить)"
    img = np.linspace(0, 1, 6).reshape(2, 3)
    if m.to_uint8(img).shape != (2, 3):
        return "форма 2D-входа должна сохраняться"


def t_promoted_more():
    cases = {("int8", "uint8"): "int16", ("int64", "float32"): "float64", ("bool", "int8"): "int8",
             ("uint64", "int64"): "float64", ("int16", "float32"): "float32", ("uint8", "uint8"): "uint8"}
    for (a, b), want in cases.items():
        got = m.promoted_dtype(a, b)
        if got != want:
            return f"promoted_dtype({a!r}, {b!r}) должно быть {want!r}, получили {got!r}"


case("wrap-add-basic", t_wrap_add_basic)
case("widen-add-basic", t_widen_add_basic)
case("to-uint8-basic", t_to_uint8_basic)
case("promoted-basic", t_promoted_basic)
case("widen-add-overflow", t_widen_add_overflow)
case("wrap-add-overflow", t_wrap_add_overflow)
case("to-uint8-clip-round", t_to_uint8_clip_round)
case("promoted-more", t_promoted_more)
