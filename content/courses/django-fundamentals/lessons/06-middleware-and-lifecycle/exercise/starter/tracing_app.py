"""Middleware и жизненный цикл запроса. Каркас не меняйте; ваш код - ниже строки «ВАШ КОД»."""
import itertools
import re

import django
from django.conf import settings

# ---- каркас: настройки, представления, журнал ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[],
        # Порядок важен: первый в списке - самый внешний слой.
        MIDDLEWARE=[
            __name__ + ".RequestIdMiddleware",
            __name__ + ".AccessLogMiddleware",
            __name__ + ".ErrorJsonMiddleware",
        ],
        USE_TZ=True,
    )
    django.setup()

from django.core.signals import request_finished
from django.dispatch import receiver
from django.http import HttpResponse, JsonResponse
from django.urls import path

LOG = []        # строки журнала доступа
FINISHED = []   # сюда receiver пишет по одной записи на завершённый запрос
VALID_ID = re.compile(r"[A-Za-z0-9-]{1,36}")
_counter = itertools.count(1)


def reset_state():
    """Для тестов: очистить журналы и начать нумерацию идентификаторов заново."""
    global _counter
    LOG.clear()
    FINISHED.clear()
    _counter = itertools.count(1)


def new_request_id():
    """Следующий сгенерированный идентификатор: req-0001, req-0002, ..."""
    return f"req-{next(_counter):04d}"


def ping(request):
    return HttpResponse("pong")


def boom(request):
    raise ValueError("kaboom")


def echo(request, word):
    return JsonResponse({"word": word, "request_id": request.request_id})


urlpatterns = [
    path("ping/", ping),
    path("boom/", boom),
    path("echo/<str:word>/", echo),
]


# ---- ВАШ КОД ниже ----
class RequestIdMiddleware:
    """Кладёт request.request_id и добавляет заголовок X-Request-ID к ответу.
    Идентификатор берётся из заголовка запроса X-Request-ID, если он подходит под VALID_ID; иначе new_request_id()."""

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        raise NotImplementedError  # TODO: request.request_id = ...; response = self.get_response(request); заголовок


class AccessLogMiddleware:
    """После ответа дописывает в LOG строку '<id> <METHOD> <path> <status>', например 'req-0001 GET /ping/ 200'."""

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        raise NotImplementedError


class ErrorJsonMiddleware:
    """Необработанное исключение представления -> JSON 500 {"error": "internal", "request_id": ...} (хук process_exception)."""

    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        return self.get_response(request)

    # TODO: def process_exception(self, request, exception)


# TODO: функция-receiver для сигнала request_finished: добавляет "finished" в FINISHED (декоратор @receiver).
