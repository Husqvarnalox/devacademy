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

import users_api as m

client = TestClient(m.app)


def mk(name="alice", password="s3cretpass"):
    return client.post("/users", json={"username": name, "password": password})


def locs(response):
    return [tuple(d["loc"]) for d in response.json().get("detail", [])]


def t_create():
    m.reset_state()
    r = mk()
    bad = expect(r.status_code, 201, "POST /users")
    if bad:
        return bad
    return expect(r.json(), {"id": 1, "username": "alice"}, "тело ответа POST /users")


def t_get():
    m.reset_state()
    mk()
    r = client.get("/users/1")
    bad = expect((r.status_code, r.json()), (200, {"id": 1, "username": "alice"}), "GET /users/1")
    if bad:
        return bad
    r = client.get("/users/7")
    return expect((r.status_code, r.json()), (404, {"detail": "user not found"}), "GET /users/7")


def t_list():
    m.reset_state()
    for n in ("alice", "bob", "Alicia"):
        mk(n)
    r = client.get("/users")
    bad = expect([u["username"] for u in r.json()], ["alice", "bob", "Alicia"], "GET /users")
    if bad:
        return bad
    r = client.get("/users", params={"q": "ALI"})
    return expect([u["username"] for u in r.json()], ["alice", "Alicia"], "GET /users?q=ALI")


def t_no_leak():
    m.reset_state()
    pw = "UniquePassw0rd!"
    r1 = mk("carol", pw)
    r2 = client.get("/users/1")
    r3 = client.get("/users")
    for name, r in (("POST", r1), ("GET /users/1", r2), ("GET /users", r3)):
        text = r.text
        if "password" in text or "hash" in text or pw in text or pw[::-1] in text:
            return f"{name}: в ответе есть пароль или хеш: {text}"


def t_body_validation():
    m.reset_state()
    r = client.post("/users", json={"username": "ab", "password": "s3cretpass"})
    bad = expect((r.status_code, locs(r)), (422, [("body", "username")]), "короткое имя")
    if bad:
        return bad
    r = client.post("/users", json={"username": "x" * 21, "password": "s3cretpass"})
    bad = expect(r.status_code, 422, "слишком длинное имя")
    if bad:
        return bad
    r = client.post("/users", json={"username": "bobby", "password": "short"})
    bad = expect((r.status_code, locs(r)), (422, [("body", "password")]), "короткий пароль")
    if bad:
        return bad
    r = client.post("/users", json={"username": "bobby"})
    bad = expect((r.status_code, locs(r)), (422, [("body", "password")]), "нет пароля")
    if bad:
        return bad
    if m.USERS:
        return "невалидные запросы не должны создавать пользователей"
    r = mk("abc", "12345678")
    return expect(r.status_code, 201, "граничные длины (3 и 8) допустимы")


def t_duplicate():
    m.reset_state()
    mk("dave")
    r = mk("dave")
    bad = expect((r.status_code, r.json()), (409, {"detail": "username taken"}), "повторное имя")
    if bad:
        return bad
    r = mk("erin")
    return expect(r.json()["id"], 2, "id после отклонённого дубликата")


def t_constraints():
    m.reset_state()
    mk()
    cases = [
        ("/users/0", ("path", "user_id")),
        ("/users/abc", ("path", "user_id")),
        ("/users?limit=0", ("query", "limit")),
        ("/users?limit=101", ("query", "limit")),
        ("/users?offset=-1", ("query", "offset")),
    ]
    for url, loc in cases:
        r = client.get(url)
        bad = expect((r.status_code, loc in locs(r)), (422, True), f"GET {url}")
        if bad:
            return bad
    for url in ("/users?limit=1", "/users?limit=100", "/users?offset=0"):
        bad = expect(client.get(url).status_code, 200, f"GET {url}")
        if bad:
            return bad


def t_paging():
    m.reset_state()
    for i in range(5):
        mk(f"user{i}")
    r = client.get("/users", params={"limit": 2, "offset": 1})
    bad = expect([u["id"] for u in r.json()], [2, 3], "limit=2&offset=1")
    if bad:
        return bad
    r = client.get("/users", params={"offset": 4})
    bad = expect([u["id"] for u in r.json()], [5], "offset=4")
    if bad:
        return bad
    mk("x" * 3)
    for i in range(12):
        mk(f"more{i}")
    return expect(len(client.get("/users").json()), 10, "limit по умолчанию")


def t_openapi():
    schemas = client.get("/openapi.json").json()["components"]["schemas"]
    if "UserOut" not in schemas:
        return "в схеме OpenAPI нет модели UserOut"
    props = set(schemas["UserOut"]["properties"])
    if props != {"id", "username"}:
        return f"UserOut содержит поля {sorted(props)}, ожидалось ['id', 'username']"
    if "password" not in schemas.get("UserCreate", {}).get("properties", {}):
        return "модель UserCreate должна содержать password"


case("create-user", t_create)
case("get-user", t_get)
case("list-users", t_list)
case("no-leak", t_no_leak)
case("body-validation", t_body_validation)
case("duplicate", t_duplicate)
case("param-constraints", t_constraints)
case("paging", t_paging)
case("openapi-schema", t_openapi)
