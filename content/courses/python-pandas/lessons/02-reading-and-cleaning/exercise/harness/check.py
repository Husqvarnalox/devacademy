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
import clean_task as m

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, "orders_raw.csv")


def raw():
    return pd.read_csv(PATH, dtype={"sku": "str"}, parse_dates=["order_date"], na_values=["N/A", "-"])


def t_load_dtypes():
    df = m.load_orders(PATH)
    if not isinstance(df, pd.DataFrame) or df.shape != (125, 8):
        return f"ожидался DataFrame формы (125, 8), получили {getattr(df, 'shape', type(df))}"
    if not pd.api.types.is_datetime64_any_dtype(df["order_date"]):
        return f"order_date должен быть datetime, сейчас {df['order_date'].dtype}"
    if not pd.api.types.is_float_dtype(df["qty"]):
        return f"qty с пропусками читается как float64 (маркеры N/A и - должны стать NaN), сейчас {df['qty'].dtype}"


def t_missing_report():
    got = m.missing_report(raw())
    want = pd.Series({"qty": 13, "unit_price": 9})
    return same_series(got, want, check_dtype=False, check_names=False)


def t_clean_basic():
    got = m.clean_orders(raw())
    base = raw().drop_duplicates()
    base = base[base["qty"].notna() & base["unit_price"].notna()].reset_index(drop=True)
    base["qty"] = base["qty"].astype("int64")
    if str(got["qty"].dtype) != "int64":
        return f"qty должен быть int64, сейчас {got['qty'].dtype}"
    return same_frame(got, base, check_dtype=True)


def t_load_leading_zeros():
    df = m.load_orders(PATH)
    if df["sku"].iloc[0] != "00393":
        return f"sku должен читаться строкой '00393', получили {df['sku'].iloc[0]!r} (без dtype ведущие нули теряются)"


def t_na_markers():
    df = m.load_orders(PATH)
    if int(df["qty"].isna().sum()) != 13:
        return f"в qty должно быть 13 пропусков (N/A и -), получили {int(df['qty'].isna().sum())}; задайте na_values"


def t_clean_dedupe_index_and_input():
    src = raw()
    before = src.copy()
    got = m.clean_orders(src)
    problem = same_frame(src, before)
    if problem:
        return "функция изменила входной DataFrame: " + problem
    if got.duplicated().any():
        return "после очистки остались полные дубликаты"
    if list(got.index) != list(range(len(got))):
        return "индекс должен быть 0..n-1 (reset_index(drop=True))"
    ids = got["order_id"]
    if ids.is_unique is False:
        return "order_id повторяется: дубликаты удалены неверно"
    small = pd.DataFrame({"order_id": [1, 1, 2], "qty": [5.0, 5.0, None], "unit_price": [1.0, 1.0, 2.0]})
    out = m.clean_orders(small)
    if len(out) != 1 or int(out["order_id"].iloc[0]) != 1:
        return f"из трёх строк должна остаться одна, получили {out.to_dict('list')}"


def t_fill_price_by_product():
    src = pd.DataFrame({"product": ["a", "a", "a", "b", "b"], "unit_price": [1.0, 3.0, None, 10.0, None]})
    got = m.fill_price(src)
    want = pd.Series([1.0, 3.0, 2.0, 10.0, 10.0])
    problem = same_series(got["unit_price"], want, check_names=False, rtol=1e-9)
    if problem:
        return "пропуск надо заполнить медианой цены того же product, а не общей медианой: " + problem
    if src["unit_price"].isna().sum() != 2:
        return "входной DataFrame изменять нельзя"
    full = m.fill_price(raw())
    if full["unit_price"].isna().any():
        return "после заполнения в unit_price не должно остаться NaN"


case("load-dtypes", t_load_dtypes)
case("missing-report", t_missing_report)
case("clean-basic", t_clean_basic)
case("load-leading-zeros", t_load_leading_zeros)
case("na-markers", t_na_markers)
case("clean-dedupe-index-input", t_clean_dedupe_index_and_input)
case("fill-price-by-product", t_fill_price_by_product)
