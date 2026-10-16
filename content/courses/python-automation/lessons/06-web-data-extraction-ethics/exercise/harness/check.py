import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import extract as m

PAGE = """<!doctype html>
<html><head><title>Каталог</title></head><body>
<a href="/about">О нас</a>
<a href="https://shop.example/items?page=2#top">Дальше</a>
<a href="item/7.html">Товар</a>
<a href="/about">Дубль</a>
<a href="#footer">Вниз</a>
<a href="mailto:info@shop.example">Почта</a>
<a href="javascript:void(0)">JS</a>
<a>без href</a>
</body></html>"""

TABLE = """<table id="prices">
  <caption>Цены</caption>
  <tr><th>Товар</th><th>Цена</th></tr>
  <tr><td>Чай &amp; кофе</td><td>10&nbsp;руб.</td></tr>
  <tr><td>Сахар</td><td>  5
      руб. </td></tr>
</table>
<table><tr><td>Второй</td></tr></table>"""


def t_links_basic():
    got = m.extract_links(PAGE, "https://shop.example/catalog/")
    want = [
        "https://shop.example/about",
        "https://shop.example/items?page=2",
        "https://shop.example/catalog/item/7.html",
    ]
    return expect(got, want, "extract_links(PAGE, base)")


def t_tables_basic():
    got = m.extract_tables(TABLE)
    want = [[["Товар", "Цена"], ["Чай & кофе", "10 руб."], ["Сахар", "5 руб."]], [["Второй"]]]
    return expect(got, want, "extract_tables(TABLE)")


def t_robots_basic():
    robots = "User-agent: *\nDisallow: /private/\n"
    urls = ["https://x.example/", "https://x.example/private/a", "https://x.example/public/b"]
    got = m.allowed_urls(robots, "DevAcademyBot/1.0", urls)
    return expect(got, (["https://x.example/", "https://x.example/public/b"], None), "allowed_urls(...)")


def t_links_filter():
    html = (
        '<A HREF=/UPPER>x</A><a href=unquoted.html>y</a><a href="ftp://h/f">f</a><a href="tel:+100">t</a>'
        '<a href="  /spaced  ">s</a><a href="">e</a><a href="//cdn.example/lib">cdn</a>'
        '<div><a href="/unclosed">u<p><a href="/b#x">b</a><a href="/b#y">b2'
    )
    got = m.extract_links(html, "http://h.example/dir/page")
    want = ["http://h.example/UPPER", "http://h.example/dir/unquoted.html", "http://h.example/spaced",
            "http://cdn.example/lib", "http://h.example/unclosed", "http://h.example/b"]
    bad = expect(got, want, "extract_links для кривой разметки")
    if bad:
        return bad
    return expect(m.extract_links("", "http://h/"), [], "extract_links('')")


def t_tables_malformed():
    html = """
    <table>
      <tr><th>Имя<th>Город
      <tr><td>Анна<td>Москва
      <tr><td>Лев<td>Тверь &lt;центр&gt;<br>район
      <td>лишняя
      <tr></tr>
      <script>var x = "<td>не ячейка</td>";</script>
      <tr><td>Вложенная<td><table><tr><td>внутри</td></tr></table></td>
    """
    got = m.extract_tables(html)
    want = [[["Имя", "Город"], ["Анна", "Москва"], ["Лев", "Тверь <центр> район", "лишняя"], ["Вложенная", ""]]]
    bad = expect(got, want, "extract_tables для незакрытых тегов")
    if bad:
        return bad
    bad = expect(m.extract_tables("<p>нет таблиц</p>"), [], "extract_tables без таблиц")
    if bad:
        return bad
    bad = expect(m.extract_tables("<td>вне таблицы</td>"), [], "ячейка вне таблицы")
    if bad:
        return bad
    return expect(m.extract_tables("<table><td>a</td></table><table><td>b"), [[["a"]], [["b"]]], "две таблицы, вторая не закрыта")


def t_records():
    table = [["Товар", "Цена"], ["Чай", "10"], ["Сахар"]]
    bad = expect(m.table_to_records(table), [{"Товар": "Чай", "Цена": "10"}, {"Товар": "Сахар", "Цена": ""}], "table_to_records")
    if bad:
        return bad
    bad = expect(m.table_to_records([["A"]]), [], "только заголовок")
    if bad:
        return bad
    cases = [[], [["A", ""], ["1", "2"]], [["A", "A"], ["1", "2"]], [["A", "B"], ["1", "2", "3"]]]
    for table in cases:
        try:
            m.table_to_records(table)
        except ValueError as exc:
            if table and len(table) == 2 and len(table[1]) == 3 and "2" not in str(exc):
                return f"сообщение должно содержать номер строки (2): {exc}"
        else:
            return f"table_to_records({table!r}) должен поднять ValueError"


def t_robots_edge():
    robots = """# комментарий
User-agent: BadBot
Disallow: /

User-agent: *
Crawl-delay: 3
Disallow: /admin
Disallow: /tmp/
"""
    urls = ["https://x.example/", "https://x.example/admin/panel", "https://x.example/tmp/a", "https://x.example/page?q=1"]
    bad = expect(m.allowed_urls(robots, "BadBot", urls), ([], None), "BadBot запрещено везде")
    if bad:
        return bad
    bad = expect(m.allowed_urls(robots, "DevAcademyBot/1.0", urls), (["https://x.example/", "https://x.example/page?q=1"], 3), "общая группа и Crawl-delay")
    if bad:
        return bad
    bad = expect(m.allowed_urls("", "Bot", urls), (urls, None), "пустой robots.txt разрешает всё")
    if bad:
        return bad
    bad = expect(m.allowed_urls("User-agent: *\nDisallow: /\n", "Bot", urls), ([], None), "Disallow: / запрещает всё")
    if bad:
        return bad
    return (
        expect(m.politeness_interval(None), 1.0, "politeness_interval(None)")
        or expect(m.politeness_interval(5), 5.0, "politeness_interval(5)")
        or expect(m.politeness_interval(0, minimum=2), 2.0, "politeness_interval(0, 2)")
    )


case("links-basic", t_links_basic)
case("tables-basic", t_tables_basic)
case("robots-basic", t_robots_basic)
case("links-filter", t_links_filter)
case("tables-malformed", t_tables_malformed)
case("records", t_records)
case("robots-edge", t_robots_edge)
