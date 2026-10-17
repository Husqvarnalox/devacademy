<!-- section: simple -->
## Форма - это проверка входных данных

Всё, что приходит от пользователя (поля формы, параметры адреса, JSON), нельзя считать правильным. Форма Django отвечает за три вещи:

* **описывает** ожидаемые поля и их типы (`CharField`, `IntegerField`, `EmailField`...);
* **проверяет** данные: тип, обязательность, длину, ваши правила - и собирает понятные ошибки;
* **превращает** текст в нужные типы Python: `cleaned_data["age"]` уже `int`.

Типовой цикл: `form = MyForm(request.POST)`; `if form.is_valid(): ...cleaned_data...` иначе показать `form.errors`. Данные берутся **только** из `cleaned_data` - сырой `request.POST` после проверки не трогают.

`ModelForm` - форма, построенная по модели: поля и базовые проверки (длина, уникальность) берутся из неё, а `form.save()` создаёт или обновляет объект.

Вторая тема урока - **CSRF**: чужой сайт может заставить браузер вошедшего пользователя отправить POST на ваш сайт, потому что браузер сам прикладывает cookie. Django защищает от этого токеном, который должен быть в запросе вместе с cookie. Подробно про механизм - в [уроке про сессии, CSRF и cookie](lesson:security.fundamentals.sessions-csrf-and-cookies).

<!-- section: precise -->
## Как это устроено

**Привязка и проверка.** `Form()` без данных - несвязанная (показывается пустой). `Form(data)` - связанная; `is_valid()` запускает проверку один раз и заполняет `cleaned_data` и `errors`. Порядок для каждого поля: `field.clean()` (преобразование типа, `required`, встроенные валидаторы вроде `max_length`), затем метод `clean_<имя>()` вашей формы; в конце `clean()` формы - для правил между полями. Если поле не прошло собственную проверку, его `clean_<имя>` не вызывается и в `clean()` его значения в `cleaned_data` нет, поэтому код `clean()` должен использовать `cleaned.get(...)`.

**Ошибки.** Правило сообщает об ошибке исключением `ValidationError(message, code="...")`; список ошибок - `ValidationError([e1, e2])`. `form.add_error("field", error)` привязывает ошибку к полю (в `clean()`), `None` - к форме целиком (ключ `__all__`). У ошибки всегда есть машинный `code`: `form.errors.get_json_data()` отдаёт `{"field": [{"message": ..., "code": ...}]}`. Коды стабильны, а тексты меняются с языком - в API и тестах опирайтесь на коды. Встроенные коды: `required`, `invalid`, `max_length`, `min_value`, `unique`.

**ModelForm.** `class Meta: model = M; fields = ["a", "b"]`. Список `fields` указывают **явно**: форма не должна принимать поля, которых вы не ожидали (например, `is_staff`), а `fields = "__all__"` делает это возможным. Помимо проверок полей ModelForm вызывает проверки модели, включая уникальность (`validate_unique`, код `unique`), уже **после** ваших `clean_<поле>`: поэтому если `clean_username` привёл значение к нижнему регистру, сравнивается нормализованное значение. `form.save()` создаёт объект; `save(commit=False)` возвращает несохранённый объект, чтобы дозаполнить поля.

**Нормализация и безопасность.** Правильное место для приведения к канонической форме (нижний регистр email, `strip`) - `clean_<поле>`: проверки и сохранение работают уже с нормализованным значением. Проверки на клиенте (HTML5, JavaScript) - только удобство; сервер обязан проверять всё сам.

**Отображение.** `form.as_p()` / `{{ form }}` рисуют поля с ошибками; значения и сообщения экранируются так же, как в шаблонах ([предыдущий урок](lesson:django.fundamentals.views-and-templates)). Виджет `PasswordInput` по умолчанию не подставляет введённое значение обратно в страницу.

