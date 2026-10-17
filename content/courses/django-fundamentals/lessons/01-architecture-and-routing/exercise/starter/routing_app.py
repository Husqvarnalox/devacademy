"""Мини-проект Django в одном файле: настройки, представления и URLconf живут в этом модуле.

Блок «каркас» не меняйте. Ваша часть - всё, что ниже строки «ВАШ КОД».
"""
import uuid  # noqa: F401  (UUID приходит из конвертера uuid)

import django
from django.conf import settings

# ---- каркас: программная настройка вместо settings.py + manage.py ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,  # URLconf - этот же модуль (переменная urlpatterns)
        ALLOWED_HOSTS=["testserver"],  # хост, который подставляет django.test.Client
        INSTALLED_APPS=[],
        MIDDLEWARE=[],
        USE_TZ=True,
    )
    django.setup()

from django.http import Http404, HttpResponse
from django.urls import path, register_converter
from django.views.decorators.http import require_GET


def text(body):
    """Текстовый ответ 200 с типом text/plain."""
    return HttpResponse(body, content_type="text/plain; charset=utf-8")


# ---- ВАШ КОД ниже ----
class MonthConverter:
    """Конвертер <month:...>: ровно две цифры 01..12. В Python значение - int, в URL - строка из двух цифр."""

    regex = ""  # TODO: регулярное выражение без якорей, например для 01..12

    def to_python(self, value):
        raise NotImplementedError

    def to_url(self, value):
        raise NotImplementedError


register_converter(MonthConverter, "month")


# TODO: каждое представление возвращает text(...) (200, text/plain)
@require_GET
def home(request):
    raise NotImplementedError  # "home"


def hello(request, name):
    raise NotImplementedError  # "Hello, <name>!"


def archive(request, year, month):
    raise NotImplementedError  # "archive 2024-03" (месяц из двух цифр)


def file_view(request, subpath):
    raise NotImplementedError  # "file: <subpath>"


def user_detail(request, uid):
    raise NotImplementedError  # "user <uuid>"


# TODO: маршруты с именами home, hello, post-archive, file, user-detail
urlpatterns = []


if __name__ == "__main__":
    from django.test import Client

    client = Client()
    for url in ("/", "/hello/Ann/", "/archive/2024/03/", "/files/a/b.txt", "/nope/"):
        response = client.get(url)
        print(url, response.status_code, response.content.decode())
