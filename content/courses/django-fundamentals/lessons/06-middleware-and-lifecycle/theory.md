<!-- section: simple -->
## Слои вокруг каждого запроса

**Middleware** - это цепочка слоёв между сервером и вашим представлением. Каждый слой видит запрос на пути «внутрь» и ответ на пути «наружу»: так устроены сессии, аутентификация, CSRF-защита, сжатие, журналирование. Если нужно одно и то же действие для всех запросов (добавить заголовок, записать в журнал, измерить время, превратить ошибку в JSON), это работа для middleware, а не для копирования в каждое представление.

Цепочку можно представить как матрёшку: первый в списке `MIDDLEWARE` - самый внешний слой, последний - ближе всего к представлению. Запрос идёт сверху вниз, ответ возвращается снизу вверх.

Рядом с middleware есть **сигналы**: события, на которые можно подписаться (`request_finished`, `post_save` и другие). Сигнал сообщает «что-то произошло», но не может изменить запрос или ответ.

<!-- section: precise -->
## Как это устроено

**Форма middleware.** Это вызываемый объект с фабрикой: класс с `__init__(self, get_response)` и `__call__(self, request)`. `get_response` - следующий слой (или само представление). Код до вызова `get_response(request)` выполняется на пути вниз, после - на пути вверх. Слой может не вызывать `get_response` вовсе и вернуть ответ сразу (короткое замыкание, например отказ по IP).

**Порядок.** Запрос проходит слои в порядке списка `MIDDLEWARE`, ответ - в обратном. Поэтому `SessionMiddleware` ставят раньше `AuthenticationMiddleware` (ему нужна сессия), а слой, который должен видеть итоговый статус, ставят снаружи слоёв, которые этот статус меняют.

**Исключения.** Каждый слой обёрнут так, что исключение превращается в ответ (`500`, или `404` для `Http404`) уже внутри слоя, и внешние слои получают обычный ответ. Дополнительно у класса может быть хук `process_exception(request, exception)`: если он вернёт ответ, этот ответ заменит ошибку; `None` оставит обработку по умолчанию. Хуки `process_view` и `process_template_response` встречаются реже.

**Путь запроса целиком.** Сервер создаёт `HttpRequest`, прогоняет его через middleware, `ROOT_URLCONF` выбирает представление, представление возвращает ответ (для `TemplateResponse` шаблон рисуется позже), ответ идёт вверх по слоям, сервер отправляет его, после чего посылается сигнал `request_finished`.

**Сигналы.** `@receiver(signal)` подписывает функцию, которая принимает `sender` и `**kwargs`. Сигналы синхронны и выполняются в том же потоке: медленный приёмник замедляет запрос, а исключение в нём (при `send`) прерывает отправку. Для событий в процессе запроса часто достаточно middleware; сигналы хороши, когда нужно реагировать на событие модели или инфраструктуры независимо от того, кто его вызвал.

**Состояние.** Объект middleware создаётся один раз на процесс и обслуживает все запросы, поэтому данные запроса хранят в `request` (например, `request.request_id`), а не в `self`.

<!-- section: examples -->
## Примеры

**Пример 1. Порядок слоёв.** Каждый слой печатает вход и выход; видно «матрёшку».

```python run
import django
from django.conf import settings
from django.http import HttpResponse
from django.test import Client
from django.urls import path


def make(name):
    class Layer:
        def __init__(self, get_response):
            self.get_response = get_response

        def __call__(self, request):
            print("in ", name)
            response = self.get_response(request)
            print("out", name)
            return response

    Layer.__name__ = name
    globals()[name] = Layer
    return __name__ + "." + name


layers = [make("A"), make("B"), make("C")]
settings.configure(ROOT_URLCONF=__name__, ALLOWED_HOSTS=["testserver"], MIDDLEWARE=layers, USE_I18N=False)
django.setup()


def view(request):
    print("view")
    return HttpResponse("ok")


urlpatterns = [path("", view)]
print(Client().get("/").status_code)
```
```output
in  A
in  B
in  C
view
out C
out B
out A
200
```

**Пример 2. Короткое замыкание и ошибки.** Слой `Gate` отвечает `403`, не вызывая дальше; `Catcher` через `process_exception` превращает ошибку в JSON; внешний `Spy` всё равно видит обычный ответ.

