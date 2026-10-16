<!-- section: simple -->
## Почему структура и тесты - одна тема

Пока приложение умещается в один файл, всё просто. Когда маршрутов несколько десятков, возникают два вопроса: **как разложить код** и **как проверять его без боли**. Они связаны: код, который удобно тестировать, обычно и хорошо устроен.

Три практических приёма:

* **Роутеры.** Маршруты группируются в `APIRouter` (товары, пользователи, служебные), у каждого свой `prefix` и `tags`. Приложение подключает их через `include_router`.
* **Фабрика приложения.** Вместо одного глобального `app` функция `create_app(settings)` собирает новое приложение с собственным состоянием. Тест создаёт свежее приложение под себя; два теста не мешают друг другу.
* **Настройки снаружи.** Всё, что меняется между окружениями (имя, лимиты, режим отладки), приходит из переменных окружения через небольшую функцию, превращающую их в типизированный неизменяемый объект.

А проверять такое приложение можно **в одном процессе, без сервера и сети**: `TestClient` вызывает ASGI-приложение напрямую (вы уже видели это в [первом уроке](lesson:fastapi.fundamentals.why-fastapi-and-asgi)). Тесты быстрые, детерминированные и запускаются где угодно.

<!-- section: precise -->
## Как это устроено

**APIRouter.** Роутер хранит список маршрутов и общие свойства: `prefix`, `tags`, `dependencies`, `responses`. Маршрут `@router.get("")` в роутере с `prefix="/items"` отвечает на `/items`, а `@router.get("/{item_id}")` - на `/items/{item_id}`. `app.include_router(router)` добавляет маршруты в приложение (при подключении можно задать дополнительный `prefix`, `tags` или `dependencies`, например `dependencies=[Depends(require_role("admin"))]` на целую группу, как в [уроке про авторизацию](lesson:fastapi.fundamentals.auth-basics)). Роутеры без собственного состояния можно безопасно подключать к любому числу приложений.

**Типичная раскладка.**

```
app/
  main.py        # create_app(): собирает приложение, подключает роутеры
  settings.py    # Settings, load_settings()
  deps.py        # общие зависимости: get_settings, get_store, get_current_user
  routers/
    items.py     # APIRouter(prefix="/items")
    meta.py
  schemas.py     # Pydantic-модели ввода и вывода
  services.py    # бизнес-логика без FastAPI (легко тестируется напрямую)
tests/
  test_items.py
```

Главное правило слоёв: обработчик тонкий (разобрать запрос, вызвать логику, вернуть ответ), логика не знает про HTTP и тестируется как обычные функции.

**Состояние приложения.** `app.state` - пространство имён для данных, которые живут столько же, сколько приложение. Зависимости получают доступ через `request.app.state`. Данные, созданные внутри `create_app`, принадлежат конкретному экземпляру. Глобальные словари на уровне модуля - общий ресурс всех экземпляров и всех тестов: они «протекают».

**Настройки.** `@dataclass(frozen=True)` даёт неизменяемый объект с типами и значениями по умолчанию. Функция `load_settings(env)` принимает `Mapping` (по умолчанию `os.environ`), приводит типы и **сразу падает с понятной ошибкой** на неверном значении, а не в середине первого запроса. Принимать `env` параметром важно для тестов: так не нужно менять глобальный `os.environ`. Для больших конфигураций (вложенные структуры, `.env`-файлы, секреты) есть пакет **pydantic-settings**; он здесь не установлен, поэтому используется простой вариант. Идея та же: типизированный объект настроек, создаваемый один раз и передаваемый зависимостью.

**Lifespan.** Асинхронный менеджер контекста, передаваемый в `FastAPI(lifespan=...)`: код до `yield` выполняется при запуске (открыть пул соединений, прогреть кэш), после - при остановке. Он заменяет устаревшие `@app.on_event("startup")` и `("shutdown")`; при указании `lifespan` старые обработчики игнорируются. Данные, нужные тестам и запросам независимо от lifespan (как `app.state.items`), лучше создавать в `create_app`, а не в lifespan: его запускают не все клиенты.

**TestClient.** Синхронный клиент: запрос выглядит как вызов обычной функции, исключения приложения по умолчанию пробрасываются в тест (`raise_server_exceptions=True`), а `raise_server_exceptions=False` позволяет проверить настоящий ответ `500`. Lifespan выполняется только в `with TestClient(app) as client:`. Внутри клиент запускает цикл событий в отдельном потоке, поэтому из асинхронных тестов его использовать неудобно.

