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
import select_task as m

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, "orders.csv")


def orders():
    return pd.read_csv(PATH)


def small():
    return pd.DataFrame({
        "order_id": [1, 2, 3, 4, 5, 6],
        "customer_id": ["C1"] * 6,
        "region": ["north", "south", "north", "east", "south", "north"],
        "product": ["pen"] * 6,
        "qty": [10, 12, 3, 20, 10, 15],
        "unit_price": [2.0, 4.0, 5.0, 1.0, 10.0, 3.0],
        "status": ["shipped", "shipped", "pending", "cancelled", "shipped", "cancelled"],
    })


def t_select_basic():
    df = orders()
    got = m.select_orders(df, ["north", "east"], 10)
    ok = df["region"].isin(["north", "east"]) & (df["qty"] >= 10) & (df["status"] != "cancelled")
    return same_frame(got, df[ok])


def t_discount_basic():
    df = orders()
    got = m.bulk_discount(df)
    want = df.copy()
    k = (df["status"] == "shipped") & (df["qty"] >= 10)
    want.loc[k, "unit_price"] = df.loc[k, "unit_price"] * 0.9
    return same_frame(got, want, rtol=1e-9)


def t_cancel_basic():
    df = orders()
    got = m.cancel_orders(df, [1, 2, 3])
    if list(got["status"].iloc[:3]) != ["cancelled"] * 3:
        return f"заказы 1-3 должны стать cancelled, получили {list(got['status'].iloc[:3])}"
    return same_series(got["status"].iloc[3:], df["status"].iloc[3:])


def t_expensive_basic():
    df = orders()
    got = m.expensive(df, 500)
    t = df["qty"] * df["unit_price"]
    want = pd.DataFrame({"order_id": df["order_id"], "total": t})[t > 500]
    return same_frame(got, want, rtol=1e-9)


def t_select_small():
    got = m.select_orders(small(), ["north", "south"], 10)
    if list(got["order_id"]) != [1, 2, 5]:
        return f"ожидались order_id [1, 2, 5], получили {list(got['order_id'])} (проверьте скобки и & вместо and)"
    got.loc[got.index[0], "qty"] = 999  # результат должен быть независимой копией
    return None


def t_discount_effect_and_input():
    src = small()
    before = src.copy()
    got = m.bulk_discount(src)
    problem = same_frame(src, before)
    if problem:
        return "исходный DataFrame изменён: " + problem
    if not all(abs(a - b) < 1e-9 for a, b in zip(got["unit_price"], [1.8, 3.6, 5.0, 1.0, 9.0, 3.0])):
        return f"скидка не применилась к нужным строкам: {list(got['unit_price'])} (присваивание через цепочку df[mask]['col'] = ... не меняет df; используйте df.loc[mask, 'col'] = ...)"


def t_cancel_input_intact():
    src = small()
    before = src.copy()
    got = m.cancel_orders(src, [3, 5])
    problem = same_frame(src, before)
    if problem:
        return "исходный DataFrame изменён: " + problem
    if list(got["status"]) != ["shipped", "shipped", "cancelled", "cancelled", "cancelled", "cancelled"]:
        return f"неверные статусы: {list(got['status'])}"


def t_expensive_boundary():
    got = m.expensive(small(), 30)  # totals: 20, 48, 15, 20, 100, 45
    if list(got["order_id"]) != [2, 5, 6]:
        return f"total СТРОГО больше 30: ожидались заказы [2, 5, 6], получили {list(got['order_id'])}"
    if list(got.columns) != ["order_id", "total"]:
        return f"столбцы должны быть ['order_id', 'total'], получили {list(got.columns)}"
    if m.expensive(small(), 48)["order_id"].tolist() != [5]:
        return "граница строгая: total == 48 не проходит"


case("select-basic", t_select_basic)
case("discount-basic", t_discount_basic)
case("cancel-basic", t_cancel_basic)
case("expensive-basic", t_expensive_basic)
case("select-small", t_select_small)
case("discount-effect-input", t_discount_effect_and_input)
case("cancel-input-intact", t_cancel_input_intact)
case("expensive-boundary", t_expensive_boundary)
