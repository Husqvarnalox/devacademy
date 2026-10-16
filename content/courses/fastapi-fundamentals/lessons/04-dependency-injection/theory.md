<!-- section: simple -->
## Зависимость - это «то, что нужно обработчику»

Почти каждому обработчику нужно что-то кроме данных запроса: подключение к базе, текущий пользователь, настройки, пагинация, клиент внешнего сервиса. Можно создавать всё это внутри функции, но тогда:

* один и тот же код повторяется в десятках обработчиков;
* обработчик трудно тестировать: в тесте нельзя подставить заглушку, он всегда обращается к настоящей базе;
* очистка ресурсов (закрыть соединение) легко забывается.

FastAPI решает это **внедрением зависимостей**. Вы объявляете параметр `repo: Annotated[TodoRepo, Depends(get_repo)]`, и фреймворк перед вызовом обработчика сам вызывает `get_repo()` и передаёт результат. Зависимость - обычная функция (или класс, или генератор), у которой могут быть собственные параметры запроса и собственные зависимости.

Три главных выигрыша: **переиспользование** (одна зависимость - много обработчиков), **очистка** (зависимость с `yield` закрывает ресурс после запроса) и **подмена в тестах** (`app.dependency_overrides[get_repo] = fake`). Внутри это знакомые вам по [декораторам](lesson:python.advanced.decorators) и [контекстным менеджерам](lesson:python.advanced.context-managers) идеи: функция как значение и «открыть - использовать - закрыть».

<!-- section: precise -->
## Как это устроено

**Построение дерева.** При регистрации маршрута FastAPI анализирует сигнатуру и находит всё, что обёрнуто в `Depends(...)` (в `Annotated`, в значениях по умолчанию, в параметре `dependencies=[...]` декоратора или роутера). Для каждой зависимости рекурсивно разбирается её собственная сигнатура. Получается дерево: корень - обработчик, узлы - зависимости, листья - зависимости без своих зависимостей. Параметры запроса любой зависимости попадают в OpenAPI маршрута; сама зависимость как параметр в схеме не видна.

**Разрешение на запросе.** Для каждого запроса FastAPI обходит дерево от листьев: вызывает зависимость, получает значение, подставляет в зависимости вышележащих узлов, наконец, передаёт всё обработчику. Если зависимость выбрасывает исключение (`HTTPException`), обработчик не вызывается. Параметры запроса зависимостей валидируются так же, как параметры обработчика, и ошибки собираются в общий `422`.

**Кэш в пределах запроса.** Результат зависимости запоминается по ключу «вызываемый объект + параметры безопасности». Если одна и та же функция встречается в дереве несколько раз, она выполняется **один раз** за запрос, а значение переиспользуется. `Depends(dep, use_cache=False)` вызывает её заново при каждом объявлении. Кэш живёт только внутри запроса: между запросами ничего не разделяется (зависимость - не синглтон).

**Что может быть зависимостью.** Любой вызываемый объект: функция (`def` или `async def`), класс (его `__init__` разбирается как зависимость - удобно для пагинации), экземпляр с `__call__`. `Depends()` без аргументов для параметра, аннотированного классом, использует сам класс. Обычные `def`-зависимости выполняются в пуле потоков, `async def` - в цикле событий, как и обработчики.

**Annotated-стиль.** `Repo = Annotated[TodoRepo, Depends(get_repo)]` - алиас, который переиспользуется: `def f(repo: Repo)`. Аннотация остаётся честным типом (`TodoRepo`), статический анализатор видит нужный тип, а `Depends` не дублируется в каждой сигнатуре. Старый стиль `repo: TodoRepo = Depends(get_repo)` работает, но в документации предпочтителен `Annotated`.

**Зависимости с `yield`.** Функция-генератор выполняется в два этапа: код до `yield` запускается перед обработчиком, значение после `yield` передаётся обработчику, а код после - когда обработчик завершится. Если обработчик (или зависимость ниже по дереву) выбросил исключение, оно **выбрасывается внутрь генератора в точке `yield`**: `try/except/finally` внутри зависимости видит ошибку. Так пишут «сессию с откатом»: `try: yield tx; except Exception: tx.rollback(); raise; else: tx.commit()`. Важная деталь: в разных версиях FastAPI менялось, когда именно выполняется код после `yield` относительно отправки ответа клиенту. В установленной версии для этого есть параметр `Depends(..., scope="function" | "request")`: `"function"` завершает зависимость до отправки ответа, `"request"` - после. Если от этого зависит корректность (например, фиксируется транзакция до ответа клиенту), задавайте `scope` явно и сверяйтесь с документацией вашей версии.