**httpx.AsyncClient + ASGITransport.** Когда сам тест `async def` и должен делать `await` других вещей в том же цикле событий (асинхронная база, очередь), используют `httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://test")`. Это тоже без сети. Важная разница: `ASGITransport` **не запускает lifespan**. Если ваше приложение зависит от него, тест ведёт себя иначе, чем `TestClient` в `with`. В тестах без `async def` оставайтесь на `TestClient`: он проще. В pytest для async-тестов нужен плагин (anyio или pytest-asyncio); в упражнении сценарий запускается через `asyncio.run`.

**Изоляция тестов.**

* Новое приложение на тест (`create_app` в фикстуре) - состояние чистое.
* `app.dependency_overrides` очищается после теста (`try/finally` или фикстура с `yield`).
* Настройки передаются параметром, а не правкой `os.environ`.
* Время, случайность и внешние сервисы приходят зависимостями, которые тест подменяет (так в [уроке про авторизацию](lesson:fastapi.fundamentals.auth-basics) подменяется «текущее время»).
* Тест проверяет **публичный контракт** (статус, тело, заголовки), а не внутренности.

**Как выглядит тест в pytest** (pytest не требуется для упражнений, но это обычная форма в проектах):

```python
import pytest
from fastapi.testclient import TestClient

@pytest.fixture
def client():
    app = create_app(Settings(max_items=2))
    with TestClient(app) as c:      # lifespan выполнится
        yield c
    app.dependency_overrides.clear()

def test_limit(client):
    assert client.post("/items", json={"name": "a"}).status_code == 201
```

<!-- section: examples -->
## Примеры

**Пример 1. Роутеры: prefix, теги и итоговые пути.** Одна и та же группа маршрутов подключается с префиксом; в OpenAPI видны пути и теги.

```python run
from fastapi import APIRouter, FastAPI
from fastapi.testclient import TestClient

users = APIRouter(prefix="/users", tags=["users"])

@users.get("")
def list_users():
    return ["ann", "bob"]

@users.get("/{name}")
def get_user(name: str):
    return {"name": name}

app = FastAPI()
app.include_router(users)
app.include_router(users, prefix="/v2", tags=["v2"])

print(sorted(app.openapi()["paths"]))
print(app.openapi()["paths"]["/v2/users"]["get"]["tags"])
c = TestClient(app)
print(c.get("/users").json(), c.get("/v2/users/ann").json())
```
```output
['/users', '/users/{name}', '/v2/users', '/v2/users/{name}']
['v2', 'users']
['ann', 'bob'] {'name': 'ann'}
```

**Пример 2. Настройки: dataclass и чтение окружения.** Значения по умолчанию, приведение типов и понятная ошибка. Окружение передаётся параметром - `os.environ` не меняется.

```python run
from collections.abc import Mapping
from dataclasses import FrozenInstanceError, dataclass

@dataclass(frozen=True)
class Settings:
    name: str = "demo"
    workers: int = 1
    debug: bool = False

def load(env: Mapping[str, str]) -> Settings:
    d = Settings()
    try:
        workers = int(env.get("APP_WORKERS", d.workers))
    except ValueError:
        raise ValueError(f"APP_WORKERS must be an integer, got {env['APP_WORKERS']!r}") from None
    return Settings(env.get("APP_NAME", d.name), workers, env.get("APP_DEBUG", "").lower() in {"1", "true"})

print(load({}))
print(load({"APP_NAME": "shop", "APP_WORKERS": "4", "APP_DEBUG": "True"}))
try:
    load({"APP_WORKERS": "four"})
except ValueError as exc:
    print("ValueError:", exc)
try:
    load({}).workers = 5
except FrozenInstanceError:
    print("frozen")
```
```output
Settings(name='demo', workers=1, debug=False)
Settings(name='shop', workers=4, debug=True)
ValueError: APP_WORKERS must be an integer, got 'four'
frozen
```

**Пример 3. Фабрика и изоляция.** Два приложения из одной фабрики не делят данные, а глобальный счётчик «протекает» между ними.

