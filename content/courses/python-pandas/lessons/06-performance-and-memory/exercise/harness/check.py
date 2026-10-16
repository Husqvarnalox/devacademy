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
import numpy as np
import perf_task as m

HERE = os.path.dirname(os.path.abspath(__file__))


def sensors():
    return pd.read_csv(os.path.join(HERE, "sensors.csv"))


def tiny():
    return pd.DataFrame({
        "reading_id": [1, 2, 3, 4],
        "ts": ["t1", "t2", "t3", "t4"],
        "sensor_id": ["S1", "S1", "S1", "S2"],
        "site": ["plant-a", "warehouse", "plant-a", "warehouse"],
        "state": ["ok", "warn", "fault", "ok"],
        "temp_c": [20.0, 25.0, 30.0, 0.0],
        "load_pct": [50.0, 40.0, 10.0, 100.0],
    })


def t_cost_basic():
    df = tiny()
    got = m.add_cost(df)
    want = [5.0, 8.0, 3.0, 12.0]
    if "cost" not in got.columns or "cost" in df.columns:
        return "нужен столбец cost в копии"
    return same_series(got["cost"], pd.Series(want, name="cost"), rtol=1e-9)


def t_shrink_dtypes():
    df = sensors()
    got = m.shrink(df)
    for col in ("site", "state", "sensor_id"):
        if not isinstance(got[col].dtype, pd.CategoricalDtype):
            return f"{col} должен стать category, сейчас {got[col].dtype}"
    if got["reading_id"].dtype != np.int16:
        return f"reading_id (0..699) должен стать int16, сейчас {got['reading_id'].dtype}"


def t_memory_basic():
    df = sensors()
    deep = m.memory_bytes(df)
    if not isinstance(deep, int) or deep != int(df.memory_usage(deep=True).sum()):
        return f"memory_bytes должен вернуть int: memory_usage(deep=True).sum(); получили {deep!r}"


def t_report_basic():
    df = sensors()
    got = m.site_report(df)
    ok = df[df["state"] == "ok"].copy()
    ok["temp_f"] = ok["temp_c"] * 9 / 5 + 32
    g = ok.groupby("site").agg(readings=("reading_id", "count"), mean_temp_f=("temp_f", "mean")).round(2).reset_index()
    return same_frame(got, g, check_dtype=False, rtol=1e-9)


def t_cost_vectorized():
    banned = []
    originals = {}

    def ban(cls, name):
        originals[(cls, name)] = getattr(cls, name)

        def blocked(*a, **k):
            banned.append(name)
            raise AssertionError(f"{name} запрещён в этом задании")

        setattr(cls, name, blocked)

    for cls, name in [(pd.DataFrame, "apply"), (pd.Series, "apply"), (pd.DataFrame, "iterrows"), (pd.DataFrame, "itertuples")]:
        ban(cls, name)
    try:
        big = pd.concat([sensors()] * 50, ignore_index=True)
        got = m.add_cost(big)
    finally:
        for (cls, name), fn in originals.items():
            setattr(cls, name, fn)
    if banned:
        return f"использованы запрещённые методы: {sorted(set(banned))}; задача решается векторно (map/np.select/np.where)"
    ref = big["load_pct"] * big["state"].map({"ok": 0.1, "warn": 0.15, "fault": 0.3}) + (big["site"] == "warehouse") * 2.0
    return same_series(got["cost"], ref, check_names=False, rtol=1e-9)


def t_shrink_memory_and_values():
    df = sensors()
    before = df.copy()
    got = m.shrink(df)
    problem = same_frame(df, before)
    if problem:
        return "входной DataFrame изменён: " + problem
    if got.memory_usage(deep=True).sum() > 0.5 * df.memory_usage(deep=True).sum():
        return f"память должна уменьшиться минимум вдвое: {df.memory_usage(deep=True).sum()} -> {got.memory_usage(deep=True).sum()}"
    return same_frame(got.astype({c: "object" for c in got.columns if isinstance(got[c].dtype, pd.CategoricalDtype)}).astype({c: "object" for c in ("site", "state", "sensor_id")}),
                      df.astype({c: "object" for c in ("site", "state", "sensor_id")}), check_dtype=False)


def t_shrink_keeps_unique_strings():
    df = tiny()
    got = m.shrink(df)
    if isinstance(got["ts"].dtype, pd.CategoricalDtype):
        return "ts уникален в каждой строке: category не даёт выигрыша и не нужен (порог nunique/len <= 0.5)"
    if isinstance(got["state"].dtype, pd.CategoricalDtype):
        return "state: 3 значения на 4 строки (0.75 > 0.5) - выше порога, должен остаться строкой"
    if not isinstance(got["sensor_id"].dtype, pd.CategoricalDtype):
        return "sensor_id: 2 значения на 4 строки (0.5 <= 0.5) - порог включительный, должен стать category"
    if got["temp_c"].dtype != np.float64:
        return "вещественные столбцы менять нельзя"


def t_report_only_ok():
    df = tiny()
    got = m.site_report(df)
    want = pd.DataFrame({"site": ["plant-a", "warehouse"], "readings": [1, 1], "mean_temp_f": [68.0, 32.0]})
    return same_frame(got, want, check_dtype=False, rtol=1e-9)


case("cost-basic", t_cost_basic)
case("shrink-dtypes", t_shrink_dtypes)
case("memory-basic", t_memory_basic)
case("report-basic", t_report_basic)
case("cost-vectorized", t_cost_vectorized)
case("shrink-memory-values", t_shrink_memory_and_values)
case("shrink-unique-strings", t_shrink_keeps_unique_strings)
case("report-only-ok", t_report_only_ok)
