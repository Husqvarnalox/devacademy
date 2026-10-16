<!-- section: simple -->
## Два разных вопроса: «кто вы» и «что вам можно»

**Аутентификация** отвечает на вопрос «кто это?»: пользователь предъявляет секрет (пароль, токен), сервер устанавливает личность. **Авторизация** отвечает «что этому пользователю разрешено?»: роль, владение ресурсом, права. Их нельзя смешивать, и HTTP различает их кодами:

* **401 Unauthorized** - личность не установлена: токена нет, он просрочен или неизвестен. Вместе с ним отправляют заголовок `WWW-Authenticate: Bearer`.
* **403 Forbidden** - личность известна, но прав недостаточно.

Типичный путь выглядит так: пользователь регистрируется, сервер сохраняет **не пароль, а его хеш**; при входе сервер проверяет пароль и выдаёт **токен** - длинную случайную строку; клиент присылает её в заголовке `Authorization: Bearer <токен>` с каждым запросом; зависимость ([урок про Depends](lesson:fastapi.fundamentals.dependency-injection)) превращает токен в текущего пользователя; ещё одна зависимость проверяет роль.

В этом уроке вы реализуете именно такую схему с «непрозрачными» токенами, которые хранятся на сервере. Это самый простой вариант, который при этом легко отзывается. Про JWT, OAuth2 и пароли в продакшене вы узнаете, где это уместно, и почему здесь они не используются.

> Безопасность - область, где «работает» не значит «безопасно». Учебный код ниже показывает механику и принципы. Для настоящей системы берите проверенные библиотеки и провайдеров идентификации, а не собственные криптографические решения.

<!-- section: precise -->
## Как это устроено

**Хранение паролей.** Пароль никогда не хранят и не логируют в открытом виде. Хранят результат функции, которая:

* **медленная и настраиваемая** (Argon2id, scrypt, bcrypt, PBKDF2): перебор паролей по украденной базе должен быть дорогим. Обычный SHA-256 задуман быстрым, его подбирают миллиардами попыток в секунду;
* **с солью** - случайными байтами, уникальными для каждого пароля. Тогда одинаковые пароли дают разные хеши, а заранее вычисленные таблицы бесполезны;
* **хранит параметры** рядом с результатом (`scrypt$16384$8$1$<соль>$<хеш>`), чтобы со временем повышать стоимость и всё ещё проверять старые записи.

В стандартной библиотеке есть `hashlib.scrypt(password, salt=..., n=..., r=..., p=..., dklen=...)`: `n` - стоимость по процессору и памяти (степень двойки), `r` - размер блока, `p` - параллелизм. Это разумный учебный выбор без сторонних пакетов. **Компромиссы:** параметры нужно подбирать по железу (OWASP рекомендует конкретные сочетания), память `128 * n * r` байт на проверку делает вход ресурсоёмким - это ожидаемо, но вход нужно ограничивать по частоте; в продакшене чаще берут **argon2-cffi** или **bcrypt**: они сами выбирают безопасные значения, формат строки и умеют «перехешировать» при смене параметров. Сравнение хешей делают через `hmac.compare_digest` (время не зависит от позиции первого несовпадения), а не `==`.

**Одинаковый ответ при ошибке входа.** «Пользователь не найден» и «неверный пароль» должны быть неразличимы: код, тело, заголовки. Иначе злоумышленник перебирает имена и узнаёт, какие аккаунты существуют (user enumeration). Различие по времени ответа даёт тот же эффект; в продакшене хеш вычисляют и для несуществующего пользователя, а вход ограничивают по частоте.

**Непрозрачные токены.** `secrets.token_urlsafe(32)` даёт 32 случайных байта (256 бит) в виде 43 символов base64url. Токен - просто ключ: сервер хранит соответствие «токен -> пользователь» (словарь, Redis, база) и при каждом запросе ищет токен. Достоинства: тривиальный **отзыв** (удалить запись), мгновенное закрытие сессии, никакой криптографии при проверке. Недостаток: нужен поиск в хранилище. Токены генерируют только модулем `secrets` (источник случайности ОС), не `random`: у `random` предсказуемое состояние. В базе токен лучше хранить в виде хеша, чтобы утечка базы не раскрыла действующих сессий (в учебном примере мы храним как есть).

