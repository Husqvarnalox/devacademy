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
    finally:
        m.app.dependency_overrides.clear()
        m.STORE._tokens.clear()


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


import session_task as m
from fastapi.testclient import TestClient

client = TestClient(m.app)


def t_basic():
    s = m.TokenStore()
    t1 = s.issue("ann", 10, 100.0)
    t2 = s.issue("bob", 10, 100.0)
    if t1 == t2 or len(t1) < 40:
        return "токены должны быть разными и длинными (secrets.token_urlsafe(32))"
    bad = expect((s.resolve(t1, 100.0), s.resolve(t2, 105.0), s.resolve("nope", 100.0)), ("ann", "bob", None), "resolve")
    if bad:
        return bad
    return expect(len(s), 2, "len")


def t_expiry():
    s = m.TokenStore()
    t = s.issue("ann", 10, 100.0)
    bad = expect(s.resolve(t, 109.999), "ann", "до границы")
    if bad:
        return bad
    bad = expect(s.resolve(t, 110.0), None, "на границе токен уже недействителен")
    if bad:
        return bad
    bad = expect(len(s), 0, "просроченный токен должен быть удалён")
    if bad:
        return bad
    t = s.issue("ann", 10, 100.0)
    s.resolve(t, 500.0)
    return expect(s.resolve(t, 101.0), None, "удалённый токен не воскресает")


def t_revoke():
    s = m.TokenStore()
    a1, a2, b1 = s.issue("ann", 99, 0), s.issue("ann", 99, 0), s.issue("bob", 99, 0)
    s.revoke(a1)
    s.revoke("unknown")
    bad = expect((s.resolve(a1, 1), s.resolve(a2, 1)), (None, "ann"), "revoke")
    if bad:
        return bad
    a3 = s.issue("ann", 99, 0)
    bad = expect(s.revoke_user("ann"), 2, "revoke_user вернул число отозванных")
    if bad:
        return bad
    bad = expect((s.resolve(a2, 1), s.resolve(a3, 1), s.resolve(b1, 1)), (None, None, "bob"), "после revoke_user")
    if bad:
        return bad
    return expect(s.revoke_user("ann"), 0, "повторный revoke_user")


def t_api():
    clock = {"t": 1000.0}
    m.app.dependency_overrides[m.get_now] = lambda: clock["t"]
    r = client.post("/session/ann")
    bad = expect(r.status_code, 200, "POST /session/ann")
    if bad:
        return bad
    token = r.json()["token"]
    h = {"Authorization": f"Bearer {token}"}
    bad = expect(m.TTL, 60, "TTL")
    if bad:
        return bad
    clock["t"] = 1059.0
    r = client.get("/whoami", headers=h)
    bad = expect((r.status_code, r.json()), (200, {"user": "ann"}), "за секунду до истечения")
    if bad:
        return bad
    clock["t"] = 1060.0
    r = client.get("/whoami", headers=h)
    bad = expect(r.status_code, 401, "в момент истечения")
    if bad:
        return bad
    bad = expect(r.headers.get("www-authenticate"), "Bearer", "WWW-Authenticate")
    if bad:
        return bad
    bad = expect(client.get("/whoami").status_code, 401, "без заголовка")
    if bad:
        return bad
    return expect(len(m.STORE), 0, "просроченный токен удалён из STORE")


case("store-basic", t_basic)
case("expiry", t_expiry)
case("revoke", t_revoke)
case("api-clock", t_api)
