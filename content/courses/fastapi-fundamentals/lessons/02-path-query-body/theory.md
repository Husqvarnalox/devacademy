<!-- section: simple -->
## Откуда функция берёт свои аргументы

Обработчик в FastAPI - обычная функция, а её параметры - это «заказ» на данные из запроса. Фреймворк читает сигнатуру и сам решает, откуда взять каждое значение:

* **Параметр пути** - часть URL: `/users/42`. Имя в фигурных скобках шаблона совпадает с именем аргумента.
* **Параметр запроса (query)** - то, что после знака вопроса: `/users?limit=10&q=ann`. Это любой «простой» аргумент, которого нет в пути.
* **Тело запроса (body)** - JSON, который клиент присылает в `POST`/`PUT`/`PATCH`. Его описывают моделью Pydantic.

Значения приходят текстом, а вы объявили `limit: int`. Фреймворк преобразует `"10"` в `10`, а на `"abc"` отвечает кодом **422** и подробным объяснением, что именно не так. Вам не нужно писать `try/except ValueError` и ручные проверки диапазонов: ограничения тоже описываются в аннотациях (`ge=1`, `max_length=20`).

Ответ описывается так же: `response_model` - это «форма» того, что увидит клиент. Если внутри системы у пользователя есть `password_hash`, а в модели ответа такого поля нет, наружу оно не уйдёт, даже если обработчик вернул запись целиком. Это важнейшее правило безопасности API: **вход и выход - разные модели**.

<!-- section: precise -->
## Как это устроено

**Правило вывода источника.** При регистрации маршрута FastAPI обходит сигнатуру функции (`inspect.signature` + аннотации) и для каждого параметра определяет, откуда он берётся:

1. имя есть в шаблоне пути -> *path* (параметр обязательный);
2. тип - модель Pydantic (или `dict`/`list` из объектов с `Body`) -> *body*;
3. простой тип (`int`, `str`, `bool`, `float`, `datetime`, `UUID` и т.п.) -> *query*; есть значение по умолчанию -> необязательный;
4. явная пометка `Path(...)`, `Query(...)`, `Body(...)`, `Header(...)`, `Cookie(...)` в `Annotated` переопределяет вывод. Список в строке запроса (`?tag=a&tag=b`) требует явного `Query()`, иначе `list[str]` был бы принят за тело.

Если тело одно (одна модель), в JSON лежат её поля напрямую. Если телом объявлено несколько параметров, JSON должен содержать вложенные объекты по именам параметров.

**Ограничения через Annotated.** Современный стиль - `Annotated[int, Query(ge=1, le=100)] = 10`: тип, метаданные и значение по умолчанию разделены, а сам алиас можно переиспользовать. У `Query`/`Path` те же ограничения, что у Pydantic `Field`: `gt/ge/lt/le`, `min_length/max_length`, `pattern`, а также `alias`, `description`, `deprecated`. Параметр пути всегда обязателен, поэтому значения по умолчанию у него нет.

**Конвертация и валидация.** Значения query приходят строками; Pydantic в режиме `lax` приводит `"5"` -> `5`, `"true"/"yes"/"1"/"on"` -> `True`. Неприводимое значение даёт ошибку типа `int_parsing`, `bool_parsing` и т.п.; нарушенное ограничение - `greater_than_equal`, `string_too_short`, `less_than_equal`. Если ошибок несколько, **все** собираются в один ответ.

**Форма 422.** Ответ всегда такой: `{"detail": [ {"type": ..., "loc": [...], "msg": ..., "input": ..., "ctx": {...}} ]}`. `loc` - путь к значению: источник и имя (`["query", "limit"]`, `["body", "username"]`, для вложенных моделей - дальше по полям и индексам). Код `422` означает «запрос синтаксически корректен, но данные не годятся». Код `400` фреймворк сам почти не использует: он обычно оставлен вашей бизнес-логике.

**Тело запроса.** Модель Pydantic в аргументе: FastAPI читает тело, разбирает JSON и вызывает валидацию модели. Битый JSON даёт `422` с типом `json_invalid`; тело неверного вида (например, список вместо объекта) - `model_attributes_type`. Более подробно модели рассматриваются в [следующем уроке](lesson:fastapi.fundamentals.pydantic-models).

**response_model.** Модель ответа можно задать параметром декоратора `response_model=` или аннотацией возвращаемого значения (`def f() -> UserOut`). FastAPI **валидирует и фильтрует** результат по этой модели: лишние поля удаляются, значения приводятся к типам, а в OpenAPI попадает именно эта схема. Если результат не соответствует модели (например, нет обязательного `id`), это ошибка *сервера*: `ResponseValidationError`, клиент получит `500`. Параметры `response_model_exclude_unset`, `response_model_exclude_none` убирают из ответа поля, которые не были заданы или равны `None`.

**Mass assignment.** Если принимать в `POST` ту же модель, что хранится в базе, клиент сможет прислать `{"is_admin": true}` и получить лишнюю роль. Отдельная входная модель (`UserCreate`) определяет, что клиенту разрешено задать; отдельная выходная (`UserOut`) - что ему разрешено увидеть. Лишние поля во входном JSON по умолчанию просто игнорируются (запретить их можно через `extra="forbid"`, см. следующий урок).

**Когда не использовать.** Не вешайте на query-параметры чувствительные данные (они попадают в логи и историю); секреты передают в заголовках или теле. Для массовых данных и сложных фильтров лучше отдельная модель-фильтр, а не десять параметров.

**Типичные ошибки тестирования.** Проверяйте не только успех, но и каждую границу (`ge=1`: проверьте 0 и 1), форму `loc` и отсутствие чувствительных полей в **каждом** ответе, где возвращается пользователь (создание, получение, список).

