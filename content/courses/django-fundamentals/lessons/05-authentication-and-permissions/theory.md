<!-- section: simple -->
## Кто вы и что вам можно

Две разные задачи, которые часто путают:

* **аутентификация** - установить, кто делает запрос (логин и пароль, сессия);
* **авторизация** - решить, что этому пользователю разрешено (права, группы).

`django.contrib.auth` даёт готовое решение для обеих: модель `User`, хранение паролей только в виде хеша, вход и выход через сессию, права (`Permission`), группы (`Group`) и декораторы для представлений. Дальше в запросе всегда есть `request.user`: либо реальный `User`, либо `AnonymousUser`.

Главное правило хранения пароля: **базе нужен не пароль, а его хеш**. Это общий принцип ([урок про хранение паролей](lesson:security.fundamentals.password-storage)); Django реализует его за вас, если создавать пользователей через `create_user` или `set_password`, а не записывать строку в поле `password` напрямую.

<!-- section: precise -->
## Как это устроено

**Пароль.** `make_password(raw)` возвращает строку вида `алгоритм$итерации$соль$хеш`. По умолчанию это `pbkdf2_sha256` с числом итераций, которое Django увеличивает от версии к версии, и случайной солью на каждый пароль. Поэтому два одинаковых пароля дают разные строки, а проверка идёт через `check_password(raw, encoded)`, который разбирает строку, берёт из неё алгоритм, соль и число итераций и пересчитывает хеш. Строка, начинающаяся с `!`, означает «пароль непригоден» (`set_unusable_password()`): войти по паролю нельзя.

**Создание и проверка.** `User.objects.create_user(username, email=None, password=None)` хеширует пароль; `create_superuser` ставит `is_staff` и `is_superuser`. `authenticate(request, username=..., password=...)` возвращает пользователя или `None` (неверный пароль, неактивный пользователь).

**Вход и выход.** `login(request, user)` записывает идентификатор пользователя в сессию и меняет ключ сессии (защита от фиксации сессии); `logout(request)` очищает сессию. Для этого нужны `SessionMiddleware` и `AuthenticationMiddleware`: последний кладёт в запрос `request.user`.

**Права.** Для каждой модели Django создаёт права `add_`, `change_`, `delete_`, `view_` (формат `app_label.codename`). Право можно выдать пользователю (`user_permissions`) или группе (`group.permissions`); `user.has_perm("app.codename")` учитывает оба источника. У активного суперпользователя `has_perm` всегда `True`. Результат проверки прав кэшируется на объекте пользователя, поэтому после выдачи права заново загрузите объект (в новом запросе так и происходит).

**Защита представлений.** `@login_required` перенаправляет анонима (`302`) на `settings.LOGIN_URL` с параметром `?next=<адрес>`. `@permission_required("app.codename")` по умолчанию тоже перенаправляет на вход; с `raise_exception=True` вошедшему без права отвечает `403`. Для классов есть `LoginRequiredMixin` и `PermissionRequiredMixin`.

**Две ступени ответа.** «Не вошёл» - это `302` на форму входа (или `401` для API), «вошёл, но нельзя» - `403`. Их не стоит смешивать: иначе вошедший пользователь без права будет бесконечно возвращаться на страницу входа.

**Тесты.** `client.force_login(user)` создаёт сессию без прохождения формы и без пересчёта хеша пароля, что экономит время; сам вход через форму проверяйте отдельным тестом.

<!-- section: examples -->
## Примеры

**Пример 1. Хеш пароля: формат, соль, проверка.** Выводим только устойчивые части: сам хеш и соль при каждом запуске новые.

```python run
import django
from django.conf import settings

settings.configure(USE_I18N=False)
django.setup()

from django.contrib.auth.hashers import check_password, make_password

a = make_password("s3cret-pass")
b = make_password("s3cret-pass")
algorithm, iterations, salt, digest = a.split("$")
print(algorithm, int(iterations) >= 600_000)
print("s3cret-pass" in a, a == b)
print(check_password("s3cret-pass", a), check_password("wrong", a))
print(make_password(None).startswith("!"))
```
```output
pbkdf2_sha256 True
False False
True False
True
```

**Пример 2. Пользователь, права и группы.** Права группы действуют на всех её участников. Объект пользователя кэширует права, поэтому после изменения его загружают заново.

```python run
import django
from django.conf import settings

settings.configure(
    SECRET_KEY="dev-only-secret",
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    INSTALLED_APPS=["django.contrib.contenttypes", "django.contrib.auth", "django.contrib.sessions"],
    MIDDLEWARE=[
        "django.contrib.sessions.middleware.SessionMiddleware",
        "django.contrib.auth.middleware.AuthenticationMiddleware",
    ],
    LOGIN_URL="/login/",
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django.core.management import call_command

call_command("migrate", verbosity=0)

from django.contrib.auth.models import Group, Permission, User

user = User.objects.create_user("ann", password="s3cret-pass")
print(user.password.startswith("pbkdf2_sha256$"), user.has_perm("auth.add_group"))

group = Group.objects.create(name="staff-editors")
group.permissions.add(Permission.objects.get(codename="add_group"))
user.groups.add(group)

print(user.has_perm("auth.add_group"))  # кэш ещё старый
fresh = User.objects.get(pk=user.pk)
print(fresh.has_perm("auth.add_group"), fresh.has_perm("auth.delete_group"))

root = User.objects.create_superuser("root", "root@example.com", "s3cret-pass")
print(root.has_perm("auth.delete_group"), root.is_staff)
```
```output
True False
False
True False
True True
```