**CSRF.** `django.middleware.csrf.CsrfViewMiddleware` проверяет все небезопасные методы (POST, PUT, PATCH, DELETE): в запросе должен быть токен, совпадающий с секретом из cookie `csrftoken`. Токен передаётся полем формы `csrfmiddlewaretoken` (тег `{% csrf_token %}` в шаблоне) или заголовком `X-CSRFToken` (для fetch/AJAX). Без токена Django отвечает 403 ещё до вызова представления. Cookie выдаётся, когда представление (или шаблон с `{% csrf_token %}`) вызвало `get_token()`; для страницы без такого тега есть `@ensure_csrf_cookie`. `@csrf_exempt` отключает проверку для представления - применяйте только для эндпоинтов, где запрос не опирается на cookie браузера (вебхуки с подписью, API с токеном в заголовке). Тестовый `Client` по умолчанию CSRF не проверяет: чтобы проверить защиту, создавайте `Client(enforce_csrf_checks=True)`.

<!-- section: examples -->
## Примеры

**Пример 1. Обычная форма: поля, `clean_<поле>`, `clean()`.** `cleaned_data` содержит типы Python; ошибки имеют коды.

```python run
import django
from django.conf import settings

settings.configure(USE_I18N=False)
django.setup()

from django import forms
from django.core.exceptions import ValidationError


class SignupForm(forms.Form):
    name = forms.CharField(max_length=10)
    age = forms.IntegerField(min_value=0)
    start = forms.IntegerField()
    end = forms.IntegerField()

    def clean_name(self):
        name = self.cleaned_data["name"].strip().title()
        if name == "Admin":
            raise ValidationError("reserved", code="reserved")
        return name

    def clean(self):
        cleaned = super().clean()
        if "start" in cleaned and "end" in cleaned and cleaned["start"] > cleaned["end"]:
            self.add_error("end", ValidationError("end before start", code="bad_range"))
        return cleaned


ok = SignupForm({"name": "  ann  ", "age": "30", "start": "1", "end": "5"})
print(ok.is_valid(), ok.cleaned_data)
bad = SignupForm({"name": "admin", "age": "x", "start": "9", "end": "1"})
print(bad.is_valid(), {f: [e["code"] for e in errs] for f, errs in bad.errors.get_json_data().items()})
print(SignupForm({}).errors.get_json_data()["name"][0]["code"], SignupForm().is_bound)
```
```output
True {'name': 'Ann', 'age': 30, 'start': 1, 'end': 5}
False {'name': ['reserved'], 'age': ['invalid'], 'end': ['bad_range']}
required False
```

**Пример 2. ModelForm: уникальность и `save()`.** Проверка уникальности выполняется после `clean_username`, то есть для нормализованного значения.

```python run
import django
from django.apps import AppConfig
from django.conf import settings


class AppCfg(AppConfig):
    name = __name__
    label = "demo"


settings.configure(
    INSTALLED_APPS=[__name__ + ".AppCfg"],
    DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
    DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    USE_I18N=False,
)
django.setup()

from django import forms
from django.db import connection, models


class User(models.Model):
    username = models.CharField(max_length=10, unique=True)
    age = models.PositiveSmallIntegerField()

    class Meta:
        app_label = "demo"


with connection.schema_editor() as editor:
    editor.create_model(User)


class UserForm(forms.ModelForm):
    class Meta:
        model = User
        fields = ["username", "age"]

    def clean_username(self):
        return self.cleaned_data["username"].lower()


first = UserForm({"username": "Ann", "age": "20"})
print(first.is_valid(), first.save().username, User.objects.count())
dup = UserForm({"username": "ANN", "age": "21"})
print(dup.is_valid(), [e["code"] for e in dup.errors.get_json_data()["username"]])
print(UserForm({"username": "x", "age": "-1"}).errors.get_json_data()["age"][0]["code"])
print(list(UserForm().fields))
```
```output
True ann 1
False ['unique']
min_value
['username', 'age']
```

**Пример 3. CSRF: без токена 403, с токеном успех.** Токен можно передать полем формы или заголовком; клиент хранит cookie между запросами.

