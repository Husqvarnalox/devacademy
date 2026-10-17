<!-- section: simple -->
## ORM: таблицы как классы Python

Вместо SQL-строк вы описываете **модель** - класс с полями. Django знает, как превратить его в таблицу, а вызовы вроде `Book.objects.filter(price__lt=1000)` - в `SELECT ... WHERE price < 1000`. Результат приходит обратно в виде объектов Python. Это ORM (object-relational mapper); знание [SQL из курса по базам данных](lesson:databases.sql.joins) здесь не заменяется, а используется: чтобы понять, сколько запросов делает ваш код, нужно думать на языке SQL.

Три идеи этого урока:

* **Модель = таблица.** Поле класса = столбец, `ForeignKey` = внешний ключ. Изменение модели требует **миграции** - записанного списка операций над схемой.
* **QuerySet ленивый.** `Book.objects.filter(...)` не обращается к базе. Запрос уходит только тогда, когда результат нужен: при переборе, `list()`, `len()`, индексе, `bool()`. Цепочки `filter().exclude().order_by()` строят один запрос.
* **Главная ловушка - N+1.** Цикл по 100 книгам с `book.author.name` делает 1 запрос на книги и ещё 100 на авторов. `select_related` и `prefetch_related` превращают это в 1-2 запроса.

<!-- section: precise -->
## Как это устроено

**Модель и поля.** Класс наследует `django.db.models.Model`. Распространённые поля: `CharField(max_length=...)`, `TextField`, `IntegerField`, `PositiveIntegerField`, `BooleanField`, `DateTimeField`, `DecimalField` (деньги - не `FloatField`), `ForeignKey(Other, on_delete=..., related_name=...)`, `ManyToManyField`. Параметры `null=True` (в базе допустим NULL), `blank=True` (форма может оставить пустым), `default=`, `unique=True`, `db_index=True`. Первичный ключ `id` добавляется автоматически (тип задаёт `DEFAULT_AUTO_FIELD`). `on_delete`: `CASCADE` (удалить зависимые), `PROTECT` (запретить), `SET_NULL`. `related_name` называет обратную связь: `author.books`.

**Приложение и таблицы.** Модель принадлежит приложению (`app_label`). В полноценном проекте приложение - пакет, а его `models.py` подхватывается автоматически; здесь приложение - класс `AppConfig` в том же файле, а `class Meta: app_label = "..."` связывает модель с ним. Таблицы создаёт **миграция**: `makemigrations` сравнивает модели с последним состоянием и записывает операции (`CreateModel`, `AddField`) в файлы пакета `migrations`, а `migrate` выполняет их и фиксирует в таблице `django_migrations`. Под капотом каждая операция вызывает **schema editor**, который генерирует DDL конкретной базы. В однофайловых упражнениях миграций нет, и таблицы создаются тем же `connection.schema_editor().create_model(Model)`. Это нормально для обучения и тестов, но не для продакшена: там схему ведут миграции, их хранят в репозитории вместе с кодом и применяют на каждом окружении.

**Выборки.** `Model.objects` - менеджер. Методы, возвращающие QuerySet (ленивые): `all`, `filter`, `exclude`, `order_by`, `values`, `values_list`, `annotate`, `select_related`, `prefetch_related`, срезы `qs[:10]`. Методы, выполняющие запрос: `get` (ровно одна строка, иначе `DoesNotExist` / `MultipleObjectsReturned`), `first`, `count`, `exists`, `aggregate`, `create`, `update`, `delete`, `bulk_create`. Условия записываются «двойным подчёркиванием»: `price__lte=1000`, `title__icontains="py"`, `author__name="Ann"` (переход по связи = JOIN). `Q(a) | Q(b)` строит OR, `~Q(a)` - NOT, `F("price")` ссылается на столбец в выражении (`filter(price__gt=F("cost"))`).

**Кэш QuerySet.** Первое вычисление QuerySet сохраняет строки в нём; повторный перебор того же объекта запроса не делает. Но `qs.filter(...)` создаёт **новый** QuerySet с пустым кэшем. `count()` и `exists()` отправляют лёгкий запрос, если QuerySet ещё не вычислен; для уже вычисленного `len(qs)` бесплатен.

