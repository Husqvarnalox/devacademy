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


from fastapi.testclient import TestClient

import errors_api as m

client = TestClient(m.app, raise_server_exceptions=False)


def shape(response):
    """(status, code) если тело имеет форму {'error': {'code', 'message'}}, иначе описание проблемы."""
    try:
        body = response.json()
    except Exception:
        return f"тело не JSON: {response.text!r}"
    err = body.get("error") if isinstance(body, dict) else None
    if not isinstance(err, dict) or not isinstance(err.get("code"), str) or not isinstance(err.get("message"), str):
        return f"тело не в формате {{'error': {{'code', 'message'}}}}: {body!r}"
    return None


def t_not_found():
    m.reset_state()
    r = client.get("/stock/banana")
    bad = shape(r)
    if bad:
        return bad
    bad = expect((r.status_code, r.json()["error"]["code"]), (404, "not_found"), "GET /stock/banana")
    if bad:
        return bad
    r = client.get("/stock/apple")
    return expect((r.status_code, r.json()), (200, {"item": "apple", "qty": 5}), "GET /stock/apple")


def t_out_of_stock():
    m.reset_state()
    r = client.post("/orders", json={"item": "apple", "qty": 6})
    bad = shape(r) or expect((r.status_code, r.json()["error"]["code"]), (409, "out_of_stock"), "заказ 6 из 5")
    if bad:
        return bad
    bad = expect(m.STOCK["apple"], 5, "склад после отказа")
    if bad:
        return bad
    r = client.post("/orders", json={"item": "pear", "qty": 1})
    return expect((r.status_code, r.json()["error"]["code"]), (409, "out_of_stock"), "заказ товара с нулевым остатком")


def t_validation():
    m.reset_state()
    r = client.post("/orders", json={"item": "apple", "qty": "many"})
    bad = shape(r)
    if bad:
        return bad
    err = r.json()["error"]
    bad = expect((r.status_code, err["code"], err["message"]), (422, "validation_error", "Invalid request"), "422")
    if bad:
        return bad
    details = err.get("details")
    if not isinstance(details, list) or len(details) != 1:
        return f"details должен быть списком из одной ошибки: {details!r}"
    bad = expect(details[0]["field"], "body.qty", "details[0].field")
    if bad:
        return bad
    if not isinstance(details[0].get("message"), str) or not details[0]["message"]:
        return "details[0].message должен быть непустой строкой"


def t_router_errors():
    r = client.get("/no-such-route")
    bad = shape(r) or expect((r.status_code, r.json()["error"]["code"]), (404, "http_404"), "неизвестный маршрут")
    if bad:
        return bad
    r = client.delete("/stock/apple")
    bad = shape(r) or expect((r.status_code, r.json()["error"]["code"]), (405, "http_405"), "DELETE /stock/apple")
    if bad:
        return bad
    if "GET" not in r.headers.get("allow", ""):
        return f"заголовок Allow потерян: {dict(r.headers)}"
    r = client.post("/stock/apple")
    return expect(r.status_code, 405, "POST /stock/apple")


def t_internal():
    r = client.get("/crash")
    bad = shape(r)
    if bad:
        return bad
    bad = expect((r.status_code, r.json()["error"]["code"], r.json()["error"]["message"]), (500, "internal_error", "Internal server error"), "GET /crash")
    if bad:
        return bad
    if "secret" in r.text:
        return f"текст внутреннего исключения попал в ответ: {r.text}"


def t_success():
    m.reset_state()
    r = client.post("/orders", json={"item": "apple", "qty": 2})
    bad = expect((r.status_code, r.json()), (201, {"item": "apple", "qty": 2, "remaining": 3}), "POST /orders")
    if bad:
        return bad
    r = client.get("/stock/apple")
    bad = expect(r.json()["qty"], 3, "остаток после заказа")
    if bad:
        return bad
    r = client.post("/orders", json={"item": "apple", "qty": 3})
    bad = expect((r.status_code, r.json()["remaining"]), (201, 0), "заказ на весь остаток")
    if bad:
        return bad
    r = client.post("/orders", json={"item": "kiwi", "qty": 1})
    return expect((r.status_code, r.json()["error"]["code"]), (404, "not_found"), "заказ неизвестного товара")


def t_multi():
    m.reset_state()
    r = client.post("/orders", json={"qty": 0})
    bad = shape(r)
    if bad:
        return bad
    fields = [d["field"] for d in r.json()["error"]["details"]]
    bad = expect(fields, ["body.item", "body.qty"], "поля ошибок")
    if bad:
        return bad
    r = client.post("/orders", json={"item": "apple", "qty": 11})
    bad = expect([d["field"] for d in r.json()["error"]["details"]], ["body.qty"], "qty=11")
    if bad:
        return bad
    r = client.post("/orders", content="{broken", headers={"content-type": "application/json"})
    return expect((r.status_code, r.json()["error"]["code"]), (422, "validation_error"), "битый JSON")


case("not-found", t_not_found)
case("out-of-stock", t_out_of_stock)
case("validation-shape", t_validation)
case("router-errors", t_router_errors)
case("internal-no-leak", t_internal)
case("order-success", t_success)
case("multi-errors", t_multi)
