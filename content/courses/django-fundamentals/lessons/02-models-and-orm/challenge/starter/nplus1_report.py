"""Челлендж: исправьте N+1. Однофайловый проект Django с моделями. Блок «каркас» не меняйте; ваша часть - ниже строки «ВАШ КОД»."""
import django
from django.apps import AppConfig
from django.conf import settings


class BlogConfig(AppConfig):
    """Приложение «blog» объявлено прямо в файле: модели получают app_label='blog'."""

    name = __name__
    label = "blog"


# ---- каркас: настройки, модели и таблицы ----
if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[__name__ + ".BlogConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
        USE_TZ=True,
    )
    django.setup()

from django.db import connection, models
from django.db.models import Avg, Count, Max, Min, Prefetch, Sum  # noqa: F401


class Author(models.Model):
    name = models.CharField(max_length=50, unique=True)

    class Meta:
        app_label = "blog"


class Book(models.Model):
    title = models.CharField(max_length=100)
    author = models.ForeignKey(Author, on_delete=models.CASCADE, related_name="books")
    price = models.PositiveIntegerField(help_text="цена в центах")
    year = models.SmallIntegerField()

    class Meta:
        app_label = "blog"


class Review(models.Model):
    book = models.ForeignKey(Book, on_delete=models.CASCADE, related_name="reviews")
    rating = models.SmallIntegerField()

    class Meta:
        app_label = "blog"


def create_tables():
    """Создаёт таблицы напрямую через schema_editor (то же делают миграции)."""
    with connection.schema_editor() as editor:
        for model in (Author, Book, Review):
            editor.create_model(model)


create_tables()


# ---- ВАШ КОД ниже ----
# Обе функции возвращают правильный результат, но делают 1 + N (или 1 + 2N) запросов. Сведите каждую к ОДНОМУ запросу.
def report_lines():
    """['<название> | <автор> | reviews=<n> | avg=<средняя оценка с одним знаком или ->'] по алфавиту названий."""
    lines = []
    for book in Book.objects.order_by("title"):
        ratings = [r.rating for r in book.reviews.all()]
        avg = "-" if not ratings else format(sum(ratings) / len(ratings), ".1f")
        lines.append(f"{book.title} | {book.author.name} | reviews={len(ratings)} | avg={avg}")
    return lines


def author_summary():
    """['<автор>: <книг> books, <отзывов> reviews'] по имени автора, включая авторов без книг."""
    lines = []
    for author in Author.objects.order_by("name"):
        books = author.books.count()
        reviews = Review.objects.filter(book__author=author).count()
        lines.append(f"{author.name}: {books} books, {reviews} reviews")
    return lines


if __name__ == "__main__":
    print(report_lines(), author_summary())
