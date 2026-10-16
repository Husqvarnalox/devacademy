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
import rank_task as m

HERE = os.path.dirname(os.path.abspath(__file__))


def sales():
    return pd.read_csv(os.path.join(HERE, "sales.csv"))


def tiny():
    return pd.DataFrame({
        "sale_id": [7, 3, 5, 1, 2, 9],
        "region": ["a", "a", "a", None, None, "b"],
        "revenue": [10.0, 10.0, 40.0, 4.0, 8.0, 5.0],
    })


def t_z_basic():
    df = sales()
    got = m.zscore_within_region(df)
    want = df.groupby("region", dropna=False)["revenue"].transform(lambda x: (x - x.mean()) / x.std())
    return same_series(got, want, check_names=False, rtol=1e-9)


def t_top_basic():
    df = sales()
    got = m.top_n_per_region(df, 2)
    if list(got.columns) != ["sale_id", "region", "revenue"]:
        return f"столбцы: {list(got.columns)}"
    if list(got.index) != list(range(len(got))):
        return "индекс должен быть 0..k-1"
    n = got.groupby("region", dropna=False).size()
    if (n != 2).any():
        return f"в каждом регионе должно быть по 2 строки, получили {n.to_dict()}"


def t_z_nan_group():
    got = m.zscore_within_region(tiny())
    if got.isna().tolist() != [False, False, False, False, False, True]:
        return f"NaN ожидается только для группы из одной строки (b); получили {got.isna().tolist()} (NaN-регион тоже группа)"
    if abs(got.iloc[3] + 0.7071067811865475) > 1e-9:
        return f"z-оценка 4.0 в группе [4, 8] равна -0.7071..., получили {got.iloc[3]}"


def t_top_order_ties_nan():
    got = m.top_n_per_region(tiny(), 2)
    want = pd.DataFrame({
        "sale_id": [5, 3, 9, 2, 1],
        "region": ["a", "a", "b", None, None],
        "revenue": [40.0, 10.0, 5.0, 8.0, 4.0],
    })
    return same_frame(got, want, check_dtype=False, rtol=1e-9)


def t_top_n_large():
    got = m.top_n_per_region(tiny(), 100)
    if len(got) != 6:
        return f"при n больше размера групп должны вернуться все 6 строк, получили {len(got)}"


case("z-basic", t_z_basic)
case("top-basic", t_top_basic)
case("z-nan-group", t_z_nan_group)
case("top-order-ties-nan", t_top_order_ties_nan)
case("top-n-large", t_top_n_large)
