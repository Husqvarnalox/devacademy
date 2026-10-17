<!-- section: simple -->
## Django - это «заводская линия» для запроса

Браузер присылает HTTP-запрос, сервер должен вернуть HTTP-ответ. Django делит эту работу на роли, и каждая живёт в своём месте:

* **URLconf** (`urlpatterns`) смотрит на адрес и выбирает, какая функция займётся запросом.
* **View** (представление) - обычная функция (или класс), которая принимает объект запроса и возвращает объект ответа. Здесь живёт логика.
* **Model** описывает данные и общается с базой через ORM (следующий урок).
* **Template** превращает данные в HTML (урок про шаблоны).
* **Settings** - единая точка настройки: база данных, список приложений, middleware, секретный ключ.

Django называет свою схему **MTV** (Model-Template-View). То, что в классическом MVC называют «контроллером», в Django - это *представление*, а «представлением» MVC - *шаблон*. Названия отличаются, но идея та же: адрес -> функция -> данные -> ответ.

Обычно проект разворачивают командами `django-admin startproject` и `manage.py`: появляются папки, `settings.py`, `urls.py`. В этом курсе мы ставим тот же самый Django, но **собираем проект в одном файле** и проверяем его без сервера и без сети: это те же настройки и тот же URLconf, только без лишней церемонии. Так всё видно целиком и легко запускается в упражнениях.

<!-- section: precise -->
## Как это устроено

**Цикл запроса.** Веб-сервер (WSGI или ASGI) передаёт запрос в `django.core.handlers`. Обработчик создаёт `HttpRequest`, прогоняет его через цепочку middleware (устройство - в [уроке про middleware](lesson:django.fundamentals.middleware-and-lifecycle)), резолвит адрес по URLconf, вызывает найденное представление и возвращает `HttpResponse` обратно по той же цепочке. Если представление бросило `Http404`, Django превратит это в ответ 404; другое необработанное исключение станет ответом 500.

**Settings.** Модуль настроек задаёт `DJANGO_SETTINGS_MODULE`; но настройки можно задать и из кода: `settings.configure(**kwargs)` вызывается один раз до того, как что-либо прочитает настройки. Читаются только имена в ВЕРХНЕМ регистре. Обращение к настройке до конфигурации даёт `ImproperlyConfigured`. Объект `django.conf.settings` ленивый: он настраивается при первом обращении.

**Apps.** Приложение (`INSTALLED_APPS`) - единица проекта со своими моделями, шаблонами и командами. Реестр приложений заполняет `django.setup()`; он же импортирует модули моделей. Без `django.setup()` модели, ORM и шаблоны не работают. В полноценном проекте приложение - это пакет с `models.py` и `apps.py`; в нашем однофайловом проекте приложением служит класс `AppConfig`, объявленный прямо в файле (о нём - в уроке про модели).

**Типичный однофайловый каркас** (используется во всех упражнениях курса):

```python
import django
from django.conf import settings

if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret",
        ROOT_URLCONF=__name__,          # URLconf - этот же модуль
        ALLOWED_HOSTS=["testserver"],   # хост, который подставляет django.test.Client
        INSTALLED_APPS=[],
        MIDDLEWARE=[],
    )
    django.setup()
```

`ROOT_URLCONF=__name__` означает «маршруты - это переменная `urlpatterns` текущего модуля». Условие `if not settings.configured` делает модуль безопасным для повторного импорта. Запросы к такому проекту отправляет `django.test.Client`: он собирает `HttpRequest`, вызывает полный обработчик (с middleware) и возвращает ответ, не открывая сокетов. База - SQLite в памяти (`":memory:"`), её таблицы создаёт код упражнения. Всё выполняется в одном процессе, поэтому упражнения быстрые и воспроизводимые.

**URLconf и конвертеры путей.** `path("articles/<int:year>/", view, name="archive")` сопоставляет адрес по порядку, побеждает первое совпадение. Встроенные конвертеры: `str` (любой непустой текст без `/`, по умолчанию), `int` (неотрицательное целое -> `int`), `slug` (буквы, цифры, `-`, `_`), `uuid` (канонический UUID в нижнем регистре -> `uuid.UUID`), `path` (как `str`, но может содержать `/`). Свой конвертер - класс с атрибутом `regex` и методами `to_python(value)` и `to_url(value)`, регистрируется `register_converter`. Если значение не подошло под `regex`, маршрут просто не совпадает (будет 404), а не бросает ошибку. Параметры из адреса передаются в представление именованными аргументами.