**annotate и aggregate.** `aggregate(total=Sum("price"))` сворачивает весь набор в один словарь. `annotate(n=Count("books"))` добавляет вычисляемое поле к **каждому** объекту (это `GROUP BY` по модели). Агрегаты: `Count`, `Sum`, `Avg`, `Min`, `Max`. Для авторов без книг `Avg` даёт `None`, а `Count` - 0. Если в одном `annotate` вы агрегируете по двум разным связям, соединения перемножают строки: `Count("books", distinct=True)` нужен, чтобы не получить завышенные числа.

**N+1 и способы лечения.**

* `select_related("author")` - один запрос с JOIN; подходит для `ForeignKey` и `OneToOne` (связь «к одному»).
* `prefetch_related("books")` - отдельный запрос на связанные строки и сборка в Python; подходит для «ко многим» (обратный `ForeignKey`, `ManyToMany`). Всего запросов: 1 + число префетчей. Если нужен фильтр или порядок на связанных объектах, используйте `Prefetch("books", queryset=Book.objects.order_by("title"))`.
* `annotate`/`aggregate`, если нужны только числа: сосчитайте в базе, а не по загруженным объектам.
* `values()` / `values_list()` и `only()` - когда не нужны полные объекты.

**Как считать запросы.** `django.test.utils.CaptureQueriesContext(connection)` фиксирует все SQL-запросы внутри блока: `len(ctx)` - число, `ctx.captured_queries` - тексты. В тестах Django это же делает `assertNumQueries(n)`. Проверка числа запросов - обычный способ закрепить, что N+1 не вернётся.

<!-- section: examples -->
## Примеры

**Пример 1. Модель, SQL таблицы и «миграция».** Schema editor может не выполнять DDL, а только собрать SQL: это то, что миграция `CreateModel` делает для вашей базы.

```python run
import django
from django.apps import AppConfig
from django.conf import settings


class ShopConfig(AppConfig):
    name = __name__
    label = "shop"


if not settings.configured:
    settings.configure(
        INSTALLED_APPS=[__name__ + ".ShopConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    )
    django.setup()

from django.db import connection, models


class Author(models.Model):
    name = models.CharField(max_length=50, unique=True)

    class Meta:
        app_label = "shop"


class Book(models.Model):
    title = models.CharField(max_length=100)
    author = models.ForeignKey(Author, on_delete=models.CASCADE, related_name="books")
    price = models.PositiveIntegerField(default=0)

    class Meta:
        app_label = "shop"


with connection.schema_editor(collect_sql=True) as editor:
    editor.create_model(Author)
    editor.create_model(Book)
for statement in editor.collected_sql:
    print(statement)
print([f.name for f in Book._meta.get_fields()])
print(Book._meta.get_field("author").remote_field.related_name)
```
```output
CREATE TABLE "shop_author" ("id" integer NOT NULL PRIMARY KEY AUTOINCREMENT, "name" varchar(50) NOT NULL UNIQUE);
CREATE TABLE "shop_book" ("id" integer NOT NULL PRIMARY KEY AUTOINCREMENT, "title" varchar(100) NOT NULL, "author_id" bigint NOT NULL REFERENCES "shop_author" ("id") DEFERRABLE INITIALLY DEFERRED, "price" integer unsigned NOT NULL CHECK ("price" >= 0));
CREATE INDEX "shop_book_author_id_82b8ab51" ON "shop_book" ("author_id");
['id', 'title', 'author', 'price']
books
```

**Пример 2. Лень QuerySet: когда уходит запрос.** Цепочка не обращается к базе; `CaptureQueriesContext` считает запросы. Кэш работает на одном объекте QuerySet, но не на новом.

