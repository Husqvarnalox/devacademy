"""Однофайловый проект Django с моделями. Блок «каркас» не меняйте; ваша часть - ниже строки «ВАШ КОД»."""
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
def titles_by_author(name):
    """Названия книг автора с именем name, по алфавиту (list[str]). Один запрос."""
    return list(Book.objects.filter(author__name=name).order_by("title").values_list("title", flat=True))


def affordable_titles(max_price, exclude_author):
    """Названия книг с price <= max_price, кроме книг автора exclude_author, по алфавиту (list[str]). Один запрос."""
    qs = Book.objects.filter(price__lte=max_price).exclude(author__name=exclude_author)
    return list(qs.order_by("title").values_list("title", flat=True))


def author_stats():
    """[(имя, число книг, средняя цена или None)] для ВСЕХ авторов, по имени. Один запрос."""
    qs = Author.objects.annotate(n=Count("books"), avg=Avg("books__price")).order_by("name")
    return [(a.name, a.n, a.avg) for a in qs]


def price_summary():
    """{'count', 'min', 'max', 'total'} по всем книгам одним запросом (для пустой таблицы count=0, остальное None)."""
    return Book.objects.aggregate(count=Count("id"), min=Min("price"), max=Max("price"), total=Sum("price"))


def cheap_books():
    """Ленивый QuerySet книг от дешёвых к дорогим (при равенстве цены - по id). Запрос не выполняется до обращения."""
    return Book.objects.order_by("price", "id")


def book_lines():
    """['<название> (<автор>)'] по алфавиту названий. Ровно один запрос, сколько бы ни было книг."""
    return [f"{b.title} ({b.author.name})" for b in Book.objects.select_related("author").order_by("title")]


def catalog():
    """{имя автора: [названия по алфавиту]} для всех авторов (порядок ключей - по имени). Ровно два запроса."""
    authors = Author.objects.order_by("name").prefetch_related(Prefetch("books", queryset=Book.objects.order_by("title")))
    return {a.name: [b.title for b in a.books.all()] for a in authors}


if __name__ == "__main__":
    ann = Author.objects.create(name="Ann")
    Book.objects.create(title="Alpha", author=ann, price=1000, year=2001)
    print(titles_by_author("Ann"), price_summary())
