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


def _positive_int(raw, default):
    if raw is None:
        return default
    if not raw.isdigit() or int(raw) < 1:
        return None
    return int(raw)


@require_http_methods(["GET"])
def note_list(request):
    qs = Note.objects.all()
    done = request.GET.get("done")
    if done is not None:
        if done not in ("true", "false"):
            return JsonResponse({"error": "invalid done"}, status=400)
        qs = qs.filter(done=(done == "true"))
    q = request.GET.get("q")
    if q:
        qs = qs.filter(title__icontains=q)
    size = _positive_int(request.GET.get("page_size"), DEFAULT_SIZE)
    if size is None or size > MAX_SIZE:
        return JsonResponse({"error": "invalid page_size"}, status=400)
    page = _positive_int(request.GET.get("page"), 1)
    count = qs.count()
    pages = max(1, -(-count // size))
    if page is None or page > pages:
        return JsonResponse({"error": "invalid page"}, status=404)
    start = (page - 1) * size
    results = [serialize(n) for n in qs[start:start + size]]
    return JsonResponse({"count": count, "page": page, "pages": pages, "results": results})


urlpatterns = [path("notes/", note_list, name="note-list")]
