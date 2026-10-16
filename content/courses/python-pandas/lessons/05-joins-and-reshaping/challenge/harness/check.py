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
import report_task as m

HERE = os.path.dirname(os.path.abspath(__file__))


def tables():
    return pd.read_csv(os.path.join(HERE, "orders.csv")), pd.read_csv(os.path.join(HERE, "customers.csv"))


def mini():
    o = pd.DataFrame({"order_id": [1, 2, 3, 4], "customer_id": ["a", "b", "z", "a"], "amount": [10.0, 20.0, 30.0, 5.0]})
    c = pd.DataFrame({"customer_id": ["a", "b", "c", "d"], "country": ["DE", "FR", "DE", "PL"]})
    return o, c


def t_report_basic():
    o, c = tables()
    got = m.customer_report(o, c)
    if list(got.columns) != ["customer_id", "country", "orders", "revenue"]:
        return f"столбцы: {list(got.columns)}"
    if len(got) != len(c):
        return f"строк должно быть {len(c)} (по числу клиентов), получили {len(got)}"
    if abs(got["revenue"].sum() - o[o["customer_id"].isin(c["customer_id"])]["amount"].sum()) > 1e-6:
        return "сумма revenue должна равняться сумме заказов известных клиентов"


def t_summary_basic():
    o, c = tables()
    got = m.merge_summary(o, c)
    known = o["customer_id"].isin(c["customer_id"])
    want = {"both": int(known.sum()), "orders_only": int((~known).sum()), "customers_only": int((~c["customer_id"].isin(o["customer_id"])).sum())}
    if got != want:
        return f"ожидалось {want}, получили {got}"


def t_report_zero_orders():
    o, c = mini()
    got = m.customer_report(o, c)
    want = pd.DataFrame({
        "customer_id": ["b", "a", "c", "d"],
        "country": ["FR", "DE", "DE", "PL"],
        "orders": [1, 2, 0, 0],
        "revenue": [20.0, 15.0, 0.0, 0.0],
    })
    problem = same_frame(got, want, check_dtype=False, rtol=1e-9)
    if problem:
        return problem + " (клиенты без заказов должны остаться с 0, а не NaN; сирота z в отчёт не входит)"
    if not pd.api.types.is_integer_dtype(got["orders"]):
        return f"orders должен быть целым, сейчас {got['orders'].dtype}"


def t_summary_small():
    o, c = mini()
    got = m.merge_summary(o, c)
    if got != {"both": 3, "orders_only": 1, "customers_only": 2}:
        return f"ожидалось both=3, orders_only=1, customers_only=2; получили {got}"
    if not all(type(v) is int for v in got.values()):
        return "значения должны быть обычными int"


def t_report_duplicate_customers():
    o, c = mini()
    dup = pd.concat([c, c.iloc[:1]], ignore_index=True)
    try:
        got = m.customer_report(o, dup)
    except Exception:
        return None  # бросить ошибку на неуникальных клиентах допустимо
    a = got[got["customer_id"] == "a"]
    if len(a) != 1 or int(a["orders"].iloc[0]) != 2:
        return "дубль клиента не должен размножать строки и удваивать заказы (проверяйте связь через validate или удаляйте дубли)"


case("report-basic", t_report_basic)
case("summary-basic", t_summary_basic)
case("report-zero-orders", t_report_zero_orders)
case("summary-small", t_summary_small)
case("report-duplicate-customers", t_report_duplicate_customers)
