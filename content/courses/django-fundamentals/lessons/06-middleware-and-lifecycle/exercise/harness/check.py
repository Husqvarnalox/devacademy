import json
import os
import warnings

warnings.filterwarnings("ignore")

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
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


import tracing_app as m
from django.test import Client


def client():
    m.reset_state()
    return Client(raise_request_exception=False)


def t_generated_ids():
    c = client()
    r1 = c.get("/ping/")
    r2 = c.get("/ping/")
    bad = expect((r1.status_code, r1.content, r1["X-Request-ID"]), (200, b"pong", "req-0001"), "первый запрос")
    if bad:
        return bad
    bad = expect(r2["X-Request-ID"], "req-0002", "второй запрос")
    if bad:
        return bad
    return expect(c.get("/echo/hi/").json(), {"word": "hi", "request_id": "req-0003"}, "request.request_id виден представлению")


def t_incoming_id():
    c = client()
    r = c.get("/ping/", headers={"X-Request-ID": "abc-123"})
    bad = expect(r["X-Request-ID"], "abc-123", "допустимый входной идентификатор сохраняется")
    if bad:
        return bad
    r = c.get("/ping/", headers={"X-Request-ID": "bad id!"})
    bad = expect(r["X-Request-ID"], "req-0001", "недопустимый заменяется, счётчик не потрачен впустую")
    if bad:
        return bad
    return expect(c.get("/echo/x/", headers={"X-Request-ID": "A" * 37}).json()["request_id"], "req-0002", "слишком длинный (37) заменяется")


def t_access_log():
    c = client()
    c.get("/ping/")
    c.post("/ping/")
    c.get("/echo/word/", headers={"X-Request-ID": "trace-9"})
    return expect(m.LOG, ["req-0001 GET /ping/ 200", "req-0002 POST /ping/ 200", "trace-9 GET /echo/word/ 200"], "журнал доступа")


def t_error_json():
    c = client()
    r = c.get("/boom/")
    bad = expect((r.status_code, r.json()), (500, {"error": "internal", "request_id": "req-0001"}), "исключение превращено в JSON")
    if bad:
        return bad
    bad = expect(r["X-Request-ID"], "req-0001", "заголовок есть и у ответа с ошибкой")
    if bad:
        return bad
    return expect(m.LOG, ["req-0001 GET /boom/ 500"], "журнал видит 500 (AccessLog снаружи ErrorJson)")


def t_signal():
    c = client()
    for _ in range(3):
        c.get("/ping/")
    c.get("/boom/")
    return expect(len(m.FINISHED), 4, "request_finished по разу на запрос, включая ошибку")


def t_edges():
    c = client()
    r = c.get("/nope/")
    bad = expect((r.status_code, r["X-Request-ID"]), (404, "req-0001"), "404 тоже получает идентификатор")
    if bad:
        return bad
    r = c.head("/ping/", headers={"X-Request-ID": "h-1"})
    bad = expect(r["X-Request-ID"], "h-1", "HEAD")
    if bad:
        return bad
    return expect(m.LOG, ["req-0001 GET /nope/ 404", "h-1 HEAD /ping/ 200"], "журнал: 404 и HEAD")


case("generated-ids", t_generated_ids)
case("incoming-id", t_incoming_id)
case("access-log", t_access_log)
case("error-json", t_error_json)
case("signal-finished", t_signal)
case("edge-cases", t_edges)
