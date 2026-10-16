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
import group_task as m

HERE = os.path.dirname(os.path.abspath(__file__))


def sales():
    return pd.read_csv(os.path.join(HERE, "sales.csv"))


def tiny():
    return pd.DataFrame({
        "sale_id": [1, 2, 3, 4, 5],
        "region": ["a", "a", None, "b", None],
        "product": ["x", "y", "x", "x", "z"],
        "channel": ["web"] * 5,
        "qty": [1, 2, 3, 4, 5],
        "revenue": [10.0, 30.0, 5.0, 8.0, 15.0],
    })


def t_summary_basic():
    df = sales()
    got = m.region_summary(df)
    if list(got.columns) != ["orders", "units", "revenue", "avg_ticket"]:
        return f"столбцы должны быть orders, units, revenue, avg_ticket; получили {list(got.columns)}"
    if not got["revenue"].is_monotonic_decreasing:
        return "строки должны идти по убыванию revenue"
    r = df[df["region"] == "north"]
    row = got.loc["north"]
    if row["orders"] != len(r) or row["units"] != r["qty"].sum() or abs(row["revenue"] - r["revenue"].sum()) > 1e-6:
        return f"north посчитан неверно: {row.to_dict()}"
    if abs(row["avg_ticket"] - r["revenue"].mean()) > 1e-9:
        return "avg_ticket должен быть средним revenue"


def t_share_basic():
    df = sales()
    got = m.add_share(df)
    if "region_share" not in got.columns or "region_share" in df.columns:
        return "нужен новый столбец region_share в копии"
    north = got[got["region"] == "north"]["region_share"].sum()
    if abs(north - 1.0) > 1e-9:
        return f"доли внутри региона должны давать 1, у north сумма {north}"
    return same_series(got["revenue"], df["revenue"])


def t_top_basic():
    df = sales()
    got = m.top_product(df)
    sums = df.groupby(["region", "product"])["revenue"].sum()
    for reg in ["north", "south", "east", "west"]:
        want = sums.loc[reg].idxmax()
        if got.loc[reg] != want:
            return f"для {reg} ожидался продукт {want}, получили {got.loc[reg]}"
    if got.name != "product":
        return "имя Series должно быть 'product'"


def t_summary_nan_region():
    df = sales()
    got = m.region_summary(df)
    if abs(got["revenue"].sum() - df["revenue"].sum()) > 1e-6 or int(got["orders"].sum()) != len(df):
        return "в сводке потерялись продажи: группировка по умолчанию отбрасывает строки с NaN в ключе (dropna=True); нужен dropna=False"
    small = m.region_summary(tiny())
    want = pd.DataFrame(
        {"orders": [2, 2, 1], "units": [3, 8, 4], "revenue": [40.0, 20.0, 8.0], "avg_ticket": [20.0, 10.0, 8.0]},
        index=pd.Index(["a", np.nan, "b"], name="region"),
    )
    return same_frame(small, want, check_dtype=False, check_index_type=False, rtol=1e-9)


def t_share_nan_and_index():
    src = tiny()
    got = m.add_share(src)
    want = pd.Series([0.25, 0.75, 0.25, 1.0, 0.75], name="region_share")
    if got["region_share"].isna().any():
        return "у строк с пропущенным регионом доля не посчитана: нужен groupby(..., dropna=False)"
    problem = same_series(got["region_share"], want, rtol=1e-9)
    if problem:
        return problem
    if "region_share" in src.columns:
        return "входной DataFrame изменён"
    shuffled = src.iloc[[4, 0, 3, 1, 2]]
    out = m.add_share(shuffled)
    if list(out.index) != [4, 0, 3, 1, 2] or abs(out.loc[0, "region_share"] - 0.25) > 1e-9:
        return "индекс и порядок строк должны сохраняться (transform выравнивает по индексу, apply может нет)"


def t_top_product_nan():
    got = m.top_product(tiny())
    if len(got) != 3:
        return f"ожидалось 3 региона (a, b, NaN), получили {len(got)}"
    d = {("nan" if pd.isna(k) else k): v for k, v in got.items()}
    if d != {"a": "y", "b": "x", "nan": "z"}:
        return f"неверные продукты: {d} (a: y=30, b: x=8, NaN: z=15 против x=5)"


def t_summary_tie_order():
    small = tiny()
    small["revenue"] = [10.0, 10.0, 10.0, 10.0, 10.0]
    got = m.region_summary(small)
    if list(got["revenue"]) != [20.0, 20.0, 10.0]:
        return f"суммы региона a и NaN по 20, b 10; получили {list(got['revenue'])}"


case("summary-basic", t_summary_basic)
case("share-basic", t_share_basic)
case("top-basic", t_top_basic)
case("summary-nan-region", t_summary_nan_region)
case("share-nan-index", t_share_nan_and_index)
case("top-product-nan", t_top_product_nan)
case("summary-sums", t_summary_tie_order)
