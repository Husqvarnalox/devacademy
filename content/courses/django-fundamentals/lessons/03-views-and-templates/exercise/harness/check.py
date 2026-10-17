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


from django.test import Client
from django.urls import reverse

import shop_pages as m

client = Client()


def html(url):
    r = client.get(url)
    return r.status_code, r.content.decode()


def t_list():
    status, body = html("/products/")
    bad = expect(status, 200, "GET /products/")
    if bad:
        return bad
    for piece in (
        "<title>Products</title>",
        "<h1>Products</h1>",
        "<li>Pen: 1.50</li>",
        "<li>Notebook: 4.00</li>",
        "<li>Pencil &lt;HB&gt;: 0.50</li>",
    ):
        if piece not in body:
            return f"в ответе нет {piece!r}; получено: {body[:300]!r}"
    if 'class="q"' in body:
        return "без параметра q абзац поиска показывать не нужно"
    return None


def t_search():
    status, body = html("/products/?q=PEN")
    if status != 200 or "<li>Pen: 1.50</li>" not in body or "Notebook" in body:
        return f"?q=PEN должен оставить Pen и Pencil и убрать Notebook; получено: {body[:300]!r}"
    if "<li>Pencil &lt;HB&gt;: 0.50</li>" not in body:
        return "подстрока без учёта регистра: Pencil тоже подходит"
    status, body = html("/products/?q=zzz")
    if "<li>No products</li>" not in body or "<ul></ul>" in body:
        return f"для пустого результата нужен блок {{% empty %}}: <li>No products</li>; получено: {body[:300]!r}"
    status, body = html("/products/?q=%3Cb%3E")
    return expect('<p class="q">Search: &lt;b&gt;</p>' in body, True, "строка поиска в ответе экранируется: Search: &lt;b&gt;")


def t_detail():
    status, body = html("/products/1/")
    bad = expect(status, 200, "GET /products/1/")
    if bad:
        return bad
    for piece in ("<title>Pen</title>", "<h1>Pen</h1>", '<p class="note">Blue ink</p>', "<span>In stock</span>"):
        if piece not in body:
            return f"в ответе нет {piece!r}; получено: {body[:300]!r}"
    status, body = html("/products/2/")
    if "<span>Sold out</span>" not in body:
        return "для in_stock=False нужен <span>Sold out</span>"
    bad = expect(client.get("/products/99/").status_code, 404, "неизвестный товар")
    if bad:
        return bad
    return expect(client.post("/products/1/").status_code, 405, "POST в классовое представление без post()")


def t_autoescape():
    status, body = html("/products/3/")
    if "<script>" in body:
        return "заметка с <script> попала в ответ неэкранированной"
    if '<p class="note">&lt;script&gt;alert(1)&lt;/script&gt;</p>' not in body:
        return f"ожидалась экранированная заметка; получено: {body[:400]!r}"
    status, body = html("/products/2/")
    if '<p class="note">A5<br>80 pages</p>' not in body:
        return f"переводы строк должны стать <br> (фильтр linebreaksbr): {body[:400]!r}"
    m.PRODUCTS.append({"id": 9, "name": "X", "price": 1, "note": "a\n<b>b</b>", "in_stock": True})
    try:
        status, body = html("/products/9/")
    finally:
        m.PRODUCTS.pop()
    return expect('<p class="note">a<br>&lt;b&gt;b&lt;/b&gt;</p>' in body, True, "linebreaksbr экранирует вход до замены переводов строк")


def t_about():
    status, body = html("/about/")
    bad = expect(status, 200, "GET /about/")
    if bad:
        return bad
    for piece in ("<title>About</title>", "<h1>Shop</h1>", "<p>Pen Shop</p>"):
        if piece not in body:
            return f"в ответе нет {piece!r} (heading не переопределён - берётся из base.html); получено: {body[:300]!r}"
    return expect(client.post("/about/").status_code, 405, "TemplateView по умолчанию принимает только GET/HEAD/OPTIONS")


def t_names():
    bad = expect(reverse("product-list"), "/products/", "reverse('product-list')")
    if bad:
        return bad
    bad = expect(reverse("product-detail", kwargs={"pk": 2}), "/products/2/", "reverse('product-detail')")
    if bad:
        return bad
    bad = expect(reverse("about"), "/about/", "reverse('about')")
    if bad:
        return bad
    bad = expect(client.get("/products/abc/").status_code, 404, "pk не int")
    if bad:
        return bad
    return expect(client.head("/products/").status_code, 200, "HEAD на функциональное представление")


case("list-template", t_list)
case("search-and-empty", t_search)
case("detail-cbv", t_detail)
case("autoescape-and-filters", t_autoescape)
case("about-templateview", t_about)
case("route-names", t_names)