**Как FastAPI достаёт токен: `OAuth2PasswordBearer`.** `OAuth2PasswordBearer(tokenUrl="login")` - это зависимость. Она читает `Authorization`, проверяет схему `Bearer` и возвращает токен как строку. Если заголовка нет или схема другая, она отвечает `401` и `WWW-Authenticate: Bearer`. Одновременно она добавляет схему безопасности в OpenAPI, и в интерфейсе `/docs` появляется кнопка Authorize. Важно понимать, чего она **не делает**: не выдаёт токены, не проверяет пароль, не проверяет сам токен. `tokenUrl` лишь подсказка для интерфейса документации. Проверка токена - ваша зависимость `get_current_user`.

**Вход по JSON, а не по форме.** Стандартная `OAuth2PasswordRequestForm` разбирает `application/x-www-form-urlencoded` и требует пакета `python-multipart`, которого в окружении курса нет, поэтому наш `/login` принимает JSON. В реальном проекте с формой установите `python-multipart`. Отдельно: сценарий OAuth 2.0 «Resource Owner Password Credentials», где клиентское приложение собирает пароль пользователя, не рекомендован современными рекомендациями по безопасности (RFC 9700). Это нормальный учебный пример, но для настоящих сторонних клиентов используют код авторизации с PKCE через провайдера идентификации.

**Авторизация по ролям через зависимости.** `get_current_user` превращает токен в пользователя. Фабрика `require_role("admin")` возвращает зависимость, которая зависит от `get_current_user` и бросает `403`, если роль не подходит. Порядок важен и получается сам: сначала аутентификация (`401`), затем роль (`403`). Роль пользователя **никогда не берут из тела запроса**: иначе клиент сам себя назначит администратором (mass assignment). В модель регистрации поле `role` просто не включают.

**JWT концептуально.** JWT (RFC 7519) - строка `заголовок.нагрузка.подпись`, где каждая часть - base64url. В нагрузке лежат утверждения (claims): `sub` (кто), `exp` (когда истекает), `iss`, `aud`. Подпись (например, HMAC-SHA256 с общим секретом или RSA/EdDSA с парой ключей) защищает **целостность**: нагрузку нельзя изменить незаметно. Она **не скрывает** содержимое: прочитать нагрузку может любой. Достоинство: сервису не нужно обращаться к хранилищу, он проверяет подпись сам (удобно между микросервисами). Платой становятся ловушки (RFC 8725):

* **Отзыв.** Токен действует до `exp`. Выйти «прямо сейчас» нельзя без чёрного списка (то есть без серверного хранилища, ради отсутствия которого JWT и выбирали). Обычно делают короткий срок жизни (минуты) плюс refresh-токен.
* **Алгоритм.** Проверяющий обязан сам зафиксировать допустимые алгоритмы. Принимать `alg` из заголовка токена нельзя: известны атаки с `alg: none` и подменой асимметричного алгоритма на симметричный.
* **Проверки claims.** Нужно проверять `exp`, `iss`, `aud`. Забытая проверка срока - вечный токен.
* **Секреты.** Слабый или попавший в репозиторий ключ подписи позволяет выпускать токены от имени любого пользователя. Хранить ключи нужно в окружении, ротация - заранее продуманная процедура.
* **Хранение у клиента.** Токен в `localStorage` доступен любому скрипту при XSS; куки с `HttpOnly` и `SameSite` защищают от кражи скриптом, но требуют защиты от CSRF.
* **Содержимое.** В нагрузку не кладут пароли и персональные данные.

Поэтому мы не пишем JWT вручную: подписи и проверки claims - зона готовых проверенных библиотек (в курсе нет ни одной), а непрозрачный токен проще и безопаснее для учебного проекта.

<!-- section: examples -->
## Примеры

