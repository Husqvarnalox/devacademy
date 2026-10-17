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
    """Возвращает (data, error_response). Тело должно быть JSON-объектом с Content-Type application/json."""
    if request.content_type != "application/json":
        return None, JsonResponse({"error": "content type must be application/json"}, status=415)
    try:
        data = json.loads(request.body)
    except ValueError:
        return None, JsonResponse({"error": "invalid json"}, status=400)
    if not isinstance(data, dict):
        return None, JsonResponse({"error": "invalid json"}, status=400)
    return data, None


def validate(data):
    """Возвращает (очищенные данные, ошибки). title: непустая строка до 100 символов; done: bool (по умолчанию False)."""
    errors = {}
    title = data.get("title")
    if not isinstance(title, str) or not title.strip():
        errors["title"] = "required"
    elif len(title.strip()) > 100:
        errors["title"] = "too_long"
    done = data.get("done", False)
    if not isinstance(done, bool):
        errors["done"] = "invalid"
    if errors:
        return None, errors
    return {"title": title.strip(), "done": done}, None


@csrf_exempt
@require_http_methods(["GET", "POST"])
def note_list(request):
    if request.method == "GET":
        return JsonResponse({"results": [serialize(n) for n in Note.objects.all()]})
    data, error = parse_json(request)
    if error:
        return error
    clean, errors = validate(data)
    if errors:
        return JsonResponse({"errors": errors}, status=400)
    note = Note.objects.create(**clean)
    response = JsonResponse(serialize(note), status=201)
    response["Location"] = f"/notes/{note.pk}/"
    return response


@csrf_exempt
@require_http_methods(["GET", "PUT", "DELETE"])
def note_detail(request, pk):
    try:
        note = Note.objects.get(pk=pk)
    except Note.DoesNotExist:
        return JsonResponse({"error": "not found"}, status=404)
    if request.method == "GET":
        return JsonResponse(serialize(note))
    if request.method == "DELETE":
        note.delete()
        return JsonResponse({}, status=204)
    data, error = parse_json(request)
    if error:
        return error
    clean, errors = validate(data)
    if errors:
        return JsonResponse({"errors": errors}, status=400)
    note.title, note.done = clean["title"], clean["done"]
    note.save()
    return JsonResponse(serialize(note))


urlpatterns = [
    path("notes/", note_list, name="note-list"),
    path("notes/<int:pk>/", note_detail, name="note-detail"),
]
