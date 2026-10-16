<!-- section: simple -->
## Что такое веб-API и зачем нужен FastAPI

Веб-API - это программа, которая слушает HTTP-запросы («дай мне пользователя 7», «создай заказ») и отвечает данными, обычно в формате JSON. Писать такую программу на «голом» сокете никто не хочет: нужно разбирать байты запроса, находить нужную функцию по пути, проверять, что пришёл `int`, а не строка, превращать результат в JSON и не забывать про коды ответа.

**FastAPI** берёт эту рутину на себя, а вам оставляет бизнес-логику. Вы пишете обычную функцию с аннотациями типов, а фреймворк:

* по декоратору `@app.get("/items/{item_id}")` понимает, на какой путь и метод она отвечает;
* по аннотации `item_id: int` приводит и проверяет значение, а при ошибке сам отвечает кодом `422` с описанием проблемы;
* из того же описания строит интерактивную документацию (`/docs`) и машиночитаемую схему OpenAPI (`/openapi.json`).

Под капотом работает **ASGI** - договорённость между веб-сервером (например, uvicorn) и приложением. Сервер принимает соединение, а приложение - это просто асинхронная функция, которой сервер передаёт «описание соединения» и два канала: откуда читать события клиента и куда писать ответ. FastAPI построен на лёгком ASGI-фреймворке **Starlette** (маршрутизация, middleware, ответы) и библиотеке **Pydantic** (валидация данных).

В этом курсе вы не будете запускать сервер: приложение проверяется в процессе через `TestClient`, который играет роль и клиента, и сервера. Так тесты быстрые, не требуют сети и ведут себя одинаково везде.

Предварительно полезно вспомнить [декораторы](lesson:python.advanced.decorators) (`@app.get` - это декоратор, который регистрирует функцию, а не оборачивает её) и [аннотации типов](lesson:python.advanced.typing-fundamentals): в FastAPI они - не комментарии, а исполняемая спецификация.

<!-- section: precise -->
## Как это устроено

**WSGI и ASGI.** Классический интерфейс WSGI (PEP 3333) - синхронная функция `app(environ, start_response)`, которая возвращает итерируемое тело. Один запрос занимает один поток от начала до конца, а соединение «запрос-ответ» короткое. Для WebSocket, долгих потоков и `async`-кода это неудобно, поэтому появился **ASGI**: приложение - это вызываемый объект

```python
async def app(scope, receive, send): ...
```

* `scope` - словарь с описанием соединения: `type` (`"http"`, `"websocket"` или `"lifespan"`), `method`, `path`, `headers` (список пар в байтах), `query_string`.
* `receive` - корутина; `await receive()` возвращает следующее событие от клиента (для HTTP - `{"type": "http.request", "body": b"...", "more_body": False}`).
* `send` - корутина для отправки событий: сначала `{"type": "http.response.start", "status": 200, "headers": [...]}`, затем одно или несколько `{"type": "http.response.body", "body": b"..."}`.

Сервер (uvicorn, hypercorn, granian) вызывает приложение на каждое соединение. Middleware в ASGI - это просто приложение, которое оборачивает другое приложение и вызывает его с теми же `scope/receive/send`.

**Жизненный цикл запроса в FastAPI.**

1. Сервер вызывает `app(scope, receive, send)`. Объект `FastAPI` - наследник `Starlette`, его `__call__` строит стек middleware и роутер.
2. Роутер сопоставляет `scope["path"]` и метод с зарегистрированными маршрутами. Нет пути - ответ `404`; путь есть, метода нет - `405` с заголовком `Allow`.
3. Для найденного маршрута FastAPI **заранее** (при регистрации) разобрал сигнатуру функции: какие параметры из пути, запроса, заголовков, тела; какие зависимости нужно вызвать. Теперь он читает тело, собирает значения, прогоняет их через Pydantic. При ошибке - ответ `422`.
4. Вызывается ваша функция. `def` выполняется в пуле потоков (чтобы блокирующий код не останавливал цикл событий), `async def` - прямо в цикле событий.
5. Возвращаемое значение сериализуется в JSON (через `response_model`, если он задан - [следующий урок](lesson:fastapi.fundamentals.path-query-body)) и отправляется через `send`.

