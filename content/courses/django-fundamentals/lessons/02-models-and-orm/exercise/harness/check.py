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


from django.db import connection
from django.db.models import QuerySet
from django.test.utils import CaptureQueriesContext

import blog_orm as m


def counted(fn, *args):
    """Результат вызова и число выполненных SQL-запросов."""
    with CaptureQueriesContext(connection) as ctx:
        result = fn(*args)
    return result, len(ctx)


def seed(extra=0):
    m.Review.objects.all().delete()
    m.Book.objects.all().delete()
    m.Author.objects.all().delete()
    ann, bob, _cid = (m.Author.objects.create(name=n) for n in ("Ann", "Bob", "Cid"))
    rows = [("Alpha", ann, 1000, 2001), ("Gamma", ann, 2000, 1999), ("Beta", bob, 1500, 2010), ("Delta", bob, 3000, 2020), ("Epsilon", bob, 500, 2005)]
    for title, author, price, year in rows:
        m.Book.objects.create(title=title, author=author, price=price, year=year)
    for i in range(extra):
        author = m.Author.objects.create(name=f"Zed{i:02d}")
        for j in range(3):
            m.Book.objects.create(title=f"Z{i:02d}-{j}", author=author, price=100 + i + j, year=2000)


def t_filter_exclude():
    seed()
    got, n = counted(m.titles_by_author, "Bob")
    bad = expect((got, n), (["Beta", "Delta", "Epsilon"], 1), "titles_by_author('Bob') и число запросов")
    if bad:
        return bad
    bad = expect(m.titles_by_author("Nobody"), [], "titles_by_author для неизвестного автора")
    if bad:
        return bad
    got, n = counted(m.affordable_titles, 1500, "Ann")
    bad = expect((got, n), (["Beta", "Epsilon"], 1), "affordable_titles(1500, 'Ann') и число запросов")
    if bad:
        return bad
    return expect(m.affordable_titles(2000, "Bob"), ["Alpha", "Gamma"], "affordable_titles(2000, 'Bob')")


def t_annotate_aggregate():
    seed()
    got, n = counted(m.author_stats)
    got = [(name, cnt, None if avg is None else round(avg, 2)) for name, cnt, avg in got]
    bad = expect((got, n), ([("Ann", 2, 1500.0), ("Bob", 3, 1666.67), ("Cid", 0, None)], 1), "author_stats и число запросов")
    if bad:
        return bad
    got, n = counted(m.price_summary)
    bad = expect((got, n), ({"count": 5, "min": 500, "max": 3000, "total": 8000}, 1), "price_summary и число запросов")
    if bad:
        return bad
    m.Book.objects.all().delete()
    return expect(m.price_summary(), {"count": 0, "min": None, "max": None, "total": None}, "price_summary для пустой таблицы")


def t_lazy():
    seed()
    qs, n = counted(m.cheap_books)
    bad = expect(isinstance(qs, QuerySet), True, "cheap_books() должен вернуть QuerySet, а не список")
    if bad:
        return bad
    bad = expect(n, 0, "создание QuerySet не должно выполнять запросы")
    if bad:
        return bad
    titles, n = counted(lambda: [b.title for b in qs])
    bad = expect((titles, n), (["Epsilon", "Alpha", "Beta", "Gamma", "Delta"], 1), "порядок и один запрос при вычислении")
    if bad:
        return bad
    _, n = counted(lambda: len(qs))
    bad = expect(n, 0, "повторное обращение берёт результат из кэша QuerySet")
    if bad:
        return bad
    got, n = counted(lambda: list(qs.filter(year__gt=2004).values_list("title", flat=True)))
    return expect((got, n), (["Epsilon", "Beta", "Delta"], 1), "цепочка filter поверх возвращённого QuerySet")


def t_select_related():
    seed()
    got, n = counted(m.book_lines)
    bad = expect(got, ["Alpha (Ann)", "Beta (Bob)", "Delta (Bob)", "Epsilon (Bob)", "Gamma (Ann)"], "book_lines")
    if bad:
        return bad
    return expect(n, 1, "число запросов в book_lines (select_related)")


def t_prefetch():
    seed()
    got, n = counted(m.catalog)
    bad = expect(got, {"Ann": ["Alpha", "Gamma"], "Bob": ["Beta", "Delta", "Epsilon"], "Cid": []}, "catalog")
    if bad:
        return bad
    bad = expect(list(got), ["Ann", "Bob", "Cid"], "порядок ключей catalog")
    if bad:
        return bad
    return expect(n, 2, "число запросов в catalog (prefetch_related: авторы + книги)")


def t_scale():
    seed(extra=12)
    lines, n1 = counted(m.book_lines)
    cat, n2 = counted(m.catalog)
    bad = expect((len(lines), n1), (5 + 36, 1), "book_lines на большем наборе: число строк и запросов")
    if bad:
        return bad
    bad = expect((len(cat), n2), (3 + 12, 2), "catalog на большем наборе: число авторов и запросов")
    if bad:
        return bad
    return expect(cat["Zed03"], ["Z03-0", "Z03-1", "Z03-2"], "книги автора в catalog")


case("filter-exclude", t_filter_exclude)
case("annotate-aggregate", t_annotate_aggregate)
case("lazy-queryset", t_lazy)
case("select-related", t_select_related)
case("prefetch-related", t_prefetch)
case("query-count-scale", t_scale)
