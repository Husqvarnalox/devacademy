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


import paginated_notes as m
from django.db import connection
from django.test import Client
from django.test.utils import CaptureQueriesContext


def get(url, n=12):
    m.seed(n)
    return Client().get(url)


def t_pagination():
    r = get("/notes/")
    d = r.json()
    bad = expect((r.status_code, d["count"], d["page"], d["pages"], [x["title"] for x in d["results"]]), (200, 12, 1, 3, [f"note {i}" for i in range(1, 6)]), "первая страница")
    if bad:
        return bad
    d = Client().get("/notes/?page=3").json()
    bad = expect(([x["title"] for x in d["results"]], d["page"]), (["note 11", "note 12"], 3), "последняя страница")
    if bad:
        return bad
    d = Client().get("/notes/?page=2&page_size=10").json()
    return expect((d["pages"], [x["id"] > 0 for x in d["results"]], len(d["results"])), (2, [True, True], 2), "page_size=10")


def t_filters():
    d = get("/notes/?done=true").json()
    bad = expect((d["count"], [x["title"] for x in d["results"]]), (4, ["note 3", "note 6", "note 9", "note 12"]), "done=true")
    if bad:
        return bad
    d = Client().get("/notes/?done=false&page_size=20").json()
    bad = expect((d["count"], d["pages"]), (8, 1), "done=false")
    if bad:
        return bad
    d = Client().get("/notes/?q=NOTE 1").json()
    bad = expect((d["count"], [x["title"] for x in d["results"]]), (4, ["note 1", "note 10", "note 11", "note 12"]), "q без учёта регистра")
    if bad:
        return bad
    d = Client().get("/notes/?q=note&done=true&page=2&page_size=3").json()
    return expect((d["count"], d["pages"], [x["title"] for x in d["results"]]), (4, 2, ["note 12"]), "фильтры вместе со страницами")


def t_errors():
    for url, status, msg in [
        ("/notes/?done=maybe", 400, "invalid done"),
        ("/notes/?page_size=0", 400, "invalid page_size"),
        ("/notes/?page_size=21", 400, "invalid page_size"),
        ("/notes/?page_size=abc", 400, "invalid page_size"),
        ("/notes/?page=0", 404, "invalid page"),
        ("/notes/?page=-1", 404, "invalid page"),
        ("/notes/?page=x", 404, "invalid page"),
        ("/notes/?page=4", 404, "invalid page"),
    ]:
        r = get(url)
        bad = expect((r.status_code, r.json()), (status, {"error": msg}), url)
        if bad:
            return bad
    return None


def t_empty_and_limits():
    d = get("/notes/?q=zzz").json()
    bad = expect((d["count"], d["page"], d["pages"], d["results"]), (0, 1, 1, []), "пустая выборка: одна пустая страница")
    if bad:
        return bad
    d = get("/notes/?page_size=20", n=25).json()
    bad = expect((len(d["results"]), d["pages"]), (20, 2), "page_size=20 - максимум")
    if bad:
        return bad
    d = Client().get("/notes/?page=2&page_size=20").json()
    bad = expect(len(d["results"]), 5, "остаток на второй странице")
    if bad:
        return bad
    return expect(Client().post("/notes/").status_code, 405, "POST не разрешён")


def t_query_count():
    m.seed(50)
    c = Client()
    with CaptureQueriesContext(connection) as ctx:
        r = c.get("/notes/?q=note&done=false&page=2&page_size=10")
    d = r.json()
    bad = expect((r.status_code, d["count"], len(d["results"])), (200, 34, 10), "ответ")
    if bad:
        return bad
    return expect(len(ctx) <= 2, True, f"запросов к базе {len(ctx)}, допустимо не больше 2 (count + срез)")


def t_dont_load_all():
    m.seed(200)
    c = Client()
    with CaptureQueriesContext(connection) as ctx:
        d = c.get("/notes/?page=40").json()
    bad = expect((d["count"], d["pages"], len(d["results"])), (200, 40, 5), "последняя страница большой выборки")
    if bad:
        return bad
    sql = " ".join(q["sql"].upper() for q in ctx.captured_queries if "COUNT" not in q["sql"].upper())
    return expect("LIMIT" in sql, True, "страница должна выбираться срезом (LIMIT), а не загружаться целиком")


case("pagination", t_pagination)
case("filters", t_filters)
case("errors", t_errors)
case("empty-and-limits", t_empty_and_limits)
case("query-count", t_query_count)
case("slice-not-all", t_dont_load_all)
