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
from django.test.utils import CaptureQueriesContext

import nplus1_report as m


def counted(fn):
    with CaptureQueriesContext(connection) as ctx:
        result = fn()
    return result, len(ctx)


def clear():
    m.Review.objects.all().delete()
    m.Book.objects.all().delete()
    m.Author.objects.all().delete()


def seed():
    clear()
    ann, bob, _cid = (m.Author.objects.create(name=n) for n in ("Ann", "Bob", "Cid"))
    rows = [("Alpha", ann, [5, 3]), ("Gamma", ann, [2, 3, 5]), ("Beta", bob, [4]), ("Delta", bob, []), ("Epsilon", bob, [1])]
    for title, author, ratings in rows:
        book = m.Book.objects.create(title=title, author=author, price=100, year=2000)
        for rating in ratings:
            m.Review.objects.create(book=book, rating=rating)


REPORT = [
    "Alpha | Ann | reviews=2 | avg=4.0",
    "Beta | Bob | reviews=1 | avg=4.0",
    "Delta | Bob | reviews=0 | avg=-",
    "Epsilon | Bob | reviews=1 | avg=1.0",
    "Gamma | Ann | reviews=3 | avg=3.3",
]
SUMMARY = ["Ann: 2 books, 5 reviews", "Bob: 3 books, 2 reviews", "Cid: 0 books, 0 reviews"]


def t_report():
    seed()
    got, n = counted(m.report_lines)
    bad = expect(got, REPORT, "report_lines")
    if bad:
        return bad
    return expect(n, 1, "число запросов report_lines (нужен один: select_related + annotate)")


def t_summary():
    seed()
    got, n = counted(m.author_summary)
    bad = expect(got, SUMMARY, "author_summary")
    if bad:
        return bad
    return expect(n, 1, "число запросов author_summary (нужен один: annotate с Count)")


def t_report_values():
    seed()
    got, n = counted(m.report_lines)
    bad = expect(got[4], "Gamma | Ann | reviews=3 | avg=3.3", "среднее по всем отзывам книги (3.33 -> 3.3)")
    if bad:
        return bad
    return expect(n, 1, "число запросов report_lines")


def t_scale():
    seed()
    ann = m.Author.objects.get(name="Ann")
    for i in range(15):
        book = m.Book.objects.create(title=f"Extra{i:02d}", author=ann, price=1, year=1)
        m.Review.objects.create(book=book, rating=5)
    r, n1 = counted(m.report_lines)
    s, n2 = counted(m.author_summary)
    bad = expect((len(r), n1), (20, 1), "report_lines на большем наборе: строк и запросов")
    if bad:
        return bad
    return expect((s[0], n2), ("Ann: 17 books, 20 reviews", 1), "author_summary на большем наборе")


def t_edges():
    clear()
    a = m.Author.objects.create(name="Solo")
    m.Author.objects.create(name="Empty")
    m.Book.objects.create(title="Only", author=a, price=1, year=1)
    r, n1 = counted(m.report_lines)
    s, n2 = counted(m.author_summary)
    bad = expect((r, n1), (["Only | Solo | reviews=0 | avg=-"], 1), "книга без отзывов")
    if bad:
        return bad
    return expect((s, n2), (["Empty: 0 books, 0 reviews", "Solo: 1 books, 0 reviews"], 1), "автор без книг и книга без отзывов")


def t_no_multiplication():
    clear()
    a = m.Author.objects.create(name="Mult")
    for t in ("B1", "B2"):
        b = m.Book.objects.create(title=t, author=a, price=1, year=1)
        for rating in (1, 2, 3):
            m.Review.objects.create(book=b, rating=rating)
    s, n = counted(m.author_summary)
    bad = expect(s, ["Mult: 2 books, 6 reviews"], "два JOIN умножают строки: Count('books') нужен с distinct=True")
    if bad:
        return bad
    return expect(n, 1, "число запросов")


case("report-lines", t_report)
case("author-summary", t_summary)
case("average-value", t_report_values)
case("scales-with-data", t_scale)
case("edge-cases", t_edges)
case("join-multiplication", t_no_multiplication)