**Подмена в тестах.** `app.dependency_overrides` - обычный словарь: ключ - **оригинальная** функция из `Depends`, значение - замена. Замена сама является зависимостью: её параметры разбираются из запроса. Словарь общий для приложения, поэтому подмену очищают после теста (`app.dependency_overrides.clear()` в `finally` или в фикстуре), иначе она «протечёт» в соседние тесты. Подменять можно и зависимости безопасности (тестовый пользователь).

**Зависимости на уровне роутера и приложения.** `APIRouter(dependencies=[Depends(auth)])` или `FastAPI(dependencies=[...])` применяют зависимость ко всем маршрутам без добавления параметра в каждую функцию; значение при этом не передаётся в обработчик. Это подходит для проверок («только администратор»).

**Когда не использовать.** Не превращайте в зависимости чистые функции без внешних эффектов: `slugify(text)` проще вызвать напрямую. Не прячьте в зависимостях бизнес-логику: она должна оставаться в сервисных функциях, которые можно вызвать и без HTTP. Не создавайте дорогие ресурсы (пул соединений, ML-модель) в зависимости на каждый запрос: их создают один раз на процесс (модуль или `lifespan`), а зависимость только возвращает готовый объект.

**Ресурсы процесса и `lifespan`.** Код, который должен выполняться один раз при запуске и остановке приложения, оформляется как `lifespan` - асинхронный контекстный менеджер, передаваемый в `FastAPI(lifespan=...)`. Старый декоратор `@app.on_event("startup")` объявлен устаревшим и в установленной версии выдаёт `DeprecationWarning`.

**Продакшен.** Размер пула соединений и таймауты задаются на уровне процесса; зависимость на запрос берёт соединение из пула и возвращает его в `finally`. Зависимости с `yield` не должны подавлять исключения без причины: если вы перехватили ошибку и не пробросили её, клиент получит успешный ответ вместо ошибки.

<!-- section: examples -->
## Примеры

**Пример 1. Дерево зависимостей и кэш в пределах запроса.** `get_config` объявлена трижды (через `get_pricing`, напрямую и с `use_cache=False`), но вызывается дважды: три объявления с кэшем дают один вызов, ещё один - принудительный.

```python run
from typing import Annotated
from fastapi import Depends, FastAPI
from fastapi.testclient import TestClient

app = FastAPI()
calls = []

def get_config():
    calls.append("config")
    return {"tax": 0.2}

def get_pricing(cfg: Annotated[dict, Depends(get_config)]):
    return lambda net: round(net * (1 + cfg["tax"]), 2)

@app.get("/price")
def price(
    net: float,
    pricing: Annotated[object, Depends(get_pricing)],
    cfg: Annotated[dict, Depends(get_config)],
    fresh: Annotated[dict, Depends(get_config, use_cache=False)],
):
    return {"gross": pricing(net)}

c = TestClient(app)
print(c.get("/price?net=10").json(), calls)
calls.clear()
print(c.get("/price?net=20").json(), calls)
print([p["name"] for p in app.openapi()["paths"]["/price"]["get"]["parameters"]])
```
```output
{'gross': 12.0} ['config', 'config']
{'gross': 24.0} ['config', 'config']
['net']
```

**Пример 2. Класс как зависимость и Annotated-алиас.**

```python run
from typing import Annotated
from fastapi import Depends, FastAPI, Query
from fastapi.testclient import TestClient

class Pagination:
    def __init__(self, limit: Annotated[int, Query(ge=1, le=50)] = 10, offset: Annotated[int, Query(ge=0)] = 0):
        self.limit, self.offset = limit, offset

Page = Annotated[Pagination, Depends()]
app = FastAPI()
DATA = list(range(100))

@app.get("/numbers")
def numbers(page: Page):
    return DATA[page.offset : page.offset + page.limit]

@app.get("/other")
def other(page: Page):
    return {"limit": page.limit}

c = TestClient(app)
print(c.get("/numbers?limit=3&offset=5").json())
print(c.get("/other").json())
r = c.get("/numbers?limit=500")
print(r.status_code, r.json()["detail"][0]["loc"])
```
```output
[5, 6, 7]
{'limit': 10}
422 ['query', 'limit']
```

