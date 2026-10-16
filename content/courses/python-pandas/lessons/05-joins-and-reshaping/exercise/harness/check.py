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
import pandas.errors
import join_task as m

HERE = os.path.dirname(os.path.abspath(__file__))


def tables():
    return pd.read_csv(os.path.join(HERE, "orders.csv")), pd.read_csv(os.path.join(HERE, "customers.csv"))


def mini():
    o = pd.DataFrame({"order_id": [1, 2, 3, 4, 5], "customer_id": ["a", "b", "z", "a", "c"], "amount": [10.0, 20.0, 30.0, 5.0, 7.0]})
    c = pd.DataFrame({"customer_id": ["a", "b", "c"], "name": ["A", "B", "C"], "country": ["DE", "FR", "DE"], "segment": ["retail", "retail", "wholesale"]})
    return o, c


def t_join_basic():
    o, c = tables()
    got = m.join_orders(o, c)
    if list(got.columns) != ["order_id", "customer_id", "amount", "name", "country", "segment"]:
        return f"столбцы: {list(got.columns)}"
    if len(got) != len(o):
        return f"строк {len(got)}, заказов {len(o)}"
    return same_series(got["order_id"], o["order_id"])


def t_orphans_basic():
    o, c = tables()
    got = m.find_orphans(o, c)
    want = sorted(o.loc[~o["customer_id"].isin(c["customer_id"]), "order_id"].tolist())
    if list(map(int, got)) != want or len(want) == 0:
        return f"ожидался список {want}, получили {got}"


def t_pivot_basic():
    o, c = tables()
    got = m.revenue_pivot(m.join_orders(o, c))
    j = o.merge(c, on="customer_id")
    want = j.groupby(["country", "segment"])["amount"].sum().unstack(fill_value=0)
    return same_frame(got, want, check_dtype=False, check_names=False, check_exact=False, rtol=1e-9)


def t_join_keeps_unmatched():
    o, c = mini()
    got = m.join_orders(o, c)
    if len(got) != 5 or int(got["name"].isna().sum()) != 1:
        return f"inner join потеряет заказ клиента 'z'; нужен how='left': строк {len(got)}, без клиента {int(got['name'].isna().sum())} (ожидалось 5 и 1)"
    if list(got["order_id"]) != [1, 2, 3, 4, 5]:
        return "порядок заказов должен сохраняться"


def t_join_validate():
    o, c = mini()
    dup = pd.concat([c, pd.DataFrame({"customer_id": ["a"], "name": ["A2"], "country": ["PL"], "segment": ["retail"]})], ignore_index=True)
    try:
        m.join_orders(o, dup)
    except pandas.errors.MergeError:
        return None
    return "при дубле customer_id в customers join размножил бы заказы; нужен validate='many_to_one', который бросает MergeError"


def t_orphans_unique():
    o, c = mini()
    dup = pd.concat([c, c.iloc[:1]], ignore_index=True)
    got = m.find_orphans(o, dup)
    if list(map(int, got)) != [3]:
        return f"сирота один - заказ 3; получили {got} (дубли в customers не должны менять ответ)"


def t_pivot_aggregates_and_fills():
    o, c = mini()
    got = m.revenue_pivot(m.join_orders(o, c))
    want = pd.DataFrame({"retail": [15.0, 20.0], "wholesale": [7.0, 0.0]}, index=pd.Index(["DE", "FR"], name="country"))
    want.columns.name = "segment"
    return same_frame(got, want, check_dtype=False, rtol=1e-9)


def t_unpivot_roundtrip():
    o, c = mini()
    wide = m.revenue_pivot(m.join_orders(o, c))
    got = m.unpivot(wide)
    want = pd.DataFrame({"country": ["DE", "DE", "FR", "FR"], "segment": ["retail", "wholesale", "retail", "wholesale"], "revenue": [15.0, 7.0, 20.0, 0.0]})
    return same_frame(got, want, check_dtype=False, rtol=1e-9)


case("join-basic", t_join_basic)
case("orphans-basic", t_orphans_basic)
case("pivot-basic", t_pivot_basic)
case("join-keeps-unmatched", t_join_keeps_unmatched)
case("join-validate", t_join_validate)
case("orphans-unique", t_orphans_unique)
case("pivot-aggregates-fills", t_pivot_aggregates_and_fills)
case("unpivot-roundtrip", t_unpivot_roundtrip)