```python run
import django
from django.conf import settings

settings.configure(
    ROOT_URLCONF=__name__, ALLOWED_HOSTS=["testserver"], SECRET_KEY="x", INSTALLED_APPS=[],
    MIDDLEWARE=["django.middleware.csrf.CsrfViewMiddleware"],
)
django.setup()

from django.http import HttpResponse
from django.test import Client
from django.urls import path
from django.views.decorators.csrf import csrf_exempt, ensure_csrf_cookie


@ensure_csrf_cookie
def page(request):
    return HttpResponse("posted" if request.method == "POST" else "form")


@csrf_exempt
def webhook(request):
    return HttpResponse("webhook ok")


urlpatterns = [path("page/", page), path("hook/", webhook)]

strict = Client(enforce_csrf_checks=True)
print("POST без токена:", strict.post("/page/").status_code)
strict.get("/page/")
token = strict.cookies["csrftoken"].value
print("cookie выдана:", len(token) > 0)
print("поле формы:", strict.post("/page/", {"csrfmiddlewaretoken": token}).status_code)
print("заголовок:", strict.post("/page/", headers={"X-CSRFToken": token}).status_code)
print("чужой токен:", strict.post("/page/", headers={"X-CSRFToken": "x" * 32}).status_code)
print("csrf_exempt:", strict.post("/hook/").status_code)
print("обычный Client:", Client().post("/page/").status_code)
```
```output
POST без токена: 403
cookie выдана: True
поле формы: 200
заголовок: 200
чужой токен: 403
csrf_exempt: 200
обычный Client: 200
```

**Пример 4. Отрисовка и экранирование.** Виджет добавляет атрибуты ограничений, значение в атрибуте экранируется, пароль обратно не выводится.

```python run
import django
from django.conf import settings

settings.configure(USE_I18N=False)
django.setup()

from django import forms


class LoginForm(forms.Form):
    name = forms.CharField(max_length=10)
    password = forms.CharField(widget=forms.PasswordInput)


form = LoginForm({"name": 'x" onfocus="alert(1)', "password": "secret"})
print(form.is_valid())
print(form["name"])
print(form["password"])
print(form.errors["name"].as_json())
```
```output
False
<input type="text" name="name" value="x&quot; onfocus=&quot;alert(1)" maxlength="10" required aria-invalid="true" aria-describedby="id_name_error" id="id_name">
<input type="password" name="password" required id="id_password">
[{"message": "Ensure this value has at most 10 characters (it has 20).", "code": "max_length"}]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Использовать `request.POST` вместо `cleaned_data`** после проверки: вы обходите нормализацию и приведение типов.
* **`fields = "__all__"` в ModelForm** (или `exclude`): новое поле модели автоматически станет принимать ввод пользователя, включая служебные флаги. Перечисляйте поля явно.
* **Опираться на текст ошибки вместо `code`.** Тексты зависят от языка и меняются; коды - контракт.
* **`clean()` без `get`**: если поле не прошло собственную проверку, в `cleaned_data` его нет, и `cleaned["start"]` даст `KeyError`.
* **Проверять уникальность вручную через `filter().exists()`** там, где есть `unique=True` в ModelForm: между проверкой и вставкой возможна гонка; настоящую гарантию даёт уникальный индекс в базе, а форма лишь красиво сообщает об ошибке.
* **Нормализовать после проверки**: привели email к нижнему регистру в `save()`, а уникальность проверили до этого. Нормализуйте в `clean_<поле>`.
* **Забыть CSRF-токен в форме или `@csrf_exempt` «чтобы заработало».** Исключение отключает защиту: если запрос опирается на cookie браузера, это уязвимость.
* **Считать проверку на клиенте защитой.** Её обходят одной командой `curl`.
* **Тестировать форму тестовым `Client()` по умолчанию** и думать, что CSRF работает: клиент по умолчанию проверок не делает, нужен `enforce_csrf_checks=True`.

<!-- section: summary -->
## Коротко

* Форма: описывает поля, проверяет, превращает в типы Python; используйте только `cleaned_data` после `is_valid()`.
* Порядок проверки: `field.clean` -> `clean_<поле>` -> `clean()`; ошибки - `ValidationError(message, code=...)`, `add_error` привязывает к полю.
* ModelForm берёт поля и проверки из модели, добавляет `unique` и `save()`; `fields` перечисляйте явно.
* Нормализацию (регистр, пробелы) делайте в `clean_<поле>`; контракт API - коды ошибок, а не тексты.
* CSRF: токен в поле `csrfmiddlewaretoken` или заголовке `X-CSRFToken`, без него 403; `csrf_exempt` - только для запросов без cookie-аутентификации; в тестах `Client(enforce_csrf_checks=True)`.