**OpenAPI из аннотаций.** При регистрации маршрута FastAPI строит модель параметров из сигнатуры. `app.openapi()` обходит маршруты и собирает схему: пути, параметры, схемы тел, коды ответов. Схема не пишется руками, поэтому не расходится с кодом: изменили аннотацию - изменилась документация и поведение валидации.

**def против async def.** Выбор определяется тем, что внутри. Если вы вызываете `await`-совместимые библиотеки (асинхронный HTTP-клиент, асинхронный драйвер БД) - `async def`. Если блокирующие (обычный драйвер, `requests`, чтение файла, тяжёлые вычисления) - обычный `def`, чтобы FastAPI отдал вызов пулу потоков. Блокирующий вызов внутри `async def` останавливает цикл событий целиком: пока он выполняется, процесс не обслуживает другие запросы.

**Коды статуса.** `200` - успех с телом; `201 Created` - ресурс создан; `204 No Content` - успех без тела (тело в ответе 204 недопустимо); `404` - ресурса нет; `405` - метод не поддерживается; `422` - данные запроса не прошли валидацию. Код по умолчанию - `200`; меняется параметром `status_code=` в декораторе.

**Что когда не использовать.** FastAPI хорош для JSON-API с чёткими схемами. Если вам нужны серверный рендеринг страниц, админка «из коробки» и ORM с миграциями, полнофункциональный фреймворк вроде Django сэкономит время. Для сверхпростой задачи достаточно Starlette. FastAPI не делает ваш код быстрым сам по себе: узкое место обычно в базе данных и сети.

**Тестирование.** `TestClient(app)` вызывает приложение прямо в процессе, без сети; запросы выглядят как у библиотеки httpx (`client.get(...)`, `client.post(..., json=...)`). Обработчики запросов при этом выполняются в отдельном потоке с event loop, поэтому поведение близко к боевому.

**Продакшен.** Приложение запускают ASGI-сервером (например, `uvicorn module:app`), как правило, за обратным прокси. Встроенный `/docs` в публичном API часто отключают или закрывают (`FastAPI(docs_url=None)`). Блокирующий код в `async def` - одна из самых частых причин «подвисающего» сервиса.

<!-- section: examples -->
## Примеры

**Пример 1. ASGI без фреймворка.** Приложение - обычная корутина. Мы вызываем её вручную, подставляя свои `receive` и `send`, и смотрим, какие события она отправила.

```python run
import asyncio

async def app(scope, receive, send):
    assert scope["type"] == "http"
    await send({"type": "http.response.start", "status": 200,
                "headers": [(b"content-type", b"text/plain; charset=utf-8")]})
    body = f"{scope['method']} {scope['path']}".encode()
    await send({"type": "http.response.body", "body": body})

async def main():
    sent = []
    async def receive():
        return {"type": "http.request", "body": b"", "more_body": False}
    async def send(message):
        sent.append(message)
    scope = {"type": "http", "method": "GET", "path": "/hello", "headers": [], "query_string": b""}
    await app(scope, receive, send)
    for message in sent:
        print(message)

asyncio.run(main())
```
```output
{'type': 'http.response.start', 'status': 200, 'headers': [(b'content-type', b'text/plain; charset=utf-8')]}
{'type': 'http.response.body', 'body': b'GET /hello'}
```

**Пример 2. Первое приложение FastAPI и TestClient.** Обратите внимание на коды статуса, в том числе на те, которые мы не писали: `404` и `405` генерирует роутер.

```python run
from fastapi import FastAPI
from fastapi.testclient import TestClient

app = FastAPI()

@app.get("/ping")
def ping():
    return {"pong": True}

@app.post("/items", status_code=201)
def create_item():
    return {"id": 1}

client = TestClient(app)
r = client.get("/ping")
print(r.status_code, r.json(), r.headers["content-type"])
r = client.post("/items")
print(r.status_code, r.json())
r = client.get("/missing")
print(r.status_code, r.json())
r = client.post("/ping")
print(r.status_code, r.json(), r.headers["allow"])
```
```output
200 {'pong': True} application/json
201 {'id': 1}
404 {'detail': 'Not Found'}
405 {'detail': 'Method Not Allowed'} GET
```