```python run
import django
from django.apps import AppConfig
from django.conf import settings


class ShopConfig(AppConfig):
    name = __name__
    label = "shop"


if not settings.configured:
    settings.configure(
        INSTALLED_APPS=[__name__ + ".ShopConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    )
    django.setup()

from django.db import connection, models

from django.test.utils import CaptureQueriesContext


class Book(models.Model):
    title = models.CharField(max_length=100)
    price = models.PositiveIntegerField(default=0)

    class Meta:
        app_label = "shop"


with connection.schema_editor() as editor:
    editor.create_model(Book)
Book.objects.bulk_create([Book(title=t, price=p) for t, p in [("Alpha", 10), ("Beta", 20), ("Gamma", 30)]])

with CaptureQueriesContext(connection) as ctx:
    qs = Book.objects.filter(price__gte=20).exclude(title="Gamma").order_by("-price")
    print("после цепочки:", len(ctx))
    print(qs.query)
    print([b.title for b in qs], "-> запросов:", len(ctx))
    print(len(qs), "-> запросов:", len(ctx))
    print(len(qs.filter(price=20)), "-> запросов:", len(ctx))
    print(Book.objects.count(), Book.objects.filter(price__gt=99).exists(), "-> запросов:", len(ctx))
```
```output
после цепочки: 0
SELECT "shop_book"."id", "shop_book"."title", "shop_book"."price" FROM "shop_book" WHERE ("shop_book"."price" >= 20 AND NOT ("shop_book"."title" = Gamma)) ORDER BY "shop_book"."price" DESC
['Beta'] -> запросов: 1
1 -> запросов: 1
1 -> запросов: 2
3 False -> запросов: 4
```

**Пример 3. get, DoesNotExist, annotate и aggregate.** Авторы без книг остаются в выборке (LEFT JOIN): `Count` даёт 0, `Avg` - `None`.

```python run
import django
from django.apps import AppConfig
from django.conf import settings


class ShopConfig(AppConfig):
    name = __name__
    label = "shop"


if not settings.configured:
    settings.configure(
        INSTALLED_APPS=[__name__ + ".ShopConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    )
    django.setup()

from django.db import connection, models

from django.db.models import Avg, Count, F, Max, Q, Sum


class Author(models.Model):
    name = models.CharField(max_length=50)

    class Meta:
        app_label = "shop"


class Book(models.Model):
    title = models.CharField(max_length=100)
    author = models.ForeignKey(Author, on_delete=models.CASCADE, related_name="books")
    price = models.PositiveIntegerField()
    cost = models.PositiveIntegerField(default=0)

    class Meta:
        app_label = "shop"


with connection.schema_editor() as editor:
    editor.create_model(Author)
    editor.create_model(Book)
ann, bob, _ = (Author.objects.create(name=n) for n in ("Ann", "Bob", "Cid"))
Book.objects.create(title="Alpha", author=ann, price=100, cost=40)
Book.objects.create(title="Beta", author=bob, price=300, cost=350)
Book.objects.create(title="Gamma", author=bob, price=200, cost=50)

print([(a.name, a.n, a.avg) for a in Author.objects.annotate(n=Count("books"), avg=Avg("books__price")).order_by("name")])
print(Book.objects.aggregate(total=Sum("price"), top=Max("price")))
print(list(Book.objects.filter(price__lt=F("cost")).values_list("title", flat=True)))
print(list(Book.objects.filter(Q(author__name="Ann") | Q(price__gte=300)).order_by("title").values_list("title", flat=True)))
try:
    Author.objects.get(name="Zed")
except Author.DoesNotExist:
    print("DoesNotExist")
try:
    Book.objects.get(author__name="Bob")
except Book.MultipleObjectsReturned:
    print("MultipleObjectsReturned")
```
```output
[('Ann', 1, 100.0), ('Bob', 2, 250.0), ('Cid', 0, None)]
{'total': 600, 'top': 300}
['Beta']
['Alpha', 'Beta']
DoesNotExist
MultipleObjectsReturned
```

**Пример 4. N+1 и два лекарства.** Одни и те же данные, три способа обойти их. Считаем запросы.

