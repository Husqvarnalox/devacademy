<!-- section: simple -->
## Представление принимает запрос, шаблон рисует страницу

Две части этого урока:

* **Представление** (view) - вызываемый объект, который получает `HttpRequest` и возвращает `HttpResponse`. Его можно написать **функцией** или **классом**. Функция проще для небольших страниц. Класс удобен, когда одна страница по-разному отвечает на `GET` и `POST`, а общее поведение нужно переиспользовать.
* **Шаблон** - текст с «дырками»: `{{ product.name }}` подставляет значение, `{% for %}` повторяет блок, `{% extends %}` берёт общий каркас страницы. Шаблон не пишет логику, а только показывает данные, которые подготовило представление.

Главное правило безопасности: Django **экранирует** всё, что подставляется через `{{ ... }}`. Если пользователь прислал имя `<script>alert(1)</script>`, в страницу попадёт безвредный текст `&lt;script&gt;...`. Это защита от XSS, о причинах которой подробно рассказано в [уроке про кодирование вывода](lesson:security.fundamentals.output-encoding-and-xss). Отключать её (`|safe`) нужно только для текста, который вы сами сделали безопасным.

В этом курсе шаблоны лежат не в файлах, а в словаре Python (загрузчик `locmem`): так весь пример помещается в один файл. В настоящем проекте это обычные файлы `templates/<app>/page.html`, а механизм тот же.

<!-- section: precise -->
## Как это устроено

**Функциональное представление.** `def view(request, **url_kwargs) -> HttpResponse`. Ярлык `render(request, "page.html", context)` загружает шаблон, подставляет контекст (словарь) и возвращает `HttpResponse`. `get_object_or_404` и `raise Http404` превращаются в страницу 404. Декораторы (`require_GET`, `login_required`) оборачивают функцию.

**Классовое представление.** Наследник `django.views.View` определяет методы по именам HTTP-методов: `get`, `post`, `put`, ... В `urlpatterns` кладут не класс, а `MyView.as_view()`: это функция, которая на каждый запрос создаёт **новый экземпляр** класса и вызывает `dispatch()`. `dispatch` выбирает метод по `request.method`; если метода нет, возвращается `405 Method Not Allowed` с заголовком `Allow`, а `OPTIONS` отвечает автоматически. Именованные аргументы адреса попадают в `get(self, request, pk)`. Готовые обобщённые представления (`TemplateView`, `RedirectView`, `ListView`, `DetailView`) устраняют типовой код: `TemplateView.as_view(template_name="about.html", extra_context={...})`. Состояние нельзя хранить в атрибутах класса между запросами (экземпляр новый, а атрибуты класса общие для всех).

**Язык шаблонов.**

* Переменные: `{{ user.name }}`. Поиск точкой идёт по порядку: ключ словаря, атрибут, индекс списка; вызываемое значение вызывается без аргументов. Отсутствующее значение даёт пустую строку (не ошибку).
* Фильтры: `{{ value|lower }}`, `{{ price|floatformat:2 }}`, `{{ items|length }}`, `{{ text|truncatechars:20 }}`, `{{ x|default:"-" }}`.
* Теги: `{% if %}...{% elif %}...{% else %}...{% endif %}`, `{% for x in xs %}...{% empty %}...{% endfor %}` (внутри есть `forloop.counter`, `forloop.first`), `{% with n=items|length %}`, `{% url "name" arg %}` (обратное разрешение по имени маршрута), `{% include "part.html" %}`, `{% comment %}`.
* Шаблоны не умеют произвольный Python: вызывать функции с аргументами, делать сложные вычисления нельзя. Это намеренно: вычисления выполняйте в представлении или в модели.

**Наследование шаблонов.** Базовый шаблон объявляет `{% block name %}...{% endblock %}`. Дочерний начинается с `{% extends "base.html" %}` и переопределяет нужные блоки; непереопределённые берутся из базового, а `{{ block.super }}` вставляет содержимое блока родителя. `extends` должен быть первым тегом шаблона. Так общая шапка, меню и подвал хранятся в одном месте.

