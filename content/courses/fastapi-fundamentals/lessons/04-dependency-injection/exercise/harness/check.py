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
    finally:
        m.app.dependency_overrides.clear()
        m.AUDIT.clear()


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from fastapi.testclient import TestClient

import todo_api as m

client = TestClient(m.app)


class FakeRepo(m.TodoRepo):
    """Заглушка: считает обращения и ведёт собственное хранилище."""

    def __init__(self):
        super().__init__()
        self.calls = []

    def add(self, title):
        self.calls.append(("add", title))
        return super().add(title)

    def get(self, todo_id):
        self.calls.append(("get", todo_id))
        return super().get(todo_id)


def use(repo):
    m.app.dependency_overrides[m.get_repo] = lambda: repo
    return repo


def t_override_repo():
    prod_before = len(m.PROD_REPO.list())
    fake = use(FakeRepo())
    r = client.post("/todos", json={"title": "write tests"})
    bad = expect((r.status_code, r.json()), (201, {"id": 1, "title": "write tests", "done": False}), "POST /todos")
    if bad:
        return bad
    bad = expect(fake.calls, [("add", "write tests")], "обращения к подменённому репозиторию")
    if bad:
        return bad
    if len(m.PROD_REPO.list()) != prod_before:
        return "обработчик записал данные в PROD_REPO, минуя зависимость get_repo"
    r = client.get("/todos/1")
    return expect(r.json()["title"], "write tests", "GET /todos/1 из подменённого репозитория")


def t_pagination():
    fake = use(FakeRepo())
    for i in range(7):
        fake.add(f"t{i}")
    r = client.get("/todos")
    bad = expect(len(r.json()), 7, "GET /todos (по умолчанию)")
    if bad:
        return bad
    r = client.get("/todos", params={"limit": 3, "offset": 2})
    bad = expect([t["id"] for t in r.json()], [3, 4, 5], "limit=3&offset=2")
    if bad:
        return bad
    r = client.get("/todos", params={"offset": 6})
    return expect([t["id"] for t in r.json()], [7], "offset=6")


def t_get_404():
    use(FakeRepo())
    r = client.get("/todos/5")
    return expect((r.status_code, r.json()), (404, {"detail": "todo not found"}), "GET /todos/5")


def t_default_repo():
    if m.get_repo() is not m.PROD_REPO:
        return "get_repo() должна возвращать PROD_REPO"
    before = len(m.PROD_REPO.list())
    r = client.post("/todos", json={"title": "persist"})
    if r.status_code != 201:
        return f"POST /todos без подмены вернул {r.status_code}"
    new_id = r.json()["id"]
    bad = expect(len(m.PROD_REPO.list()), before + 1, "размер PROD_REPO после POST")
    if bad:
        return bad
    r = client.get(f"/todos/{new_id}")
    return expect(r.json()["title"], "persist", "задача читается из PROD_REPO")


def t_audit_order():
    use(FakeRepo())
    client.post("/todos", json={"title": "a"})
    bad = expect(m.AUDIT, ["open", "created:1", "close"], "журнал после POST")
    if bad:
        return bad
    m.AUDIT.clear()
    client.delete("/todos/1")
    return expect(m.AUDIT, ["open", "deleted:1", "close"], "журнал после DELETE")


def t_audit_error():
    use(FakeRepo())
    r = client.delete("/todos/42")
    bad = expect((r.status_code, r.json()), (404, {"detail": "todo not found"}), "DELETE несуществующей")
    if bad:
        return bad
    bad = expect(m.AUDIT, ["open", "close"], "журнал после ошибки (close обязателен, deleted не должно быть)")
    if bad:
        return bad
    m.AUDIT.clear()
    r = client.post("/todos", json={"title": ""})
    bad = expect(r.status_code, 422, "пустой title")
    if bad:
        return bad
    if "created:1" in m.AUDIT:
        return "при невалидном теле обработчик не должен выполняться"


def t_bounds():
    use(FakeRepo())
    for q, loc in (("limit=0", "limit"), ("limit=51", "limit"), ("offset=-1", "offset")):
        r = client.get(f"/todos?{q}")
        bad = expect(r.status_code, 422, f"GET /todos?{q}")
        if bad:
            return bad
        if ["query", loc] not in [d["loc"] for d in r.json()["detail"]]:
            return f"ожидался loc ['query', '{loc}'], получено {[d['loc'] for d in r.json()['detail']]}"
    for q in ("limit=1", "limit=50", "offset=0"):
        bad = expect(client.get(f"/todos?{q}").status_code, 200, f"GET /todos?{q}")
        if bad:
            return bad


def t_openapi():
    op = client.get("/openapi.json").json()["paths"]["/todos"]["get"]
    names = sorted(p["name"] for p in op.get("parameters", []))
    return expect(names, ["limit", "offset"], "параметры GET /todos в OpenAPI")


case("override-repo", t_override_repo)
case("pagination", t_pagination)
case("get-404", t_get_404)
case("default-repo", t_default_repo)
case("audit-order", t_audit_order)
case("audit-on-error", t_audit_error)
case("pagination-bounds", t_bounds)
case("openapi-params", t_openapi)
