<!-- section: simple -->
## Ошибка - тоже часть контракта

Клиент вашего API (мобильное приложение, другой сервис, фронтенд) не видит ваш код, он видит только **код статуса и тело ответа**. Если при ошибке одни эндпоинты отвечают `{"detail": "..."}`, другие - `{"error": "..."}`, а третьи - HTML-страницей с трассировкой, клиенту приходится писать код для каждого случая. Поэтому ошибки проектируют так же внимательно, как успешные ответы.

Два вопроса, которые нужно задать себе:

1. **Чья это ошибка?** Если клиент прислал неверные данные, не имеет прав или просит несуществующее - это **4xx**. Если сломался сервер (баг, недоступная база, необработанное исключение) - это **5xx**. Граница важна: клиент на 4xx должен *исправить запрос*, а на 5xx может *повторить позже*; мониторинг тревожится именно по 5xx.
2. **Что именно рассказать?** Клиенту нужен стабильный машинный код ошибки (`out_of_stock`) и понятное сообщение. Внутренние детали (текст исключения, SQL, пути файлов) - только в лог.

В FastAPI для этого есть исключения (`HTTPException` и свои классы) и **обработчики исключений**: функции, которые превращают исключение в ответ. Один раз описав формат в обработчиках, вы пишете в обработчиках маршрутов просто `raise OutOfStock(...)`.

<!-- section: precise -->
## Как это устроено

**Коды статуса, которые вы будете использовать чаще всего.**

* `400 Bad Request` - запрос нельзя обработать по причине, не покрытой более точным кодом; `422` - данные не прошли валидацию (его возвращает FastAPI);
* `401 Unauthorized` - нет или неверные учётные данные (с заголовком `WWW-Authenticate`); `403 Forbidden` - личность известна, но прав нет;
* `404 Not Found` - ресурса нет; `405 Method Not Allowed` - путь есть, метода нет (заголовок `Allow`); `409 Conflict` - конфликт с текущим состоянием (дубликат, нехватка остатка); `429 Too Many Requests` - ограничение частоты;
* `500 Internal Server Error` - непредвиденная ошибка сервера; `503 Service Unavailable` - зависимость недоступна.

Для 404 безопаснее, чем 403, когда существование ресурса само по себе секрет (чужой заказ): иначе вы подтверждаете, что он существует.

**HTTPException.** `raise HTTPException(status_code=404, detail="...", headers={...})` прерывает обработку и превращается в ответ `{"detail": ...}` (в `detail` можно положить любую JSON-совместимую структуру). Исключение выбрасывается из обработчика, зависимости или вложенного вызова - стек разворачивается, зависимости с `yield` получают исключение.

**Свои исключения.** Исключение предметной области (`OutOfStock`) не знает про HTTP и может быть выброшено из сервисного кода; один обработчик сопоставляет его коду статуса. Это разделяет ответственность: бизнес-логику можно тестировать без FastAPI, а формат ответов меняется в одном месте. Подклассы одного базового `AppError` с атрибутами `status_code`, `code` избавляют от дублирования.

**Обработчики исключений.** `@app.exception_handler(ExcClass)` регистрирует функцию `(request, exc) -> Response`. Выбирается самый специфичный подходящий класс (по иерархии наследования). Важные классы:

* `starlette.exceptions.HTTPException` - перехватывает и ваши `HTTPException`, и 404/405, которые создаёт роутер. `fastapi.HTTPException` наследуется от него, но обработчик для подкласса *не* увидит ошибок роутера. Не забывайте передавать `headers=exc.headers`, иначе потеряются `Allow` и `WWW-Authenticate`.
* `fastapi.exceptions.RequestValidationError` - ошибки валидации запроса. `exc.errors()` - список словарей Pydantic (`type`, `loc`, `msg`, `input`, `ctx`). В ответе клиенту не повторяйте `input` целиком: он может содержать пароли.
* `Exception` - «последний рубеж». Обработчик вызывается Starlette в `ServerErrorMiddleware`, **после ответа исключение всё равно пробрасывается** (чтобы сервер записал его в лог). Поэтому `TestClient(app)` по умолчанию поднимает такое исключение прямо в тесте; для проверки самого ответа создавайте клиента с `raise_server_exceptions=False`.