**Пример 1. Хеш пароля: соль, параметры, проверка.** Два хеша одного пароля различаются из-за соли, а параметры хранятся в самой строке. Неверный пароль и мусор вместо хеша дают `False`.

```python run
import hashlib, hmac, secrets

def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.scrypt(password.encode(), salt=salt, n=2**14, r=8, p=1, dklen=32)
    return "$".join(["scrypt", "16384", "8", "1", salt.hex(), digest.hex()])

def verify_password(password: str, stored: str) -> bool:
    try:
        _, n, r, p, salt_hex, hash_hex = stored.split("$")
        expected = bytes.fromhex(hash_hex)
        got = hashlib.scrypt(password.encode(), salt=bytes.fromhex(salt_hex),
                             n=int(n), r=int(r), p=int(p), dklen=len(expected))
    except ValueError:
        return False
    return hmac.compare_digest(got, expected)

h1, h2 = hash_password("correct horse"), hash_password("correct horse")
print(h1 == h2, h1.split("$")[:4], len(h1.split("$")))
print(verify_password("correct horse", h1), verify_password("Correct horse", h1), verify_password("x", "garbage"))
```
```output
False ['scrypt', '16384', '8', '1'] 6
True False False
```

**Пример 2. Токены и вход.** Сервис регистрирует пользователя, выдаёт токен и отвечает на `/me`. Без токена и с чужим токеном - `401`. Пароль в ответе не появляется.

```python run
import secrets
from typing import Annotated
from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import OAuth2PasswordBearer
from fastapi.testclient import TestClient
from pydantic import BaseModel

app = FastAPI()
oauth2 = OAuth2PasswordBearer(tokenUrl="login")
USERS = {"ann": {"id": 1, "name": "ann", "password": "pw-for-demo-only"}}
TOKENS: dict[str, str] = {}

class Login(BaseModel):
    name: str
    password: str

@app.post("/login")
def login(body: Login):
    user = USERS.get(body.name)
    if user is None or user["password"] != body.password:  # демо: настоящий код хранит хеш
        raise HTTPException(401, "invalid credentials", headers={"WWW-Authenticate": "Bearer"})
    token = secrets.token_urlsafe(32)
    TOKENS[token] = body.name
    return {"access_token": token, "token_type": "bearer"}

def current(token: Annotated[str, Depends(oauth2)]):
    name = TOKENS.get(token)
    if name is None:
        raise HTTPException(401, "invalid token", headers={"WWW-Authenticate": "Bearer"})
    return USERS[name]

@app.get("/me")
def me(user: Annotated[dict, Depends(current)]):
    return {"id": user["id"], "name": user["name"]}

c = TestClient(app)
print(c.get("/me").status_code, c.get("/me", headers={"Authorization": "Bearer nope"}).status_code)
tok = c.post("/login", json={"name": "ann", "password": "pw-for-demo-only"}).json()
print(tok["token_type"], len(tok["access_token"]))
r = c.get("/me", headers={"Authorization": f"Bearer {tok['access_token']}"})
print(r.status_code, r.json())
print(c.get("/me").headers["www-authenticate"])
```
```output
401 401
bearer 43
200 {'id': 1, 'name': 'ann'}
Bearer
```

**Пример 3. Роль через фабрику зависимостей: 401, 403 и 200.** Порядок кодов определяется порядком зависимостей: сначала токен, потом роль.

```python run
from typing import Annotated
from fastapi import Depends, FastAPI, HTTPException
from fastapi.security import OAuth2PasswordBearer
from fastapi.testclient import TestClient

app = FastAPI()
oauth2 = OAuth2PasswordBearer(tokenUrl="login")
SESSIONS = {"tok-user": {"name": "ann", "role": "user"}, "tok-admin": {"name": "root", "role": "admin"}}

def current(token: Annotated[str, Depends(oauth2)]):
    user = SESSIONS.get(token)
    if user is None:
        raise HTTPException(401, "invalid token", headers={"WWW-Authenticate": "Bearer"})
    return user

def require_role(role: str):
    def checker(user: Annotated[dict, Depends(current)]):
        if user["role"] != role:
            raise HTTPException(403, f"{role} only")
        return user
    return checker

@app.get("/admin")
def admin(user: Annotated[dict, Depends(require_role("admin"))]):
    return {"hello": user["name"]}

c = TestClient(app)
for header in (None, "tok-bad", "tok-user", "tok-admin"):
    h = {"Authorization": f"Bearer {header}"} if header else {}
    r = c.get("/admin", headers=h)
    print(header, r.status_code, r.json())
```
```output
None 401 {'detail': 'Not authenticated'}
tok-bad 401 {'detail': 'invalid token'}
tok-user 403 {'detail': 'admin only'}
tok-admin 200 {'hello': 'root'}
```