```python run
import django
from django.apps import AppConfig
from django.conf import settings


class ShopConfig(AppConfig):
    name = __name__
    label = "shop"


if not settings.configured:
    settings.configure(
        INSTALLED_APPS=[__name__ + ".ShopConfig"],
        DATABASES={"default": {"ENGINE": "django.db.backends.sqlite3", "NAME": ":memory:"}},
        DEFAULT_AUTO_FIELD="django.db.models.BigAutoField",
    )
    django.setup()

from django.db import connection, models

from django.test.utils import CaptureQueriesContext


class Author(models.Model):
    name = models.CharField(max_length=50)

    class Meta:
        app_label = "shop"


class Book(models.Model):
    title = models.CharField(max_length=100)
    author = models.ForeignKey(Author, on_delete=models.CASCADE, related_name="books")

    class Meta:
        app_label = "shop"


with connection.schema_editor() as editor:
    editor.create_model(Author)
    editor.create_model(Book)
for i in range(5):
    author = Author.objects.create(name=f"A{i}")
    Book.objects.create(title=f"B{i}a", author=author)
    Book.objects.create(title=f"B{i}b", author=author)


def queries(label, fn):
    with CaptureQueriesContext(connection) as ctx:
        fn()
    print(f"{label}: запросов {len(ctx)}")


queries("N+1", lambda: [b.author.name for b in Book.objects.all()])
queries("select_related", lambda: [b.author.name for b in Book.objects.select_related("author")])
queries("N+1 обратная связь", lambda: [[b.title for b in a.books.all()] for a in Author.objects.all()])
queries("prefetch_related", lambda: [[b.title for b in a.books.all()] for a in Author.objects.prefetch_related("books")])
```
```output
N+1: запросов 11
select_related: запросов 1
N+1 обратная связь: запросов 6
prefetch_related: запросов 2
```

<!-- section: mistakes -->
## Типичные ошибки

* **N+1 в цикле и в шаблоне.** `{{ book.author.name }}` в шаблоне, который перебирает книги, делает запрос на каждую строку. Исправление - `select_related` во view. Обнаруживайте проблему тестом с `assertNumQueries` / `CaptureQueriesContext`.
* **Вычислять QuerySet ради проверки.** `if Book.objects.filter(...):` загружает все строки; для «есть ли хоть одна» используйте `.exists()`, для числа - `.count()`. Если же QuerySet уже вычислен, `len(qs)` запроса не делает.
* **Фильтровать в Python вместо базы.** `[b for b in Book.objects.all() if b.price < 100]` читает всю таблицу; пишите `filter(price__lt=100)`.
* **Путать `null` и `blank`.** `blank` относится к валидации форм, `null` - к хранению NULL. Для строк обычно `blank=True` без `null=True` (иначе два «пустых» значения: `""` и `NULL`).
* **Ждать, что `filter()` на `prefetch_related` использует кэш.** Любой `filter`/`order_by` поверх `obj.books.all()` создаёт новый запрос и обходит префетч; задавайте условия внутри `Prefetch(queryset=...)`.
* **Перемножение строк при двух агрегатах по разным связям** без `distinct=True`.
* **Деньги во `FloatField`**: используйте `DecimalField` или целые копейки/центы.
* **Менять модель без миграции** (в реальном проекте): база и модель расходятся, и запросы падают с `OperationalError`.

<!-- section: summary -->
## Коротко

* Модель = таблица, `QuerySet` ленив: запрос уходит при переборе, `list()`, `len()`, индексе; цепочки строят один запрос.
* Методы `filter`, `exclude`, `annotate`, `order_by` ленивые; `get`, `count`, `exists`, `aggregate`, `create` выполняют запрос.
* `annotate` считает по объектам (GROUP BY), `aggregate` - по всему набору; `Count(..., distinct=True)` при нескольких связях.
* N+1 лечат `select_related` («к одному», JOIN) и `prefetch_related` («ко многим», +1 запрос); число запросов проверяют `CaptureQueriesContext` или `assertNumQueries`.
* Миграции - записанные операции над схемой; в однофайловых упражнениях таблицы создаёт `schema_editor`.