**Единый формат.** Выберите схему один раз: например, `{"error": {"code": "...", "message": "...", "details": [...]}}` или формат RFC 9457 (`application/problem+json`). Главное - **один** формат для всех ошибок, включая 404/405 роутера и 422 валидации; для этого и нужны перечисленные обработчики. Хорошая практика - стабильный `code` для программ, `message` для людей, `details` для полей.

**Ошибки валидации.** По умолчанию тело `422` содержит `detail` со списком. Если контракт требует другой формат, переопределите обработчик `RequestValidationError`. Не меняйте код статуса без нужды: клиенты и инструменты ожидают `422` (в `fastapi.status` есть `HTTP_422_UNPROCESSABLE_CONTENT` и прежнее имя `HTTP_422_UNPROCESSABLE_ENTITY`).

**ResponseValidationError.** Если ответ не соответствует `response_model` (см. [параметры и модели](lesson:fastapi.fundamentals.path-query-body)), это баг сервера: клиент получит `500`, а в логе будет исключение. Формат `500` определяется обработчиком `Exception`.

**Когда не использовать исключения.** Ожидаемые «ошибки» внутри бизнес-кода (например, «пользователь не найден» при проверке существования) удобнее возвращать как значения (`None`, `Result`) и превращать в ответ на границе; исключения нужны там, где нужно прервать глубокий стек вызовов.

**Идемпотентность и повторы.** Клиент может безопасно повторять `GET`, `PUT`, `DELETE` при `5xx` и сетевых сбоях; повтор `POST` без ключа идемпотентности создаст дубликат. Для создания ресурсов в ненадёжных сетях принимают `Idempotency-Key`.

**Тестирование.** Тестируйте каждую ветку ошибки: статус, код, форму тела и отсутствие утечек. Отдельно проверьте 404/405 (чужие маршруты), 422 с несколькими ошибками и 500 (`raise_server_exceptions=False`, текст исключения отсутствует в теле).

**Продакшен.** Логируйте исключения с идентификатором запроса и возвращайте этот идентификатор клиенту (в теле или заголовке), чтобы поддержка могла найти запись. Не логируйте тело запроса целиком (пароли, токены). Различайте уровни: 4xx - `info`/`warning`, 5xx - `error`.

<!-- section: examples -->
## Примеры

**Пример 1. HTTPException: код, `detail` и заголовки.**

```python run
from fastapi import FastAPI, HTTPException
from fastapi.testclient import TestClient

app = FastAPI()

@app.get("/secure")
def secure():
    raise HTTPException(status_code=401, detail="token required", headers={"WWW-Authenticate": "Bearer"})

@app.get("/rich")
def rich():
    raise HTTPException(status_code=409, detail={"code": "duplicate", "field": "email"})

c = TestClient(app)
r = c.get("/secure")
print(r.status_code, r.json(), r.headers["www-authenticate"])
r = c.get("/rich")
print(r.status_code, r.json())
```
```output
401 {'detail': 'token required'} Bearer
409 {'detail': {'code': 'duplicate', 'field': 'email'}}
```

**Пример 2. Своё исключение и единый формат, включая ошибки роутера.**

```python run
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from fastapi.testclient import TestClient
from starlette.exceptions import HTTPException as StarletteHTTPException

app = FastAPI()

class AppError(Exception):
    status_code, code = 400, "app_error"
    def __init__(self, message):
        self.message = message

class OutOfStock(AppError):
    status_code, code = 409, "out_of_stock"

def body(code, message):
    return {"error": {"code": code, "message": message}}

@app.exception_handler(AppError)
async def on_app_error(request: Request, exc: AppError):
    return JSONResponse(status_code=exc.status_code, content=body(exc.code, exc.message))

@app.exception_handler(StarletteHTTPException)
async def on_http_error(request: Request, exc: StarletteHTTPException):
    return JSONResponse(status_code=exc.status_code, content=body(f"http_{exc.status_code}", str(exc.detail)), headers=exc.headers)

@app.get("/buy")
def buy():
    raise OutOfStock("only 0 left")

c = TestClient(app)
for r in (c.get("/buy"), c.get("/missing"), c.post("/buy")):
    print(r.status_code, r.json(), r.headers.get("allow"))
```
```output
409 {'error': {'code': 'out_of_stock', 'message': 'only 0 left'}} None
404 {'error': {'code': 'http_404', 'message': 'Not Found'}} None
405 {'error': {'code': 'http_405', 'message': 'Method Not Allowed'}} GET
```