```python run
from itertools import count
from fastapi import FastAPI
from fastapi.testclient import TestClient

GLOBAL_IDS = count(1)  # плохо: общий для всех приложений

def create_app() -> FastAPI:
    app = FastAPI()
    app.state.items = []           # хорошо: своё состояние

    @app.post("/items")
    def add(name: str):
        app.state.items.append(name)
        return {"global_id": next(GLOBAL_IDS), "count": len(app.state.items)}

    return app

a, b = create_app(), create_app()
ca, cb = TestClient(a), TestClient(b)
print(ca.post("/items", params={"name": "x"}).json())
print(cb.post("/items", params={"name": "y"}).json())
print(a.state.items, b.state.items)
```
```output
{'global_id': 1, 'count': 1}
{'global_id': 2, 'count': 1}
['x'] ['y']
```

**Пример 4. Lifespan: когда он выполняется.** Без `with` старт не вызывается. Внутри `with` - вызывается, при выходе - останавливается.

```python run
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.testclient import TestClient

events = []

@asynccontextmanager
async def lifespan(app: FastAPI):
    events.append("startup")
    yield
    events.append("shutdown")

app = FastAPI(lifespan=lifespan)

@app.get("/ping")
def ping():
    return "pong"

c = TestClient(app)
c.get("/ping")
print("без with:", events)
with TestClient(app) as c2:
    c2.get("/ping")
    print("внутри with:", events)
print("после with:", events)
```
```output
без with: []
внутри with: ['startup']
после with: ['startup', 'shutdown']
```

**Пример 5. Тот же запрос через TestClient и httpx.AsyncClient.** Результат одинаков; клиент асинхронный и выполняется внутри `asyncio.run`. Lifespan через `ASGITransport` не запускается.

```python run
import asyncio
from contextlib import asynccontextmanager
import httpx
from fastapi import FastAPI
from fastapi.testclient import TestClient

started = []

@asynccontextmanager
async def lifespan(app: FastAPI):
    started.append(True)
    yield

app = FastAPI(lifespan=lifespan)

@app.get("/double/{n}")
async def double(n: int):
    return {"result": n * 2}

print(TestClient(app).get("/double/21").json())

async def main():
    async with httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://test") as ac:
        r = await ac.get("/double/21")
        return r.json()

print(asyncio.run(main()), "lifespan запущен:", bool(started))
```
```output
{'result': 42}
{'result': 42} lifespan запущен: False
```

<!-- section: mistakes -->
## Типичные ошибки

* **Глобальное состояние в модуле.** Данные, созданные при импорте, переживают тест; результат зависит от порядка запуска. Создавайте состояние внутри `create_app`.
* **Тест без `with`, которому нужен lifespan.** Старт не выполнится, поля `app.state` не будут созданы.
* **Ожидание lifespan от `ASGITransport`.** Он его не запускает.
* **Неочищенный `dependency_overrides`.** Подмена из одного теста ломает другой.
* **Правка `os.environ` в тестах.** Глобальное изменение остаётся для всех; передавайте окружение параметром.
* **Настройки читаются прямо в обработчике.** Тяжело тестировать и менять; настройки приходят зависимостью.
* **Циклические импорты между роутерами и `main.py`.** Роутеры не должны импортировать приложение; общие вещи вынесите в `deps.py`.
* **Бизнес-логика внутри обработчиков.** Её тестируют только через HTTP; выносите в обычные функции.
* **Использовать `@app.on_event`.** Он устарел, применяйте `lifespan`.
* **Пустой путь и слеши.** `@router.get("/")` в роутере с prefix даёт `/items/` и редирект с `/items`; для самого prefix используйте `""`.
* **Один `TestClient` на модуль с общим состоянием.** Тесты зависят друг от друга; создавайте клиент на тест (дёшево).

<!-- section: summary -->
## Итоги

* `APIRouter(prefix, tags)` группирует маршруты; `include_router` подключает их; пустой путь `""` означает сам prefix.
* `create_app(settings)` - фабрика: каждое приложение со своим `app.state`; глобальных изменяемых данных нет.
* Настройки - неизменяемый dataclass, `load_settings(env)` приводит типы и падает с понятной ошибкой; pydantic-settings вырастает из той же идеи, но здесь не установлен.
* `TestClient` - синхронный клиент без сети; lifespan только в `with`. `httpx.AsyncClient(ASGITransport)` - для async-тестов; lifespan не запускает.
* Изоляция: свежее приложение на тест, очистка `dependency_overrides`, параметры вместо правки окружения, проверка публичного контракта.
* `lifespan` заменил `on_event`.
