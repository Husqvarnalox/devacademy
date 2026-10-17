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


import json

import notes_api as m
from django.test import Client


def fresh():
    m.Note.objects.all().delete()
    return Client()


def post(c, url, payload, ctype="application/json"):
    body = payload if isinstance(payload, str) else json.dumps(payload)
    return c.post(url, data=body, content_type=ctype)


def t_create_and_get():
    c = fresh()
    r = post(c, "/notes/", {"title": "  buy milk ", "done": False})
    bad = expect((r.status_code, r.json()["title"], r.json()["done"]), (201, "buy milk", False), "создание")
    if bad:
        return bad
    nid = r.json()["id"]
    bad = expect(r["Location"], f"/notes/{nid}/", "Location")
    if bad:
        return bad
    bad = expect(c.get(f"/notes/{nid}/").json(), {"id": nid, "title": "buy milk", "done": False}, "чтение")
    if bad:
        return bad
    post(c, "/notes/", {"title": "second", "done": True})
    return expect([n["title"] for n in c.get("/notes/").json()["results"]], ["buy milk", "second"], "список по id")


def t_validation():
    c = fresh()
    r = post(c, "/notes/", {"title": "   "})
    bad = expect((r.status_code, r.json()), (400, {"errors": {"title": "required"}}), "пустой title")
    if bad:
        return bad
    r = post(c, "/notes/", {"title": "x" * 101, "done": "yes"})
    bad = expect((r.status_code, r.json()), (400, {"errors": {"title": "too_long", "done": "invalid"}}), "несколько ошибок")
    if bad:
        return bad
    bad = expect(m.Note.objects.count(), 0, "при ошибках записи не создаются")
    if bad:
        return bad
    bad = expect(post(c, "/notes/", {"title": "x" * 100}).status_code, 201, "100 символов допустимо")
    if bad:
        return bad
    return expect(post(c, "/notes/", {"title": 5}).json(), {"errors": {"title": "required"}}, "title не строка")


def t_update_delete():
    c = fresh()
    nid = post(c, "/notes/", {"title": "a"}).json()["id"]
    r = post(c, f"/notes/{nid}/", {"title": "ignored"})
    bad = expect(r.status_code, 405, "POST на детальный адрес")
    if bad:
        return bad
    r = c.put(f"/notes/{nid}/", data=json.dumps({"title": "b", "done": True}), content_type="application/json")
    bad = expect((r.status_code, r.json()), (200, {"id": nid, "title": "b", "done": True}), "PUT")
    if bad:
        return bad
    bad = expect(c.put(f"/notes/{nid}/", data=json.dumps({"done": True}), content_type="application/json").status_code, 400, "PUT без title")
    if bad:
        return bad
    bad = expect(c.delete(f"/notes/{nid}/").status_code, 204, "DELETE")
    if bad:
        return bad
    return expect(c.get(f"/notes/{nid}/").json(), {"error": "not found"}, "после удаления")


def t_bad_bodies():
    c = fresh()
    bad = expect(post(c, "/notes/", "{not json").json(), {"error": "invalid json"}, "битый JSON")
    if bad:
        return bad
    bad = expect(post(c, "/notes/", "[1, 2]").status_code, 400, "JSON не объект")
    if bad:
        return bad
    r = post(c, "/notes/", {"title": "a"}, ctype="text/plain")
    bad = expect((r.status_code, r.json()), (415, {"error": "content type must be application/json"}), "не тот Content-Type")
    if bad:
        return bad
    return expect(m.Note.objects.count(), 0, "ничего не создано")


def t_not_found_and_methods():
    c = fresh()
    r = c.get("/notes/999/")
    bad = expect((r.status_code, r.json()), (404, {"error": "not found"}), "нет записи")
    if bad:
        return bad
    bad = expect(c.delete("/notes/999/").status_code, 404, "DELETE несуществующей")
    if bad:
        return bad
    r = c.delete("/notes/")
    bad = expect((r.status_code, sorted(r["Allow"].replace(" ", "").split(","))), (405, ["GET", "POST"]), "405 и Allow у списка")
    if bad:
        return bad
    return expect(c.get("/notes/abc/").status_code, 404, "нечисловой pk не подходит под маршрут")


def t_unicode_and_isolation():
    c = fresh()
    r = post(c, "/notes/", {"title": "Купить молоко", "done": True})
    bad = expect((r.status_code, r.json()["title"]), (201, "Купить молоко"), "юникод")
    if bad:
        return bad
    bad = expect(c.get("/notes/").json()["results"][0]["done"], True, "done сохранён")
    if bad:
        return bad
    c.delete(f"/notes/{r.json()['id']}/")
    return expect(c.get("/notes/").json(), {"results": []}, "пустой список")


case("create-and-get", t_create_and_get)
case("validation-errors", t_validation)
case("update-delete", t_update_delete)
case("bad-bodies", t_bad_bodies)
case("not-found-and-methods", t_not_found_and_methods)
case("unicode-and-empty", t_unicode_and_isolation)
