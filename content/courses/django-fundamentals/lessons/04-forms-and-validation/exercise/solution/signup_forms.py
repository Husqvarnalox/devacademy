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
    password = forms.CharField(widget=forms.PasswordInput)
    password2 = forms.CharField(widget=forms.PasswordInput)

    class Meta:
        model = Member
        fields = ["username", "email", "age"]

    def clean_username(self):
        username = self.cleaned_data["username"].strip().lower()
        if not USERNAME_RE.fullmatch(username):
            raise ValidationError("Invalid username", code="invalid_username")
        return username

    def clean_email(self):
        return self.cleaned_data["email"].lower()

    def clean_age(self):
        age = self.cleaned_data["age"]
        if age < 18:
            raise ValidationError("You must be at least 18", code="too_young")
        return age

    def clean_password(self):
        password = self.cleaned_data["password"]
        errors = []
        if len(password) < 8:
            errors.append(ValidationError("Too short", code="too_short"))
        if not any(ch.isdigit() for ch in password):
            errors.append(ValidationError("Needs a digit", code="no_digit"))
        if errors:
            raise ValidationError(errors)
        return password

    def clean(self):
        cleaned = super().clean()
        p1, p2 = cleaned.get("password"), cleaned.get("password2")
        if p1 is not None and p2 is not None and p1 != p2:
            self.add_error("password2", ValidationError("Passwords do not match", code="password_mismatch"))
        return cleaned


@ensure_csrf_cookie
@require_http_methods(["GET", "POST"])
def register(request):
    if request.method == "GET":
        return HttpResponse("register form")
    form = RegistrationForm(request.POST)
    if not form.is_valid():
        errors = {field: [e["code"] for e in items] for field, items in form.errors.get_json_data().items()}
        return JsonResponse({"errors": errors}, status=400)
    member = form.save()
    return JsonResponse({"id": member.pk, "username": member.username}, status=201)


urlpatterns = [path("register/", register, name="register")]