**Пример 3. Зависимость с yield: порядок и ошибка в обработчике.**

```python run
from typing import Annotated
from fastapi import Depends, FastAPI, HTTPException
from fastapi.testclient import TestClient

app = FastAPI()
log = []

def session():
    log.append("open")
    try:
        yield "S"
    except HTTPException as exc:
        log.append(f"error {exc.status_code}")
        raise
    finally:
        log.append("close")

@app.get("/ok")
def ok(s: Annotated[str, Depends(session)]):
    log.append("handler")
    return {"s": s}

@app.get("/fail")
def fail(s: Annotated[str, Depends(session)]):
    log.append("handler")
    raise HTTPException(404, "nope")

c = TestClient(app)
c.get("/ok")
print(log)
log.clear()
r = c.get("/fail")
print(r.status_code, log)
```
```output
['open', 'handler', 'close']
404 ['open', 'handler', 'error 404', 'close']
```

**Пример 4. Подмена зависимости в тесте.** Обработчик не меняется; в тест подставлена заглушка с готовым ответом, и подмена очищается в `finally`.

```python run
from typing import Annotated
from fastapi import Depends, FastAPI
from fastapi.testclient import TestClient

app = FastAPI()

class RealClock:
    def now(self):
        import datetime
        return datetime.datetime.now().isoformat()

def get_clock():
    return RealClock()

@app.get("/time")
def time(clock: Annotated[RealClock, Depends(get_clock)]):
    return {"now": clock.now()}

class FixedClock:
    def now(self):
        return "2025-01-01T00:00:00"

c = TestClient(app)
app.dependency_overrides[get_clock] = lambda: FixedClock()
try:
    print(c.get("/time").json())
finally:
    app.dependency_overrides.clear()
print(c.get("/time").json()["now"] != "2025-01-01T00:00:00")
```
```output
{'now': '2025-01-01T00:00:00'}
True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Вызов зависимости вручную.** `Depends(get_repo())` - передаётся результат вызова, а не функция. Правильно: `Depends(get_repo)`.
* **Обращение к глобальному объекту в обработчике.** Если обработчик берёт `PROD_REPO` напрямую, подмена через `dependency_overrides` не сработает: тест будет менять реальные данные.
* **Подмена не очищается.** Забытый `dependency_overrides.clear()` ломает соседние тесты, причём порядок запуска влияет на результат.
* **Ключ подмены не тот.** В `dependency_overrides` ключом должна быть **та же** функция, что и в `Depends`; другая функция или `partial` не совпадут.
* **Проглоченное исключение в `yield`-зависимости.** `except Exception: log(...)` без `raise` превращает ошибку обработчика в успешный ответ с пустым телом.
* **Транзакция без отката.** Код после `yield` без `try/except/finally` не отработает корректно при исключении.
* **Дорогой ресурс на каждый запрос.** Создание пула или модели в зависимости - на каждый запрос; создавайте один раз на процесс.
* **Ожидание синглтона.** Зависимость вызывается на каждый запрос; кэш живёт только внутри запроса.
* **`@app.on_event`.** Устаревший способ запуска и остановки; используйте `lifespan`.

<!-- section: summary -->
## Итоги

* `Depends(callable)` - FastAPI вызывает зависимость сам, на каждый запрос; результат кэшируется внутри запроса (`use_cache=False` отключает).
* Зависимости образуют дерево; параметры запроса зависимостей входят в схему OpenAPI, сами зависимости - нет.
* `yield` даёт «открыть - использовать - закрыть»; исключение обработчика попадает внутрь генератора, поэтому откат делается в `except`/`finally`.
* `Annotated`-алиас убирает повторение `Depends` и сохраняет тип параметра.
* `app.dependency_overrides[оригинал] = замена` подменяет зависимость в тестах; очищайте словарь после теста.
* Ресурсы процесса живут в `lifespan`, а не в зависимости на запрос; `on_event` устарел.
