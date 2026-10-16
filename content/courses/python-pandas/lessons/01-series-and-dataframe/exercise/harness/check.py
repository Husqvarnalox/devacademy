import json
import os

import pandas as pd
import pandas.testing as pdt

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {str(exc)[:300]}")
    else:
        report(case_id, problem is None, problem or "")


def same_frame(got, want, **kw):
    """None если равны, иначе короткое сообщение."""
    try:
        pdt.assert_frame_equal(got, want, **kw)
    except AssertionError as e:
        return str(e).strip().splitlines()[0][:240] + " | " + " ".join(str(e).strip().splitlines()[1:3])[:200]


def same_series(got, want, **kw):
    try:
        pdt.assert_series_equal(got, want, **kw)
    except AssertionError as e:
        return str(e).strip().splitlines()[0][:240] + " | " + " ".join(str(e).strip().splitlines()[1:3])[:200]

import series_task as m


def t_align_basic():
    a = pd.Series([1, 2, 3], index=["x", "y", "z"])
    b = pd.Series([10, 20, 30], index=["y", "z", "w"])
    got = m.align_sum(a, b)
    if not isinstance(got, pd.Series):
        return f"нужна Series, получили {type(got).__name__}"
    if got.isna().any():
        return "в результате остались NaN: метка без пары должна считаться нулём"
    want = pd.Series([30.0, 1.0, 12.0, 23.0], index=["w", "x", "y", "z"])
    return same_series(got.sort_index(), want, check_dtype=False, check_names=False, rtol=1e-12)


def t_label_position_basic():
    s = pd.Series([5, 6, 7], index=["a", "b", "c"])
    got = m.label_and_position(s, "c", 0)
    if tuple(int(v) for v in got) != (7, 5):
        return f"ожидалось (7, 5), получили {got!r}"


def t_slice_basic():
    s = pd.Series([1, 2, 3, 4], index=["a", "b", "c", "d"])
    got = m.slice_by_labels(s, "b", "c")
    if list(got.index) != ["b", "c"] or list(got) != [2, 3]:
        return f"срез по меткам 'b'..'c' должен дать b и c, получили {list(got.index)}"


def t_align_disjoint():
    a = pd.Series([1, 2], index=[1, 2])
    b = pd.Series([5, 6], index=[3, 4])
    got = m.align_sum(a, b)
    if got.isna().any() or len(got) != 4:
        return f"для непересекающихся индексов нужно 4 строки без NaN, получили {got.to_dict()}"
    return same_series(got.sort_index(), pd.Series([1.0, 2.0, 5.0, 6.0], index=[1, 2, 3, 4]), check_dtype=False, check_names=False)


def t_position_other_order():
    a = pd.Series([1, 2, 3], index=["a", "b", "c"])
    b = pd.Series([10, 20, 30], index=["c", "a", "b"])
    got = m.add_by_position(a, b)
    want = pd.Series([11, 22, 33], index=["a", "b", "c"])
    problem = same_series(got, want, check_dtype=False, check_names=False)
    if problem:
        return "складывать нужно по позициям, а не по меткам (pandas выровняло бы a['a'] с b['a']); " + problem
    try:
        m.add_by_position(a, pd.Series([1, 2]))
    except ValueError:
        return None
    return "при разной длине нужен ValueError"


def t_label_not_position():
    s = pd.Series([100, 200, 300], index=[30, 10, 20])
    got = m.label_and_position(s, 10, 0)
    if tuple(int(v) for v in got) != (200, 100):
        return f"на индексе [30, 10, 20] метка 10 это 200, а позиция 0 это 100; получили {got!r}"


def t_slice_inclusive_int():
    s = pd.Series(range(5), index=[10, 20, 30, 40, 50])
    got = m.slice_by_labels(s, 20, 40)
    if list(got.index) != [20, 30, 40]:
        return f"метки 20..40 включительно, получили {list(got.index)}"


case("align-basic", t_align_basic)
case("label-position-basic", t_label_position_basic)
case("slice-basic", t_slice_basic)
case("align-disjoint", t_align_disjoint)
case("position-other-order", t_position_other_order)
case("label-not-position", t_label_not_position)
case("slice-inclusive-int", t_slice_inclusive_int)