**Пример 3. Аннотации превращаются в проверку и в документацию.**

```python run
from fastapi import FastAPI
from fastapi.testclient import TestClient

app = FastAPI(title="Demo")

@app.get("/items/{item_id}")
def read_item(item_id: int, verbose: bool = False):
    return {"item_id": item_id, "verbose": verbose}

client = TestClient(app)
print(client.get("/items/5?verbose=true").json())
r = client.get("/items/abc")
print(r.status_code, r.json()["detail"][0]["loc"], r.json()["detail"][0]["type"])

params = app.openapi()["paths"]["/items/{item_id}"]["get"]["parameters"]
for p in params:
    print(p["name"], p["in"], p["required"], p["schema"]["type"])
```
```output
{'item_id': 5, 'verbose': True}
422 ['path', 'item_id'] int_parsing
item_id path True integer
verbose query False boolean
```

**Пример 4. Где выполняются def и async def.** В `TestClient` приложение работает в потоке с собственным event loop. Обработчик `async def` выполняется прямо в нём, а `def` - в потоке из пула.

```python run
import threading
from fastapi import FastAPI
from fastapi.testclient import TestClient

app = FastAPI()
seen = {}

@app.get("/sync")
def sync_handler():
    seen["sync"] = threading.get_ident()
    return {}

@app.get("/async")
async def async_handler():
    seen["async"] = threading.get_ident()
    return {}

client = TestClient(app)
client.get("/sync")
client.get("/async")
print("разные потоки:", seen["sync"] != seen["async"])
print("async не в главном потоке теста:", seen["async"] != threading.get_ident())
```
```output
разные потоки: True
async не в главном потоке теста: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Блокирующий вызов в `async def`.** `time.sleep(1)`, `requests.get(...)`, синхронный драйвер БД внутри `async def` останавливают весь цикл событий. Сделайте обработчик обычным `def` или используйте асинхронную библиотеку.
* **Тело в ответе `204`.** Для `204` пишите `status_code=204` и ничего не возвращайте. Возврат словаря с кодом 204 бессмыслен: в проверенной версии FastAPI тело отбрасывается, но код выглядит так, будто данные отправляются.
* **Забытый `status_code=201`.** Создание ресурса, возвращающее `200`, технически работает, но клиенты и тесты ожидают `201`.
* **Имя параметра пути не совпадает с аргументом функции.** `@app.get("/items/{id}")` и `def f(item_id)`: `item_id` будет считаться параметром запроса, а `id` в функции не найдётся.
* **Запуск сервера внутри модуля.** `uvicorn.run(...)` на верхнем уровне модуля мешает импорту в тестах. Запуск - отдельной командой или под `if __name__ == "__main__":`.
* **Глобальное состояние как «база данных».** Словарь на уровне модуля подходит для учебных примеров, но в реальном сервисе с несколькими процессами у каждого процесса будет своя копия. Состояние хранят во внешнем хранилище.
* **Вера в «async быстрее».** Выигрыш бывает только на ожидании ввода-вывода; для вычислений нужна другая модель (процессы, очередь задач).

<!-- section: summary -->
## Итоги

* ASGI-приложение - `async def app(scope, receive, send)`; сервер вызывает его на соединение, middleware оборачивает приложение в приложение.
* FastAPI = Starlette (маршрутизация, middleware, ответы) + Pydantic (валидация) + разбор сигнатур, зависимости и OpenAPI.
* Параметры, их типы и коды ответов описываются аннотациями и декоратором; `404`, `405`, `422` отвечает сам фреймворк.
* `def` - в пуле потоков, `async def` - в цикле событий; блокирующий код в `async def` недопустим.
* Тесты - через `TestClient`, в процессе, без сети.