**Имена маршрутов и обратное разрешение.** Параметр `name=` позволяет получать адрес по имени: `reverse("archive", kwargs={"year": 2024})`. Захардкоженные адреса в шаблонах и коде - частый источник поломок при переименовании: ссылайтесь по имени. Если имя неизвестно или аргументы не подходят, `reverse` бросает `NoReverseMatch`. `to_url` конвертера используется именно при обратном разрешении.

**Запрос и ответ.** `HttpRequest` содержит `method`, `path`, `GET` и `POST` (`QueryDict`: один ключ может иметь несколько значений, `getlist`), `headers`, `COOKIES`, `body`. `HttpResponse(content, status=200, content_type=...)` по умолчанию отдаёт `text/html; charset=utf-8`; для простого текста передают `content_type="text/plain; charset=utf-8"`. Декораторы `require_GET`, `require_POST`, `require_http_methods([...])` отвечают `405 Method Not Allowed` с заголовком `Allow`.

**Версии.** Курс проверен на Django 6.1.x и Python 3.14 (Django 6.1 поддерживает Python 3.12-3.14). Релизы, запланированные когда-то как 7.0 и 7.1, по новой схеме называются «Django 2028» и «Django 2029» (календарное версионирование): для вас это значит, что «следующая мажорная версия» живёт в 2028 году. Устаревшие возможности помечены предупреждениями `RemovedInDjango2028Warning` и подобными: запускайте тесты с включёнными предупреждениями.

<!-- section: examples -->
## Примеры

**Пример 1. Целый проект в одном файле.** Настройки, представление, URLconf и клиент. Клиент проходит весь цикл запроса.

```python run
import django
from django.conf import settings

if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[],
        MIDDLEWARE=[],
    )
    django.setup()

from django.http import HttpResponse
from django.test import Client
from django.urls import path, resolve


def hello(request, name):
    return HttpResponse(f"Hello, {name}!", content_type="text/plain; charset=utf-8")


urlpatterns = [path("hello/<str:name>/", hello, name="hello")]

response = Client().get("/hello/Ann/")
print(response.status_code, response.content.decode(), response["Content-Type"])
match = resolve("/hello/Ann/")
print(match.func.__name__, match.kwargs, match.url_name)
print(settings.ROOT_URLCONF == __name__)
```
```output
200 Hello, Ann! text/plain; charset=utf-8
hello {'name': 'Ann'} hello
True
```

**Пример 2. Запрос и ответ: что видит представление.** `QueryDict` хранит несколько значений на ключ; статус и заголовки ответа - обычные атрибуты.

```python run
import django
from django.conf import settings

if not settings.configured:
    settings.configure(ROOT_URLCONF=__name__, ALLOWED_HOSTS=["testserver"], SECRET_KEY="x", INSTALLED_APPS=[], MIDDLEWARE=[])
    django.setup()

from django.http import HttpResponse
from django.test import Client
from django.urls import path
from django.views.decorators.http import require_http_methods


@require_http_methods(["GET", "POST"])
def echo(request):
    body = [request.method, request.path, request.GET.get("q"), request.GET.getlist("tag"), request.headers.get("X-Demo")]
    return HttpResponse(repr(body), status=201 if request.method == "POST" else 200)


urlpatterns = [path("echo/", echo)]
c = Client()
r = c.get("/echo/?q=django&tag=a&tag=b", headers={"X-Demo": "1"})
print(r.status_code, r.content.decode())
r = c.post("/echo/")
print(r.status_code)
r = c.delete("/echo/")
print(r.status_code, r["Allow"])
```
```output
200 ['GET', '/echo/', 'django', ['a', 'b'], '1']
201
405 GET, POST
```

**Пример 3. Конвертеры, `resolve` и `reverse`.** Свой конвертер двух цифр месяца; значения приходят в представление уже приведёнными типами, а неподходящий адрес не совпадает.

