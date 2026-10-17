<!-- section: simple -->
## JSON API на чистом Django

Для небольшого API фреймворк поверх Django не обязателен. Всё необходимое уже есть: представление читает запрос, проверяет его, работает с моделью и возвращает `JsonResponse` с правильным статусом. Сторонние пакеты (например, Django REST framework) добавляют сериализаторы, права и документацию, но принципы те же, и их полезно понимать до выбора инструмента.

Контракт API состоит из четырёх частей:

* **адрес и метод** (`GET /notes/`, `POST /notes/`);
* **формат тела** (JSON-объект с понятными полями);
* **статусы** (`200`, `201`, `204`, `400`, `404`, `405`, `415`);
* **формат ошибок** (стабильные коды, а не только тексты).

Тестирование API в Django не требует сервера: тестовый `Client` вызывает цепочку обработки напрямую, как это делали примеры предыдущих уроков.

<!-- section: precise -->
## Как это устроено

**Чтение JSON.** Тело запроса - байты в `request.body`. Разбирайте его `json.loads` внутри `try`: ошибка разбора - это `400`, а не `500`. Проверьте тип результата: `[1, 2]` или `"text"` тоже валидный JSON, но не объект. Проверяйте `request.content_type`: если клиент прислал `text/plain`, честный ответ `415`. `request.POST` для JSON пуст: он заполняется только формами.

**Проверка данных.** Для API подходят те же формы (`Form(data)`) или ручная проверка. В любом случае соберите все ошибки сразу в `{"поле": "код"}` и верните одним ответом `400`, чтобы клиенту не приходилось исправлять их по одной.

**Ответы.** `JsonResponse(data, status=...)` сериализует словарь и ставит `Content-Type: application/json`. Для списка (не словаря) нужен `safe=False`, но надёжнее всегда возвращать объект с ключом (`{"results": [...]}`): так можно добавить `count` и страницы без поломки клиентов. Статусы: `201` и заголовок `Location` при создании, `204` без тела при удалении, `404` для отсутствующей записи, `405` с заголовком `Allow` для неподдерживаемого метода (`require_http_methods`).

**CSRF и аутентификация.** Если API отвечает браузеру с сессионной cookie, нужна CSRF-защита (токен в заголовке `X-CSRFToken`). Если запросы авторизуются заголовком (токен в `Authorization`), cookie не участвует, и `csrf_exempt` допустим. Выбор должен быть осознанным (урок про формы).

**Пагинация и фильтры.** Список нельзя отдавать целиком: клиент и база задыхаются на больших таблицах. Параметры `?page=` и `?page_size=` превращаются в срез `qs[start:start+size]` (SQL `LIMIT/OFFSET`), общее число даёт один `COUNT`. Размер страницы ограничивают сверху, а некорректные параметры отклоняют понятной ошибкой. Порядок должен быть детерминированным (`order_by("id")`), иначе записи на границах страниц будут повторяться или пропадать. Фильтры применяются до подсчёта `count`.

**Тесты.** `Client.post(url, data=json.dumps(...), content_type="application/json")`; `response.json()` разбирает ответ (и проверяет заголовок типа). `CaptureQueriesContext` считает запросы к базе. В полноценном проекте тесты наследуют `django.test.TestCase`: каждый тест идёт в транзакции, которая откатывается, и база остаётся чистой; запускает их `python manage.py test`. В однофайловых примерах курса вместо этого очищают таблицы вручную.

```python
from django.test import TestCase


class NoteApiTests(TestCase):
    def test_create(self):
        response = self.client.post("/notes/", {"title": "a"}, content_type="application/json")
        self.assertEqual(response.status_code, 201)
```

<!-- section: examples -->
## Примеры

**Пример 1. Создание и чтение.** Разбор JSON с проверкой типа, `201` с `Location`, `404` для отсутствующей записи.