```python run
import django
from django.conf import settings
from django.http import HttpResponse, JsonResponse
from django.test import Client
from django.urls import path


class Spy:
    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        response = self.get_response(request)
        print("spy sees", request.path, response.status_code)
        return response


class Gate:
    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        if request.headers.get("X-Block"):
            return HttpResponse("blocked", status=403)
        return self.get_response(request)


class Catcher:
    def __init__(self, get_response):
        self.get_response = get_response

    def __call__(self, request):
        return self.get_response(request)

    def process_exception(self, request, exception):
        return JsonResponse({"error": type(exception).__name__}, status=500)


settings.configure(
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    MIDDLEWARE=[__name__ + ".Spy", __name__ + ".Gate", __name__ + ".Catcher"],
    USE_I18N=False,
)
django.setup()


def ok(request):
    return HttpResponse("ok")


def boom(request):
    raise KeyError("x")


urlpatterns = [path("ok/", ok), path("boom/", boom)]
c = Client(raise_request_exception=False)
print(c.get("/ok/").status_code)
print(c.get("/ok/", headers={"X-Block": "1"}).content)
print(c.get("/boom/").json())
```
```output
spy sees /ok/ 200
200
spy sees /ok/ 403
b'blocked'
spy sees /boom/ 500
{'error': 'KeyError'}
```

**Пример 3. Сигнал `request_finished` и состояние.** Приёмник срабатывает после каждого ответа; данные запроса лежат в `request`, а не в объекте middleware.

```python run
import django
from django.conf import settings
from django.http import JsonResponse
from django.test import Client
from django.urls import path

settings.configure(
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    MIDDLEWARE=[__name__ + ".Counter"],
    USE_I18N=False,
)
django.setup()

from django.core.signals import request_finished
from django.dispatch import receiver

finished = []


class Counter:
    def __init__(self, get_response):
        self.get_response = get_response
        self.seen = 0  # один объект на процесс, общий для всех запросов

    def __call__(self, request):
        self.seen += 1
        request.number = self.seen
        return self.get_response(request)


@receiver(request_finished)
def done(sender, **kwargs):
    finished.append(1)


def view(request):
    return JsonResponse({"n": request.number})


urlpatterns = [path("", view)]
c = Client()
print([c.get("/").json()["n"] for _ in range(3)])
print(len(finished))
```
```output
[1, 2, 3]
3
```

<!-- section: mistakes -->
## Типичные ошибки

* **Не вызвать `get_response`** там, где нужно продолжить цепочку: запрос тихо оборвётся. Короткое замыкание допустимо только осознанно.
* **Хранить данные запроса в `self`.** Объект middleware общий для всех запросов, значит и для параллельных: данные «утекут» между запросами. Пишите в `request`.
* **Перепутать порядок.** Журнал доступа поставили внутрь слоя, меняющего статус, и он пишет не то, что получил клиент; `AuthenticationMiddleware` поставили раньше `SessionMiddleware` - `request.session` ещё нет.
* **Тяжёлая работа в каждом запросе:** обращение к базе или внешнему сервису в `__call__` замедляет все страницы. Фильтруйте по пути или делайте работу лениво.
* **Доверять входному идентификатору.** Заголовок вроде `X-Request-ID` приходит от клиента: проверьте формат и длину, иначе в журнал можно записать произвольную строку (подмену записей, переносы строк).
* **Путать сигнал и middleware.** Сигнал не может изменить ответ, а его приёмник выполняется синхронно в том же потоке; сообщение об ошибке внутри приёмника легко потерять.
* **Рассчитывать, что тестовый `Client` скроет исключение.** По умолчанию он повторно поднимает исключение представления; для проверки ответа `500` нужен `raise_request_exception=False`.

<!-- section: summary -->
## Коротко

* Middleware - вызываемые слои с фабрикой `get_response`; запрос идёт по списку `MIDDLEWARE` сверху вниз, ответ возвращается снизу вверх.
* Слой может изменить запрос, изменить ответ или вернуть ответ сам, не вызывая дальше.
* Исключения превращаются в ответы внутри слоёв; `process_exception` позволяет заменить ответ об ошибке.
* Данные запроса храните в `request`; объект middleware один на процесс.
* Сигналы (`request_finished` и др.) сообщают о событиях, выполняются синхронно и не меняют ответ; для изменения запроса и ответа служит middleware.