```python run
import django
from django.conf import settings

if not settings.configured:
    settings.configure(ROOT_URLCONF=__name__, ALLOWED_HOSTS=["testserver"], SECRET_KEY="x", INSTALLED_APPS=[], MIDDLEWARE=[])
    django.setup()

from django.http import HttpResponse
from django.test import Client
from django.urls import NoReverseMatch, path, register_converter, resolve, reverse


class MonthConverter:
    regex = "0[1-9]|1[0-2]"

    def to_python(self, value):
        return int(value)

    def to_url(self, value):
        return "%02d" % value


register_converter(MonthConverter, "month")


def archive(request, year, month):
    return HttpResponse(f"{year}/{month}: {type(year).__name__}, {type(month).__name__}")


urlpatterns = [path("archive/<int:year>/<month:month>/", archive, name="archive")]

print(resolve("/archive/2024/03/").kwargs)
print(reverse("archive", kwargs={"year": 2024, "month": 3}))
c = Client()
print(c.get("/archive/2024/03/").content.decode())
print(c.get("/archive/2024/13/").status_code, c.get("/archive/2024/3/").status_code)
try:
    reverse("archive", kwargs={"year": 2024})
except NoReverseMatch:
    print("NoReverseMatch")
```
```output
{'year': 2024, 'month': 3}
/archive/2024/03/
2024/3: int, int
404 404
NoReverseMatch
```

**Пример 4. Настройки ленивые.** До `configure` чтение настройки - ошибка; имена только в верхнем регистре; повторная конфигурация запрещена.

```python run
from django.conf import settings
from django.core.exceptions import ImproperlyConfigured

try:
    settings.DEBUG
except ImproperlyConfigured:
    print("ImproperlyConfigured до configure")

try:
    settings.configure(debug=True)
except TypeError as exc:
    print("TypeError:", exc)
settings.configure(DEBUG=True)
print(settings.DEBUG, settings.configured)
try:
    settings.configure(DEBUG=False)
except RuntimeError as exc:
    print("RuntimeError:", exc)
```
```output
ImproperlyConfigured до configure
TypeError: Setting 'debug' must be uppercase.
True True
RuntimeError: Settings already configured.
```

<!-- section: mistakes -->
## Типичные ошибки

* **Вызвать `settings.configure()` дважды**: Django бросит `RuntimeError`. Защищайтесь условием `if not settings.configured`.
* **Забыть `django.setup()`**: модели и шаблоны падают с `AppRegistryNotReady` или `ImproperlyConfigured`.
* **Импортировать модели до `settings.configure()`**: порядок в однофайловом проекте строгий - сначала настройки и `setup()`, потом `from django.db import models`.
* **Не указать `ALLOWED_HOSTS` при `DEBUG=False`**: любой запрос через `Client` получит 400. Тестовый клиент использует хост `testserver`.
* **Жёстко прописать адреса вместо `reverse`.** Переименуете маршрут - сломаются все ссылки.
* **Порядок маршрутов.** Первое совпадение выигрывает: `path("users/<str:name>/", ...)` перед `path("users/new/", ...)` перехватит `new`. Конкретные маршруты ставят выше общих.
* **Забыть завершающий `/`** в шаблоне маршрута: `files/x` и `files/x/` - разные адреса (при `APPEND_SLASH=True` Django перенаправляет, но только при включённом `CommonMiddleware`).
* **Считать `int` конвертер знающим знак**: он принимает только неотрицательные целые; `/items/-1/` не совпадёт.

<!-- section: summary -->
## Коротко

* Django = URLconf -> view -> (model, template) -> response; настройки - единый источник конфигурации.
* Однофайловый проект: `settings.configure(...)` и `django.setup()` в начале, `ROOT_URLCONF=__name__`, `urlpatterns` ниже; проверка через `django.test.Client` без сети.
* Конвертеры `str`, `int`, `slug`, `uuid`, `path`; свой - класс с `regex`, `to_python`, `to_url`.
* Имена маршрутов и `reverse` вместо жёстких адресов.
* `HttpResponse` по умолчанию `text/html`; методы ограничивают `require_http_methods`.
* Версия курса: Django 6.1.x; даты следующих мажорных версий - календарные.