```python run
import json

import django
from django.apps import AppConfig
from django.conf import settings


class DemoConfig(AppConfig):
    name = __name__
    label = "demo"


settings.configure(
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    INSTALLED_APPS=[__name__ + ".DemoConfig"],
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django.db import connection, models
from django.http import JsonResponse
from django.test import Client
from django.urls import path
from django.views.decorators.http import require_http_methods


class Item(models.Model):
    name = models.CharField(max_length=50)

    class Meta:
        app_label = "demo"
        ordering = ["id"]


with connection.schema_editor() as editor:
    editor.create_model(Item)


def parse(request):
    try:
        data = json.loads(request.body)
    except ValueError:
        return None
    return data if isinstance(data, dict) else None


@require_http_methods(["GET", "POST"])
def items(request):
    if request.method == "GET":
        return JsonResponse({"results": list(Item.objects.values("id", "name"))})
    data = parse(request)
    if data is None or not isinstance(data.get("name"), str) or not data["name"]:
        return JsonResponse({"errors": {"name": "required"}}, status=400)
    item = Item.objects.create(name=data["name"])
    response = JsonResponse({"id": item.pk, "name": item.name}, status=201)
    response["Location"] = f"/items/{item.pk}/"
    return response


def item(request, pk):
    obj = Item.objects.filter(pk=pk).first()
    if obj is None:
        return JsonResponse({"error": "not found"}, status=404)
    return JsonResponse({"id": obj.pk, "name": obj.name})


urlpatterns = [path("items/", items), path("items/<int:pk>/", item)]
c = Client()
r = c.post("/items/", data=json.dumps({"name": "pen"}), content_type="application/json")
print(r.status_code, r["Location"], r.json())
print(c.post("/items/", data="[1]", content_type="application/json").json())
print(c.post("/items/", data="{oops", content_type="application/json").status_code)
print(c.get("/items/1/").json(), c.get("/items/9/").status_code)
print(c.delete("/items/").status_code, c.delete("/items/")["Allow"])
```
```output
201 /items/1/ {'id': 1, 'name': 'pen'}
{'errors': {'name': 'required'}}
400
{'id': 1, 'name': 'pen'} 404
405 GET, POST
```

**Пример 2. Пагинация: срез и число запросов.** Список из 25 записей отдаётся страницами; `CaptureQueriesContext` показывает, что страница стоит двух запросов, а SQL содержит `LIMIT`.

```python run
import json

import django
from django.apps import AppConfig
from django.conf import settings


class DemoConfig(AppConfig):
    name = __name__
    label = "demo"


settings.configure(
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    INSTALLED_APPS=[__name__ + ".DemoConfig"],
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django.db import connection, models
from django.http import JsonResponse
from django.test import Client
from django.urls import path
from django.views.decorators.http import require_http_methods


class Item(models.Model):
    name = models.CharField(max_length=50)

    class Meta:
        app_label = "demo"
        ordering = ["id"]


with connection.schema_editor() as editor:
    editor.create_model(Item)

from django.test.utils import CaptureQueriesContext


@require_http_methods(["GET"])
def items(request):
    size = min(int(request.GET.get("page_size", 10)), 50)
    page = int(request.GET.get("page", 1))
    count = Item.objects.count()
    start = (page - 1) * size
    rows = list(Item.objects.values_list("name", flat=True)[start:start + size])
    return JsonResponse({"count": count, "page": page, "pages": -(-count // size), "results": rows})


urlpatterns = [path("items/", items)]
Item.objects.bulk_create([Item(name=f"item {i}") for i in range(1, 26)])
c = Client()
with CaptureQueriesContext(connection) as ctx:
    data = c.get("/items/?page=3&page_size=10").json()
print(data)
print(len(ctx), "LIMIT" in ctx.captured_queries[-1]["sql"])
```
```output
{'count': 25, 'page': 3, 'pages': 3, 'results': ['item 21', 'item 22', 'item 23', 'item 24', 'item 25']}
2 True
```

