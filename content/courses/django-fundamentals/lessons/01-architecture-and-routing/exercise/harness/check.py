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


import uuid

from django.test import Client
from django.urls import reverse

import routing_app as m

client = Client()


def get(url):
    r = client.get(url)
    return r.status_code, r.content.decode()


def t_home_hello():
    bad = expect(get("/"), (200, "home"), "GET /")
    if bad:
        return bad
    if not client.get("/")["Content-Type"].startswith("text/plain"):
        return "Content-Type должен начинаться с text/plain"
    bad = expect(get("/hello/Ann/"), (200, "Hello, Ann!"), "GET /hello/Ann/")
    if bad:
        return bad
    bad = expect(client.get("/hello/a/b/").status_code, 404, "<str:...> не должен захватывать слеш: /hello/a/b/")
    if bad:
        return bad
    return expect(get("/hello/%D0%90%D0%BD%D0%BD%D0%B0/"), (200, "Hello, Анна!"), "кириллица в сегменте")


def t_archive():
    bad = expect(get("/archive/2024/03/"), (200, "archive 2024-03"), "GET /archive/2024/03/")
    if bad:
        return bad
    bad = expect(get("/archive/1999/12/"), (200, "archive 1999-12"), "GET /archive/1999/12/")
    if bad:
        return bad
    return expect(get("/archive/2024/10/"), (200, "archive 2024-10"), "GET /archive/2024/10/")


def t_reverse():
    bad = expect(reverse("home"), "/", "reverse('home')")
    if bad:
        return bad
    bad = expect(reverse("hello", args=["Ann"]), "/hello/Ann/", "reverse('hello')")
    if bad:
        return bad
    bad = expect(reverse("post-archive", kwargs={"year": 2024, "month": 3}), "/archive/2024/03/", "reverse('post-archive')")
    if bad:
        return bad
    bad = expect(reverse("file", kwargs={"subpath": "a/b.txt"}), "/files/a/b.txt", "reverse('file') с path-конвертером")
    if bad:
        return bad
    u = uuid.UUID("12345678-1234-5678-1234-567812345678")
    return expect(reverse("user-detail", kwargs={"uid": u}), "/users/12345678-1234-5678-1234-567812345678/", "reverse('user-detail')")


def t_path_uuid():
    bad = expect(get("/files/docs/2024/report.txt"), (200, "file: docs/2024/report.txt"), "path-конвертер")
    if bad:
        return bad
    uid = "1234abcd-12ab-56cd-12ab-5678123456ef"
    bad = expect(get(f"/users/{uid}/"), (200, f"user {uid}"), "uuid-конвертер")
    if bad:
        return bad
    bad = expect(client.get(f"/users/{uid.upper()}/").status_code, 404, "uuid в верхнем регистре не совпадает с конвертером")
    if bad:
        return bad
    return expect(client.get("/users/not-a-uuid/").status_code, 404, "некорректный uuid")


def t_unmatched():
    for url in ("/archive/2024/13/", "/archive/2024/00/", "/archive/2024/1/", "/archive/abcd/01/", "/archive/2024/03", "/nope/", "/hello//"):
        bad = expect(client.get(url).status_code, 404, f"GET {url}")
        if bad:
            return bad
    bad = expect(client.post("/").status_code, 405, "POST / (require_GET)")
    if bad:
        return bad
    return expect(client.post("/").headers.get("Allow"), "GET", "заголовок Allow")


def t_converter():
    c = m.MonthConverter()
    import re

    pattern = re.compile(c.regex)
    for good in ("01", "09", "10", "12"):
        if not pattern.fullmatch(good):
            return f"regex должен принимать {good!r}"
    for bad_value in ("00", "13", "1", "001", "ab", ""):
        if pattern.fullmatch(bad_value):
            return f"regex не должен принимать {bad_value!r}"
    bad = expect(c.to_python("07"), 7, "to_python('07')")
    if bad:
        return bad
    bad = expect(c.to_url(7), "07", "to_url(7)")
    if bad:
        return bad
    return expect(type(c.to_python("12")), int, "тип результата to_python")


case("home-and-hello", t_home_hello)
case("archive-converter", t_archive)
case("reverse-names", t_reverse)
case("path-and-uuid", t_path_uuid)
case("unmatched-and-405", t_unmatched)
case("month-converter-unit", t_converter)
