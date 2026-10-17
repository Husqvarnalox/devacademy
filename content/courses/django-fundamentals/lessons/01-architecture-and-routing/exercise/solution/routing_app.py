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

    regex = "0[1-9]|1[0-2]"

    def to_python(self, value):
        return int(value)

    def to_url(self, value):
        return "%02d" % int(value)


register_converter(MonthConverter, "month")


@require_GET
def home(request):
    return text("home")


def hello(request, name):
    return text(f"Hello, {name}!")


def archive(request, year, month):
    return text(f"archive {year}-{month:02d}")


def file_view(request, subpath):
    return text(f"file: {subpath}")


def user_detail(request, uid):
    return text(f"user {uid}")


urlpatterns = [
    path("", home, name="home"),
    path("hello/<str:name>/", hello, name="hello"),
    path("archive/<int:year>/<month:month>/", archive, name="post-archive"),
    path("files/<path:subpath>", file_view, name="file"),
    path("users/<uuid:uid>/", user_detail, name="user-detail"),
]


if __name__ == "__main__":
    from django.test import Client

    client = Client()
    for url in ("/", "/hello/Ann/", "/archive/2024/03/", "/files/a/b.txt", "/nope/"):
        response = client.get(url)
        print(url, response.status_code, response.content.decode())