**Пример 3. Вход, `login_required`, `403` и `302`.** Представления в этом же файле; запросы отправляет тестовый `Client`.

```python run
import django
from django.conf import settings

settings.configure(
    SECRET_KEY="dev-only-secret",
    ROOT_URLCONF=__name__,
    ALLOWED_HOSTS=["testserver"],
    INSTALLED_APPS=["django.contrib.contenttypes", "django.contrib.auth", "django.contrib.sessions"],
    MIDDLEWARE=[
        "django.contrib.sessions.middleware.SessionMiddleware",
        "django.contrib.auth.middleware.AuthenticationMiddleware",
    ],
    LOGIN_URL="/login/",
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django.core.management import call_command

call_command("migrate", verbosity=0)

from django.contrib.auth import authenticate, login
from django.contrib.auth.decorators import login_required, permission_required
from django.contrib.auth.models import Permission, User
from django.http import JsonResponse
from django.test import Client
from django.urls import path


def login_view(request):
    user = authenticate(request, username=request.POST["username"], password=request.POST["password"])
    if user is None:
        return JsonResponse({"error": "invalid"}, status=401)
    login(request, user)
    return JsonResponse({"user": user.username})


@login_required
def secret(request):
    return JsonResponse({"hello": request.user.username})


@login_required
@permission_required("auth.add_group", raise_exception=True)
def admin_only(request):
    return JsonResponse({"ok": True})


urlpatterns = [path("login/", login_view), path("secret/", secret), path("admin-only/", admin_only)]

user = User.objects.create_user("ann", password="s3cret-pass")
c = Client()
r = c.get("/secret/")
print(r.status_code, r["Location"])
print(c.post("/login/", {"username": "ann", "password": "bad"}).status_code)
print(c.post("/login/", {"username": "ann", "password": "s3cret-pass"}).json())
print(c.get("/secret/").json())
print(c.get("/admin-only/").status_code)
user.user_permissions.add(Permission.objects.get(codename="add_group"))
print(Client().get("/admin-only/").status_code)
c2 = Client()
c2.force_login(User.objects.get(pk=user.pk))
print(c2.get("/admin-only/").status_code)
```
```output
302 /login/?next=/secret/
401
{'user': 'ann'}
{'hello': 'ann'}
403
302
200
```

<!-- section: mistakes -->
## Типичные ошибки

* **Записать пароль прямо в поле:** `User(username="a", password="123")` или `User.objects.create(password=...)`. Это сохранит открытый текст. Используйте `create_user` или `set_password()`.
* **Сравнивать пароли самому:** `user.password == raw`. Используйте `check_password` или `authenticate`.
* **Путать 401/302 и 403.** Анониму нужно предложить войти, вошедшему без права - отказать (`403`). Без `raise_exception=True` `permission_required` будет перенаправлять уже вошедшего на страницу входа.
* **Проверять `is_staff` вместо права.** `is_staff` лишь допускает в админку; доступ к действию выражайте правом или группой.
* **Выдавать права по именам групп в коде** (`if user.groups.filter(name="editors")`): лучше проверять право `has_perm`, а группы настраивать данными.
* **Забыть, что `has_perm` кэшируется на объекте.** После выдачи права в том же запросе перезагрузите пользователя.
* **Слабый или отключённый хеш.** Не заменяйте `PASSWORD_HASHERS` на быстрый (MD5) вне тестов: ускоряйте тесты через `force_login`, а не ослабляя хеширование в продакшене.
* **Вход без `login()`:** запись `request.session["user_id"] = ...` вручную обходит смену ключа сессии и обновление хеша сессии.

<!-- section: summary -->
## Коротко

* Аутентификация отвечает на вопрос «кто это», авторизация - «что ему можно»; в запросе есть `request.user`.
* Пароль хранится как `алгоритм$итерации$соль$хеш` (по умолчанию PBKDF2-SHA256, соль у каждого пароля своя); создавайте пользователей через `create_user`/`set_password`, проверяйте через `authenticate`/`check_password`.
* `login()` и `logout()` работают через сессию и требуют `SessionMiddleware` и `AuthenticationMiddleware`.
* Права `app.codename` выдают пользователю или группе; проверка - `has_perm`, суперпользователь проходит всё.
* `@login_required` даёт `302` на `LOGIN_URL?next=...`, `@permission_required(..., raise_exception=True)` - `403` для вошедшего без права.