**Автоэкранирование.** Двигатель шаблонов заменяет в значениях `& < > " '` на `&amp; &lt; &gt; &quot; &#x27;`. Исключения: значение, помеченное безопасным (`mark_safe`, результат `format_html`), фильтр `|safe` и блок `{% autoescape off %}`. Фильтры вроде `linebreaksbr` и `urlize` сами сначала экранируют вход. Экранирование зависит от **контекста вывода**: оно рассчитано на HTML-текст и значения атрибутов в кавычках, но не защищает данные внутри `<script>` и URL в `href` (например, `javascript:`); для JSON внутри страницы есть фильтр `json_script`.

**Настройка шаблонов.** В настройках `TEMPLATES` задают движок, каталоги (`DIRS`), поиск в приложениях (`APP_DIRS`), **контекстные процессоры** (например, `django.template.context_processors.request`) и загрузчики (`loaders`). В нашем проекте загрузчик один - `locmem`, который читает словарь `имя -> текст шаблона`.

<!-- section: examples -->
## Примеры

**Пример 1. Функция и класс: одна страница двумя способами.** `as_view()` создаёт экземпляр на запрос; класс сам отвечает 405 на непредусмотренный метод.

```python run
import django
from django.conf import settings

if not settings.configured:
    settings.configure(ROOT_URLCONF=__name__, ALLOWED_HOSTS=["testserver"], SECRET_KEY="x", INSTALLED_APPS=[], MIDDLEWARE=[])
    django.setup()

from django.http import HttpResponse
from django.test import Client
from django.urls import path
from django.views import View


def hello_function(request):
    return HttpResponse(f"function: {request.method}")


class HelloView(View):
    greeting = "class"

    def get(self, request):
        return HttpResponse(f"{self.greeting}: GET")

    def post(self, request):
        return HttpResponse(f"{self.greeting}: POST", status=201)


urlpatterns = [path("f/", hello_function), path("c/", HelloView.as_view()), path("c2/", HelloView.as_view(greeting="class2"))]
c = Client()
print(c.get("/f/").content.decode(), c.post("/f/").content.decode())
print(c.get("/c/").content.decode(), c.post("/c/").status_code)
print(c.get("/c2/").content.decode())
r = c.put("/c/")
print(r.status_code, r["Allow"])
print(c.options("/c/")["Allow"])
```
```output
function: GET function: POST
class: GET 201
class2: GET
405 GET, POST, HEAD, OPTIONS
GET, POST, HEAD, OPTIONS
```

**Пример 2. Язык шаблонов.** Поиск точкой, фильтры, `if`, `for ... empty`, `forloop`, `with`. Движок создаётся напрямую.

```python run
from django.conf import settings

settings.configure(USE_I18N=False)  # floatformat читает настройки локализации

from django.template import Context, Engine

engine = Engine(loaders=[("django.template.loaders.locmem.Loader", {
    "t.html": (
        "{{ user.name|upper }} ({{ user.tags.0 }}) {{ user.score|floatformat:1 }} {{ missing }}|{{ missing|default:'-' }}\n"
        "{% for p in products %}{{ forloop.counter }}.{{ p.title }}{% if forloop.last %}.{% else %}, {% endif %}{% empty %}none{% endfor %}\n"
        "{% with n=products|length %}{% if n > 2 %}many{% elif n %}some{% else %}no{% endif %}{% endwith %}"
    )
})])
template = engine.get_template("t.html")
user = {"name": "ann", "tags": ["admin", "dev"], "score": 4.25}
print(template.render(Context({"user": user, "products": [{"title": "A"}, {"title": "B"}]})))
print(template.render(Context({"user": user, "products": []})).splitlines()[1:])
```
```output
ANN (admin) 4.3 |-
1.A, 2.B.
some
['none', 'no']
```

**Пример 3. Автоэкранирование: что экранируется и как это обойти.** `mark_safe` и `|safe` отключают защиту, `format_html` экранирует аргументы, но не свой шаблон.

