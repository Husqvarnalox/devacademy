"""Регистрация: ModelForm, проверки полей и защита от CSRF. Каркас не меняйте; ваш код - ниже строки «ВАШ КОД»."""
import re  # noqa: F401

import django
from django.apps import AppConfig
from django.conf import settings


class MembersConfig(AppConfig):
    name = __name__
    label = "members"


# ---- каркас ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[__name__ + ".MembersConfig"],
        MIDDLEWARE=["django.middleware.csrf.CsrfViewMiddleware"],  # проверка CSRF для небезопасных методов
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    )
    django.setup()

from django import forms
from django.core.exceptions import ValidationError
from django.db import connection, models
from django.http import HttpResponse, JsonResponse
from django.urls import path
from django.views.decorators.csrf import ensure_csrf_cookie
from django.views.decorators.http import require_http_methods


class Member(models.Model):
    username = models.CharField(max_length=20, unique=True)
    email = models.EmailField()
    age = models.PositiveSmallIntegerField()

    class Meta:
        app_label = "members"


with connection.schema_editor() as _editor:
    _editor.create_model(Member)


# ---- ВАШ КОД ниже ----
USERNAME_RE = re.compile(r"[a-z][a-z0-9_]{2,19}")


class RegistrationForm(forms.ModelForm):
    """ModelForm по Member (username, email, age) плюс поля password и password2 (PasswordInput).

    Правила (коды ошибок - в скобках):
      * username: после strip приводится к нижнему регистру; должен подходить под USERNAME_RE (invalid_username);
        уникальность проверяет сам ModelForm (код unique), писать её не нужно;
      * email: сохраняется в нижнем регистре;
      * age: не меньше 18 (too_young);
      * password: минимум 8 символов (too_short) и хотя бы одна цифра (no_digit) - обе ошибки могут быть сразу;
      * password2 должен совпадать с password: ошибка на поле password2 с кодом password_mismatch (метод clean).
    """

    # TODO: поля password и password2, класс Meta, методы clean_<поле> и clean


def register(request):
    """GET -> 200 'register form' и выдача CSRF-cookie (ensure_csrf_cookie); POST -> проверка формы:
    успех - 201 {"id": ..., "username": ...}; ошибка - 400 {"errors": {поле: [коды ошибок]}}; другие методы - 405."""
    raise NotImplementedError


urlpatterns = []  # TODO: path("register/", register, name="register")