**Продакшен.** Всегда ограничивайте `limit` сверху: эндпоинт списка без верхней границы - лёгкий способ положить базу. Тело запроса дополнительно ограничивают на уровне прокси или сервера (размер).

<!-- section: examples -->
## Примеры

**Пример 1. Откуда берётся каждый параметр и как выглядит 422.**

```python run
from typing import Annotated
from fastapi import FastAPI, Path, Query
from fastapi.testclient import TestClient

app = FastAPI()

@app.get("/users/{user_id}/posts")
def posts(
    user_id: Annotated[int, Path(ge=1)],
    page: Annotated[int, Query(ge=1)] = 1,
    tag: Annotated[list[str] | None, Query()] = None,
    draft: bool = False,
):
    return {"user_id": user_id, "page": page, "tag": tag, "draft": draft}

c = TestClient(app)
print(c.get("/users/7/posts?page=2&tag=a&tag=b&draft=yes").json())
r = c.get("/users/0/posts?page=x")
print(r.status_code)
for e in r.json()["detail"]:
    print(e["loc"], e["type"])
```
```output
{'user_id': 7, 'page': 2, 'tag': ['a', 'b'], 'draft': True}
422
['path', 'user_id'] greater_than_equal
['query', 'page'] int_parsing
```

**Пример 2. Без response_model пароль утекает, с ним - нет.**

```python run
from fastapi import FastAPI
from fastapi.testclient import TestClient
from pydantic import BaseModel

app = FastAPI()

class UserOut(BaseModel):
    id: int
    username: str

DB = {1: {"id": 1, "username": "ann", "password_hash": "pbkdf2$secret"}}

@app.get("/raw/{uid}")
def raw(uid: int):
    return DB[uid]

@app.get("/safe/{uid}", response_model=UserOut)
def safe(uid: int):
    return DB[uid]

c = TestClient(app)
print("raw :", c.get("/raw/1").json())
print("safe:", c.get("/safe/1").json())
print(sorted(app.openapi()["components"]["schemas"]["UserOut"]["properties"]))
```
```output
raw : {'id': 1, 'username': 'ann', 'password_hash': 'pbkdf2$secret'}
safe: {'id': 1, 'username': 'ann'}
['id', 'username']
```

**Пример 3. Вход и выход - разные модели; лишние поля игнорируются.**

```python run
from fastapi import FastAPI
from fastapi.testclient import TestClient
from pydantic import BaseModel, Field

app = FastAPI()

class UserCreate(BaseModel):
    username: str = Field(min_length=3)
    password: str = Field(min_length=8)

class UserOut(BaseModel):
    id: int
    username: str

@app.post("/users", status_code=201)
def create(body: UserCreate) -> UserOut:
    stored = {"id": 1, "username": body.username, "password_hash": "x", "is_admin": False}
    return stored

c = TestClient(app)
r = c.post("/users", json={"username": "ann", "password": "longenough", "is_admin": True})
print(r.status_code, r.json())
r = c.post("/users", json={"username": "an"})
for e in r.json()["detail"]:
    print(e["loc"], e["type"], e["msg"])
```
```output
201 {'id': 1, 'username': 'ann'}
['body', 'username'] string_too_short String should have at least 3 characters
['body', 'password'] missing Field required
```

**Пример 4. Если ответ не соответствует модели - это 500, а не 422.**

```python run
from fastapi import FastAPI
from fastapi.testclient import TestClient
from pydantic import BaseModel

app = FastAPI()

class UserOut(BaseModel):
    id: int
    username: str

@app.get("/broken", response_model=UserOut)
def broken():
    return {"username": "ann"}

c = TestClient(app, raise_server_exceptions=False)
r = c.get("/broken")
print(r.status_code, r.text)
```
```output
500 Internal Server Error
```

<!-- section: mistakes -->
## Типичные ошибки

* **Возврат записи БД без `response_model`.** Самая частая причина утечки хешей паролей и внутренних флагов. Всегда задавайте модель ответа.
* **Одна модель на всё.** Модель, которую вы и принимаете, и храните, и отдаёте, рано или поздно позволит клиенту записать лишнее или увидеть лишнее.
* **Путаница path/query.** Имя в `{фигурных скобках}` не совпадает с аргументом - параметр превращается в обязательный query-параметр и клиент получает `422` с `["query", "..."]`.
* **Список в query без `Query()`.** `tag: list[str] | None = None` без явной пометки FastAPI считает телом запроса.
* **Забытая верхняя граница `limit`.** Клиент запрашивает `limit=10**9`.
* **Ожидание 400 вместо 422.** Для ошибок валидации по умолчанию отвечает 422; если контракт API требует 400, это настраивается обработчиком (урок про ошибки), а не решается путаницей кодов.
* **Тесты только на «счастливый путь».** Без проверки границ и `loc` ошибки валидации проявятся у клиентов.
* **`Optional` без смысла.** `q: str | None = None` значит «параметр можно не передавать»; `q: str | None` без значения по умолчанию в Pydantic v2 - обязательный параметр, который может быть `None`.

<!-- section: summary -->
## Итоги

* Источник параметра выводится из сигнатуры: путь, query (простые типы), тело (модель Pydantic); `Path/Query/Body` дают явные ограничения.
* Ограничения и значения по умолчанию пишите в `Annotated[...]`; все ошибки валидации приходят вместе в `422` с `loc`, `type`, `msg`.
* `response_model` фильтрует и проверяет ответ; несоответствие - `500`, а не `422`.
* Для входа и выхода используйте разные модели; никогда не возвращайте записи хранилища без модели ответа.
* Тестируйте границы ограничений, форму `loc` и отсутствие чувствительных полей в каждом ответе.
