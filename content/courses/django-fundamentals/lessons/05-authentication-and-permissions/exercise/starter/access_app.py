"""Аутентификация и права. Каркас не меняйте; ваш код - ниже строки «ВАШ КОД»."""
import django
from django.apps import AppConfig
from django.conf import settings
from django.contrib.auth.hashers import PBKDF2PasswordHasher


class AccessConfig(AppConfig):
    name = __name__
    label = "access"


class QuickPBKDF2PasswordHasher(PBKDF2PasswordHasher):
    """Тот же pbkdf2_sha256, но с минимально допустимым здесь числом итераций.

    Настоящий Django (в 6.1 - 1 500 000 итераций) намеренно хеширует медленно, а проверка выполняется
    со строгим лимитом времени и десятками паролей. Формат строки не меняется; в продакшене этот класс не нужен.
    """

    iterations = 100_000


# ---- каркас: django.contrib.auth + сессии, SQLite в памяти ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[
            "django.contrib.contenttypes",
            "django.contrib.auth",
            "django.contrib.sessions",
            __name__ + ".AccessConfig",
        ],
        MIDDLEWARE=[
            "django.contrib.sessions.middleware.SessionMiddleware",
            "django.contrib.auth.middleware.AuthenticationMiddleware",
        ],
        LOGIN_URL="/login/",
        PASSWORD_HASHERS=[__name__ + ".QuickPBKDF2PasswordHasher"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
        USE_TZ=True,
    )
    django.setup()

from django.contrib.auth import authenticate, login, logout
from django.contrib.auth.decorators import login_required, permission_required
from django.contrib.auth.models import Group, Permission, User
from django.contrib.contenttypes.models import ContentType
from django.core.management import call_command
from django.db import connection, models
from django.http import JsonResponse
from django.urls import path


class Article(models.Model):
    title = models.CharField(max_length=100)

    class Meta:
        app_label = "access"


call_command("migrate", verbosity=0)  # таблицы auth, contenttypes, sessions
with connection.schema_editor() as _editor:
    _editor.create_model(Article)

# Право access.publish_article: у приложения нет файла моделей, поэтому создаём запись Permission вручную.
Permission.objects.get_or_create(
    codename="publish_article",
    content_type=ContentType.objects.get_for_model(Article),
    defaults={"name": "Can publish article"},
)


# ---- ВАШ КОД ниже ----
def signup(username, password):
    """Создаёт пользователя с ХЕШИРОВАННЫМ паролем и возвращает его."""
    raise NotImplementedError  # TODO: User.objects.create_user(...)


def hash_summary(encoded):
    """Разбирает строку пароля Django 'алгоритм$итерации$соль$хеш' -> {'algorithm', 'iterations' (int), 'salt_length'}.
    Если строка не состоит ровно из четырёх частей через '$' (например, непригодный пароль '!...'), вернуть None."""
    raise NotImplementedError


def make_editors_group():
    """Группа 'editors' с правом access.publish_article. Повторный вызов не создаёт дубликатов."""
    raise NotImplementedError  # TODO: Group.objects.get_or_create, group.permissions.add(Permission...)


def add_to_editors(user):
    raise NotImplementedError


def login_view(request):
    """POST username, password: успех - 200 {"username": ...} и вход (login); неверные данные - 401 {"error": "invalid credentials"}; не POST - 405."""
    raise NotImplementedError


def logout_view(request):
    """POST: выход (logout), ответ 204; не POST - 405."""
    raise NotImplementedError


def profile(request):
    """Только для вошедших (login_required): {"username": ..., "is_staff": ...}. Аноним получает 302 на /login/?next=/profile/."""
    raise NotImplementedError


def publish(request):
    """Только для вошедших и с правом access.publish_article: {"published": true}. Аноним - 302 на вход, вошедший без права - 403."""
    raise NotImplementedError


urlpatterns = []  # TODO: login/ (login), logout/ (logout), profile/ (profile), publish/ (publish)