**Пример 3. Свой формат ошибок валидации.**

```python run
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from fastapi.testclient import TestClient

app = FastAPI()

@app.exception_handler(RequestValidationError)
async def on_validation(request: Request, exc: RequestValidationError):
    details = [{"field": ".".join(map(str, e["loc"])), "message": e["msg"]} for e in exc.errors()]
    return JSONResponse(status_code=422, content={"error": {"code": "validation_error", "details": details}})

@app.get("/page")
def page(n: int, size: int = 10):
    return {"n": n, "size": size}

c = TestClient(app)
r = c.get("/page?size=x")
print(r.status_code)
for d in r.json()["error"]["details"]:
    print(d)
```
```output
422
{'field': 'query.n', 'message': 'Field required'}
{'field': 'query.size', 'message': 'Input should be a valid integer, unable to parse string as an integer'}
```

**Пример 4. 500: обработчик Exception и поведение TestClient.**

```python run
from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from fastapi.testclient import TestClient

app = FastAPI()

@app.exception_handler(Exception)
async def on_unhandled(request: Request, exc: Exception):
    return JSONResponse(status_code=500, content={"error": {"code": "internal_error"}})

@app.get("/boom")
def boom():
    raise RuntimeError("db password is hunter2")

strict = TestClient(app)
try:
    strict.get("/boom")
except RuntimeError as exc:
    print("тест получил исключение:", exc)

lenient = TestClient(app, raise_server_exceptions=False)
r = lenient.get("/boom")
print(r.status_code, r.json(), "hunter2" in r.text)
```
```output
тест получил исключение: db password is hunter2
500 {'error': {'code': 'internal_error'}} False
```

<!-- section: mistakes -->
## Типичные ошибки

* **500 вместо 4xx.** Необработанное `KeyError` на неверный ключ клиента - это 500. Ожидаемые отказы превращайте в 4xx.
* **200 с телом `{"error": ...}`.** Ошибка с кодом 200 ломает кэши, мониторинг и клиентские библиотеки.
* **Разные форматы ошибок.** Часть эндпоинтов отвечает `detail`, часть - `error`. Зарегистрируйте обработчики один раз.
* **Обработчик только для `fastapi.HTTPException`.** 404/405 роутера останутся в старом формате. Нужен базовый `starlette.exceptions.HTTPException`.
* **Потерянные заголовки.** Обработчик без `headers=exc.headers` скрывает `WWW-Authenticate` и `Allow`.
* **Утечка деталей.** `content={"error": str(exc)}` отправляет клиенту внутренний текст исключения.
* **Путаница 401 и 403.** 401 - «кто вы?», 403 - «вам нельзя».
* **Проглоченные исключения.** `except Exception: pass` скрывает баг; логируйте и пробрасывайте.
* **Тесты 500 без `raise_server_exceptions=False`.** Тест падает исключением, хотя обработчик настроен правильно.
* **Повтор `POST` при 5xx без идемпотентности.** Приводит к дублям.

<!-- section: summary -->
## Итоги

* 4xx - ошибка клиента (исправить запрос), 5xx - ошибка сервера (повторить позже/чинить сервис); выбирайте код по смыслу.
* `HTTPException` для простых случаев, свои исключения предметной области плюс обработчики - для единого формата.
* Для перехвата 404/405 роутера регистрируйте обработчик на `starlette.exceptions.HTTPException` и сохраняйте `headers`.
* `RequestValidationError` можно переоформить, не меняя код `422`; `Exception`-обработчик закрывает 500, но исключение всё равно пробрасывается серверу.
* Не раскрывайте внутренние детали; логируйте подробности с идентификатором запроса; тестируйте все ветки ошибок.
