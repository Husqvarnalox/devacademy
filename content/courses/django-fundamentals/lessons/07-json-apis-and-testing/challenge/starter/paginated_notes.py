"""Пагинация и фильтры списка. Каркас не меняйте; ваш код - ниже строки «ВАШ КОД»."""
import json

import django
from django.apps import AppConfig
from django.conf import settings


class NotesConfig(AppConfig):
    name = __name__
    label = "notes"


# ---- каркас: Django в одном файле, SQLite в памяти ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[__name__ + ".NotesConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
        USE_TZ=True,
    )
    django.setup()

from django.db import connection, models
from django.http import JsonResponse
from django.urls import path
from django.views.decorators.csrf import csrf_exempt
from django.views.decorators.http import require_http_methods


class Note(models.Model):
    title = models.CharField(max_length=100)
    done = models.BooleanField(default=False)

    class Meta:
        app_label = "notes"
        ordering = ["id"]


with connection.schema_editor() as _editor:
    _editor.create_model(Note)


def serialize(note):
    return {"id": note.pk, "title": note.title, "done": note.done}


def seed(n):
    """Для тестов: n заметок 'note 1'..'note n'; каждая третья выполнена."""
    Note.objects.all().delete()
    Note.objects.bulk_create([Note(title=f"note {i}", done=(i % 3 == 0)) for i in range(1, n + 1)])


# ---- ВАШ КОД ниже ----
DEFAULT_SIZE, MAX_SIZE = 5, 20


@require_http_methods(["GET"])
def note_list(request):
    """Пока возвращает все заметки. Нужно: фильтры ?done=true|false и ?q=<подстрока в title без учёта регистра>,
    пагинация ?page=N (с 1) и ?page_size=N (по умолчанию 5, максимум 20).
    Ответ: {"count": всего после фильтров, "page": N, "pages": число страниц (минимум 1), "results": [...]}.
    done не true/false или page_size не целое число 1..20 -> 400 {"error": "invalid done"} / {"error": "invalid page_size"};
    page не целое >= 1 или больше pages -> 404 {"error": "invalid page"}. Порядок - по id. Запросов к базе не больше двух."""
    return JsonResponse({"results": [serialize(n) for n in Note.objects.all()]})


urlpatterns = [path("notes/", note_list, name="note-list")]