**Пример 3. Тест как таблица случаев.** Один набор запросов проверяет статусы и коды ошибок, а не тексты; так же устроены тесты упражнения.

```python run
import json

import django
from django.apps import AppConfig
from django.conf import settings


class DemoConfig(AppConfig):
    name = __name__
    label = "demo"


settings.configure(
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    INSTALLED_APPS=[__name__ + ".DemoConfig"],
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django.db import connection, models
from django.http import JsonResponse
from django.test import Client
from django.urls import path
from django.views.decorators.http import require_http_methods


class Item(models.Model):
    name = models.CharField(max_length=50)

    class Meta:
        app_label = "demo"
        ordering = ["id"]


with connection.schema_editor() as editor:
    editor.create_model(Item)


@require_http_methods(["POST"])
def items(request):
    try:
        data = json.loads(request.body)
    except ValueError:
        return JsonResponse({"error": "invalid json"}, status=400)
    name = data.get("name") if isinstance(data, dict) else None
    if not isinstance(name, str) or not name.strip():
        return JsonResponse({"errors": {"name": "required"}}, status=400)
    return JsonResponse({"name": name.strip()}, status=201)


urlpatterns = [path("items/", items)]
cases = [
    ("valid", '{"name": " pen "}', "application/json"),
    ("empty", '{"name": " "}', "application/json"),
    ("not json", "{", "application/json"),
    ("list", "[]", "application/json"),
    ("form", "name=pen", "application/x-www-form-urlencoded"),
]
c = Client()
for label, body, ctype in cases:
    r = c.post("/items/", data=body, content_type=ctype)
    print(f"{label:9}", r.status_code, r.json())
```
```output
valid     201 {'name': 'pen'}
empty     400 {'errors': {'name': 'required'}}
not json  400 {'error': 'invalid json'}
list      400 {'errors': {'name': 'required'}}
form      400 {'error': 'invalid json'}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что JSON всегда объект.** `json.loads("[]")` успешно вернёт список, а `data.get` упадёт с `AttributeError` и даст `500`. Проверяйте `isinstance(data, dict)`.
* **Не ловить ошибку разбора.** Битое тело не должно давать `500`: это ошибка клиента, `400`.
* **Читать `request.POST` для JSON.** Он заполняется только формами; тело JSON лежит в `request.body`.
* **Отдавать статус `200` на всё.** Создание - `201`, удаление - `204`, ошибка клиента - `4xx`, отсутствующая запись - `404`.
* **Сообщать ошибки только текстом.** Клиент не должен разбирать русскую фразу: возвращайте стабильные коды.
* **Отдавать весь список.** На больших таблицах это перегружает базу и сеть: нужна пагинация с верхним пределом `page_size`.
* **Пагинация без порядка.** Без `order_by` порядок строк не гарантирован, и записи на границах страниц повторяются или теряются.
* **Подсчёт `count` до фильтров.** Число записей должно соответствовать отфильтрованной выборке.
* **`csrf_exempt` «на всякий случай».** Если API пользуется cookie браузера, отключение CSRF - уязвимость.
* **Тестировать только счастливый путь.** Проверьте битый JSON, неверный `Content-Type`, пустые значения, отсутствующую запись и неподдерживаемый метод.

<!-- section: summary -->
## Коротко

* JSON API на Django: разобрать тело (`json.loads` с обработкой ошибок и проверкой типа), проверить данные, вернуть `JsonResponse` с точным статусом.
* Статусы: `201` + `Location`, `204`, `400` (коды ошибок по полям), `404`, `405` + `Allow`, `415`.
* Всегда возвращайте объект верхнего уровня; для списков добавляйте `count`, `page`, `pages`.
* Пагинация - срез `LIMIT/OFFSET` и один `COUNT`, детерминированный порядок, ограничение `page_size`, фильтры до подсчёта.
* Тесты: `Client` с `content_type="application/json"`, `response.json()`, `CaptureQueriesContext` для числа запросов; в проекте - `TestCase` с откатом транзакции.