```python run
from django.template import Context, Engine
from django.utils.html import format_html
from django.utils.safestring import mark_safe

engine = Engine(loaders=[("django.template.loaders.locmem.Loader", {
    "t.html": "{{ x }} | {{ x|safe }} | {% autoescape off %}{{ x }}{% endautoescape %} | {{ x|linebreaksbr }}",
})])
x = "<b>\"R&D\"</b>\n'tag'"
print(engine.get_template("t.html").render(Context({"x": x})))
print(engine.from_string("{{ a }} {{ b }}").render(Context({"a": mark_safe("<i>ok</i>"), "b": "<i>no</i>"})))
print(format_html("<a href='/u/{}/'>{}</a>", "x y", "<script>"))
```
```output
&lt;b&gt;&quot;R&amp;D&quot;&lt;/b&gt;
&#x27;tag&#x27; | <b>"R&D"</b>
'tag' | <b>"R&D"</b>
'tag' | &lt;b&gt;&quot;R&amp;D&quot;&lt;/b&gt;<br>&#x27;tag&#x27;
<i>ok</i> &lt;i&gt;no&lt;/i&gt;
<a href='/u/x y/'>&lt;script&gt;</a>
```

**Пример 4. Наследование шаблонов и `block.super`.** Дочерний шаблон переопределяет блоки, остальное берёт у родителя.

```python run
from django.template import Context, Engine

engine = Engine(loaders=[("django.template.loaders.locmem.Loader", {
    "base.html": "<title>{% block title %}Site{% endblock %}</title><nav>{% block nav %}home{% endblock %}</nav><main>{% block content %}{% endblock %}</main>",
    "page.html": '{% extends "base.html" %}{% block title %}{{ block.super }} - Page{% endblock %}{% block content %}Hello, {{ name }}!{% endblock %}',
    "wide.html": '{% extends "page.html" %}{% block nav %}{{ block.super }} | wide{% endblock %}',
})])
print(engine.get_template("page.html").render(Context({"name": "Ann"})))
print(engine.get_template("wide.html").render(Context({"name": "Bob"})))
```
```output
<title>Site - Page</title><nav>home</nav><main>Hello, Ann!</main>
<title>Site - Page</title><nav>home | wide</nav><main>Hello, Bob!</main>
```

<!-- section: mistakes -->
## Типичные ошибки

* **`|safe` или `mark_safe` на пользовательском тексте.** Это отключает защиту от XSS. Помечайте безопасным только то, что собрали сами (`format_html` с экранируемыми аргументами).
* **Собирать HTML склейкой строк в представлении** (`HttpResponse("<p>" + name + "</p>")`): экранирование не применяется. Используйте шаблон или `format_html`.
* **Данные внутри `<script>` и в `href`.** Автоэкранирование HTML здесь не помогает: для данных в скрипт используйте `json_script`, а ссылки из пользовательского ввода проверяйте по схеме (`https`, `/path`).
* **Логика в шаблоне.** Попытка вызвать метод с аргументами или посчитать что-то сложное. Подготовьте значение в представлении.
* **Состояние в атрибутах классового представления между запросами.** Атрибуты класса общие для всех запросов; пишите в `self` только внутри метода запроса.
* **Забыть, что отсутствующая переменная молча равна пустой строке.** Опечатка `{{ prodcut.name }}` не вызывает ошибку; тестируйте вывод, а в разработке включайте `string_if_invalid` в настройках шаблонов.
* **`extends` не первым тегом** (кроме комментариев): будет `TemplateSyntaxError`.
* **Положить класс, а не `.as_view()`, в `urlpatterns`.** Маршрут получит класс, а не вызываемое представление, и вы увидите ошибку при первом запросе.

<!-- section: summary -->
## Коротко

* Представление: функция или класс (`View` + `as_view()`); метод класса называется как HTTP-метод, лишние методы дают 405 автоматически.
* `render(request, "t.html", ctx)` - шаблон плюс контекст; `Http404` - страница 404.
* В шаблонах: переменные с точкой, фильтры `|`, теги `{% %}`, наследование `extends` и `block` (`block.super` берёт блок родителя).
* Всё в `{{ }}` экранируется автоматически; `|safe`, `mark_safe` и `autoescape off` - осознанное исключение, не для пользовательских данных.
* Автоэкранирование защищает HTML-текст и значения атрибутов в кавычках, но не JavaScript и URL.
