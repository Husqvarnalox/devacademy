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


import asyncio

import httpx
from fastapi.testclient import TestClient

import shop_app as m


def t_settings():
    d = m.load_settings({})
    bad = expect((d.app_name, d.max_items, d.debug), ("shop", 100, False), "значения по умолчанию")
    if bad:
        return bad
    s = m.load_settings({"SHOP_APP_NAME": "demo", "SHOP_MAX_ITEMS": "5", "SHOP_DEBUG": "TRUE"})
    bad = expect((s.app_name, s.max_items, s.debug), ("demo", 5, True), "значения из окружения")
    if bad:
        return bad
    for v, want in (("1", True), ("yes", True), ("on", True), ("0", False), ("no", False), ("", False), ("banana", False)):
        bad = expect(m.load_settings({"SHOP_DEBUG": v}).debug, want, f"SHOP_DEBUG={v!r}")
        if bad:
            return bad
    try:
        m.load_settings({"SHOP_MAX_ITEMS": "many"})
    except ValueError as exc:
        if "SHOP_MAX_ITEMS" not in str(exc):
            return f"сообщение ValueError должно называть переменную SHOP_MAX_ITEMS, получено: {exc}"
    else:
        return "нечисловой SHOP_MAX_ITEMS должен давать ValueError"
    try:
        s.max_items = 1
    except Exception:
        return None
    return "Settings должен быть неизменяемым (frozen dataclass)"


def t_crud():
    c = TestClient(m.create_app())
    r = c.post("/items", json={"name": "pen"})
    bad = expect((r.status_code, r.json()), (201, {"id": 1, "name": "pen"}), "POST /items")
    if bad:
        return bad
    c.post("/items", json={"name": "cup"})
    bad = expect(c.get("/items").json(), [{"id": 1, "name": "pen"}, {"id": 2, "name": "cup"}], "GET /items")
    if bad:
        return bad
    bad = expect(c.get("/items/2").json(), {"id": 2, "name": "cup"}, "GET /items/2")
    if bad:
        return bad
    r = c.get("/items/9")
    bad = expect((r.status_code, r.json()), (404, {"detail": "item not found"}), "GET /items/9")
    if bad:
        return bad
    return expect(c.post("/items", json={"name": ""}).status_code, 422, "пустое имя")


def t_info():
    c = TestClient(m.create_app(m.Settings(app_name="demo", debug=True)))
    c.post("/items", json={"name": "pen"})
    return expect(c.get("/info").json(), {"app": "demo", "debug": True, "items": 1}, "GET /info")


def t_isolation():
    a, b = m.create_app(), m.create_app()
    if a is b:
        return "create_app должен возвращать новое приложение"
    ca, cb = TestClient(a), TestClient(b)
    ca.post("/items", json={"name": "only-in-a"})
    bad = expect(cb.get("/items").json(), [], "приложение b не должно видеть данные a")
    if bad:
        return bad
    bad = expect(cb.post("/items", json={"name": "x"}).json()["id"], 1, "нумерация в b начинается с 1")
    if bad:
        return bad
    return expect(ca.get("/info").json()["items"], 1, "счётчик в a")


def t_limit():
    c = TestClient(m.create_app(m.Settings(max_items=2)))
    codes = [c.post("/items", json={"name": f"n{i}"}).status_code for i in range(3)]
    bad = expect(codes, [201, 201, 409], "коды при max_items=2")
    if bad:
        return bad
    r = c.post("/items", json={"name": "late"})
    bad = expect(r.json(), {"detail": "limit reached"}, "тело 409")
    if bad:
        return bad
    return expect(len(c.get("/items").json()), 2, "превышение лимита ничего не сохраняет")


def t_lifespan():
    app = m.create_app()
    if app.state.started or app.state.closed:
        return "до запуска started и closed должны быть False"
    with TestClient(app) as c:
        if app.state.started is not True:
            return "после старта app.state.started должен быть True"
        if app.state.closed:
            return "closed не должен быть True во время работы"
        c.get("/info")
    return expect(app.state.closed, True, "после выхода из with app.state.closed")


def t_async():
    async def scenario():
        app = m.create_app(m.Settings(max_items=1))
        transport = httpx.ASGITransport(app=app)
        async with httpx.AsyncClient(transport=transport, base_url="http://test") as ac:
            r1 = await ac.post("/items", json={"name": "pen"})
            r2 = await ac.post("/items", json={"name": "cup"})
            r3 = await ac.get("/info")
            return r1.status_code, r2.status_code, r3.json()

    got = asyncio.run(scenario())
    return expect(got, (201, 409, {"app": "shop", "debug": False, "items": 1}), "сценарий через httpx.AsyncClient")


def t_openapi():
    spec = m.create_app(m.Settings(app_name="demo")).openapi()
    bad = expect(spec["info"]["title"], "demo", "title приложения")
    if bad:
        return bad
    bad = expect(sorted(spec["paths"]), ["/info", "/items", "/items/{item_id}"], "пути")
    if bad:
        return bad
    bad = expect(spec["paths"]["/items"]["post"]["tags"], ["items"], "теги /items")
    if bad:
        return bad
    return expect(spec["paths"]["/info"]["get"]["tags"], ["meta"], "теги /info")


case("settings-from-env", t_settings)
case("items-crud", t_crud)
case("info-route", t_info)
case("app-isolation", t_isolation)
case("item-limit", t_limit)
case("lifespan-flags", t_lifespan)
case("async-client", t_async)
case("router-openapi", t_openapi)
