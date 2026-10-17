"""CRUD JSON API без DRF. Каркас не меняйте; ваш код - ниже строки «ВАШ КОД»."""
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


# ---- ВАШ КОД ниже ----
def parse_json(request):
    """Возвращает (data, error_response). Тело - JSON-объект с Content-Type application/json:
    другой Content-Type -> 415 {"error": "content type must be application/json"};
    невалидный JSON или не объект -> 400 {"error": "invalid json"}."""
    raise NotImplementedError


def validate(data):
    """Возвращает (очищенные данные, ошибки). title: непустая строка до 100 символов после strip
    (ошибки "required" / "too_long"); done: bool, по умолчанию False (ошибка "invalid")."""
    raise NotImplementedError


def note_list(request):
    """GET -> {"results": [serialize(n), ...]} по возрастанию id.
    POST -> 201, тело serialize(note), заголовок Location /notes/<id>/;
    ошибки проверки -> 400 {"errors": {поле: код}}; остальные методы -> 405."""
    raise NotImplementedError


def note_detail(request, pk):
    """GET -> 200 serialize(note); PUT -> полная замена (те же проверки, 200); DELETE -> 204;
    нет записи -> 404 {"error": "not found"}; остальные методы -> 405."""
    raise NotImplementedError


urlpatterns = []  # TODO: notes/ (note-list) и notes/<int:pk>/ (note-detail)