**Пример 4. Что именно знает OpenAPI о защите.** Схема безопасности попадает в документацию; защищённые маршруты помечены требованием, открытые - нет.

```python run
from typing import Annotated
from fastapi import Depends, FastAPI
from fastapi.security import OAuth2PasswordBearer

app = FastAPI()
oauth2 = OAuth2PasswordBearer(tokenUrl="login")

@app.get("/public")
def public():
    return {}

@app.get("/private")
def private(token: Annotated[str, Depends(oauth2)]):
    return {}

spec = app.openapi()
scheme = spec["components"]["securitySchemes"]["OAuth2PasswordBearer"]
print(scheme["type"], scheme["flows"]["password"]["tokenUrl"])
print("public:", spec["paths"]["/public"]["get"].get("security"))
print("private:", spec["paths"]["/private"]["get"]["security"])
```
```output
oauth2 login
public: None
private: [{'OAuth2PasswordBearer': []}]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Пароль или хеш в ответе и в логах.** Выходная модель (`response_model=UserOut`) без поля пароля - обязательное условие. Не логируйте тела запросов `/login` и `/register`.
* **Быстрый хеш вместо медленного.** `sha256(password)` подбирается на GPU. Нужны scrypt, argon2 или bcrypt с солью.
* **Одна соль на всех или соль в коде.** Соль должна быть случайной для каждого пароля.
* **Сравнение через `==`.** Используйте `hmac.compare_digest` для хешей и токенов.
* **Токен из `random`.** Для токенов и секретов - только `secrets`.
* **Разные ответы для «нет пользователя» и «неверный пароль».** Раскрывает список аккаунтов.
* **Путаница 401 и 403.** Нет или недействительный токен - 401 (с `WWW-Authenticate`); известный пользователь без прав - 403.
* **Роль из тела запроса.** Клиент не должен выбирать себе роль; это поле задаёт сервер.
* **Проверка роли в теле обработчика, которую забыли.** Выносите проверку в зависимость: её видно в сигнатуре маршрута и в OpenAPI, и её нельзя забыть в новом маршруте группы.
* **Вера в `OAuth2PasswordBearer`.** Он не проверяет токен: без своей зависимости `get_current_user` любой текст в заголовке проходит как «токен».
* **Токен без срока жизни и отзыва.** Нужна возможность выйти (удалить токен) и разумное время жизни.
* **JWT «потому что так принято».** Не принимайте `alg` из токена, всегда проверяйте `exp`, не кладите секреты в claims, держите ключ вне репозитория.

<!-- section: summary -->
## Итоги

* Аутентификация - «кто вы» (401 при неудаче), авторизация - «что можно» (403).
* Пароль хранят как медленный хеш с уникальной солью и параметрами в строке (scrypt из `hashlib`; в продакшене чаще argon2/bcrypt); сравнивают через `hmac.compare_digest`.
* Токены - `secrets.token_urlsafe(32)`, хранятся на сервере; это даёт простой отзыв и выход.
* `OAuth2PasswordBearer` лишь достаёт Bearer-токен и описывает схему в OpenAPI; проверка - ваша зависимость.
* Роли проверяются фабрикой зависимостей; роль не принимается от клиента.
* Вход отвечает одинаково при неизвестном пользователе и неверном пароле; ответы API не содержат хешей.
* JWT - подписанный, но читаемый формат; его ловушки: отзыв, алгоритм, проверка claims, секреты, хранение.
