import json
import os
import warnings

warnings.filterwarnings("ignore")

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


import copy
from decimal import Decimal

import models_task as m

VALID = {
    "items": [
        {"sku": " ab-1 ", "qty": 2, "unit_price": "10.50"},
        {"sku": "xyz", "qty": 1, "unit_price": "4"},
    ],
    "address": {"street": "  Main st 1 ", "city": "Berlin", "zip": "10115"},
}


def data(**patch):
    d = copy.deepcopy(VALID)
    d.update(patch)
    return d


def errs(payload):
    r = m.parse_or_errors(payload)
    if r["ok"]:
        return None
    return {(e["loc"], e["type"]) for e in r["errors"]}


def t_normalize():
    r = m.parse_or_errors(data())
    if not r["ok"]:
        return f"корректный заказ отклонён: {r['errors']}"
    o = r["order"]
    bad = expect([i.sku for i in o.items], ["AB-1", "XYZ"], "артикулы после нормализации")
    if bad:
        return bad
    bad = expect(o.address.street, "Main st 1", "street после обрезки пробелов")
    if bad:
        return bad
    bad = expect(o.total, Decimal("25.00"), "total")
    if bad:
        return bad
    return expect(o.model_dump()["total"], Decimal("25.00"), "total в model_dump()")


def t_field_errors():
    d = data()
    d["items"][0]["qty"] = 0
    d["items"][1]["qty"] = 101
    d["items"][1]["unit_price"] = "0"
    d["address"]["zip"] = "1011"
    got = errs(d)
    want = {
        ("items.0.qty", "greater_than_equal"),
        ("items.1.qty", "less_than_equal"),
        ("items.1.unit_price", "greater_than"),
        ("address.zip", "string_pattern_mismatch"),
    }
    return expect(got, want, "ошибки (loc, type)")


def t_payload():
    o = m.parse_or_errors(data())["order"]
    got = m.to_payload(o)
    if "coupon" in got:
        return "coupon=None не должен попадать в payload"
    bad = expect(got["total"], "25.00", "payload['total']")
    if bad:
        return bad
    bad = expect(got["items"][0]["unit_price"], "10.50", "Decimal в JSON-режиме - строка")
    if bad:
        return bad
    o2 = m.parse_or_errors(data(coupon="SALE-10"))["order"]
    return expect(m.to_payload(o2)["coupon"], "SALE-10", "coupon в payload")


def t_forbid():
    d = data(unexpected=1)
    bad = expect(errs(d), {("unexpected", "extra_forbidden")}, "лишнее поле заказа")
    if bad:
        return bad
    d = data()
    d["items"][0]["discount"] = 5
    bad = expect(errs(d), {("items.0.discount", "extra_forbidden")}, "лишнее поле позиции")
    if bad:
        return bad
    d = data()
    d["address"]["country"] = "DE"
    return expect(errs(d), {("address.country", "extra_forbidden")}, "лишнее поле адреса")


def t_model_rules():
    got = errs(data(coupon="FREE"))
    bad = expect(got, {("coupon", "value_error")}, "купон без префикса SALE-")
    if bad:
        return bad
    if errs(data(coupon="SALE-5")) is not None:
        return "купон SALE-5 должен проходить"
    d = data()
    d["items"][1]["sku"] = "ab-1"
    got = errs(d)
    return expect(got, {("", "value_error")}, "повтор sku (после нормализации) - ошибка модели")


def t_default_isolation():
    a = m.parse_or_errors(data())["order"]
    b = m.parse_or_errors(data())["order"]
    a.tags.append("gift")
    bad = expect(b.tags, [], "tags у другого заказа")
    if bad:
        return bad
    return expect(m.parse_or_errors(data(tags=["x"]))["order"].tags, ["x"], "tags из входных данных")


def t_list_bounds():
    got = errs(data(items=[]))
    bad = expect(got, {("items", "too_short")}, "пустой items")
    if bad:
        return bad
    many = [{"sku": f"sku{i:03d}", "qty": 1, "unit_price": "1"} for i in range(51)]
    got = errs(data(items=many))
    bad = expect(got, {("items", "too_long")}, "51 позиция")
    if bad:
        return bad
    fifty = many[:50]
    if errs(data(items=fifty)) is not None:
        return "ровно 50 позиций допустимо"


def t_price():
    d = data()
    d["items"][0]["unit_price"] = "1.234"
    bad = expect(errs(d), {("items.0.unit_price", "decimal_max_places")}, "три знака в цене")
    if bad:
        return bad
    d["items"][0]["unit_price"] = "0.01"
    if errs(d) is not None:
        return "цена 0.01 допустима"
    d["items"][0]["unit_price"] = "-3"
    bad = expect(errs(d), {("items.0.unit_price", "greater_than")}, "отрицательная цена")
    if bad:
        return bad
    d["items"][0]["unit_price"] = "1.50"
    d["items"][0]["qty"] = 3
    d["items"][1]["unit_price"] = "0.10"
    o = m.parse_or_errors(d)["order"]
    return expect(o.total, Decimal("4.60"), "total с копейками")


case("normalize-and-total", t_normalize)
case("field-errors", t_field_errors)
case("payload", t_payload)
case("forbid-extra", t_forbid)
case("model-rules", t_model_rules)
case("default-isolation", t_default_isolation)
case("list-bounds", t_list_bounds)
case("price-precision", t_price)
