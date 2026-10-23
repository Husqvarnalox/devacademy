<!-- section: simple -->
## Исключения, файлы и модули без привычных костылей

В Python ошибки - это исключения, и это основной способ управления «нештатными» ситуациями, а не редкость. Принятый стиль называется **EAFP** («проще попросить прощения, чем разрешения»): выполните действие и обработайте исключение, вместо того чтобы заранее проверять все условия (стиль **LBYL**, «посмотри, прежде чем прыгнуть», типичный для C и Go). Файлы открывают через `with`, который гарантирует закрытие. Код делят на **модули** - обычные файлы `.py`, которые выполняются при первом `import`.

Зачем это знать: условие «файл существует» может измениться между проверкой и открытием, а пропущенный `close` или `import`, выполняющий действия при загрузке, создают ошибки, которые трудно поймать.

<!-- section: examples -->
## Примеры

**Пример 1. EAFP вместо проверок.**

```python run
def parse_int(text, default=None):
    try:
        return int(text)
    except (ValueError, TypeError):
        return default

print(parse_int("42"), parse_int(" 7 "), parse_int("4_2"), parse_int("abc", -1), parse_int("3.5"), parse_int(None, 0))
```
```output
42 7 42 -1 None 0
```

`int()` сам знает, какие строки допустимы: пробелы по краям и разделитель `_` разрешены (поэтому `"4_2"` даёт 42), а `"3.5"` - `ValueError`. Проверять формат вручную (`isdigit` и т. п.) - значит повторять правила, и обычно неточно. Перехватывайте конкретные исключения, а не все подряд.

**Пример 2. `try / except / else / finally` и цепочка причин.**

```python run
def load(data, key):
    try:
        value = data[key]
    except KeyError as exc:
        raise ValueError(f"нет поля {key!r}") from exc
    else:
        print("найдено")
        return value
    finally:
        print("готово")

try:
    load({}, "a")
except ValueError as err:
    print(err, "<-", type(err.__cause__).__name__)
print(load({"a": 1}, "a"))
```
```output
готово
нет поля 'a' <- KeyError
найдено
готово
1
```

`else` выполняется, если исключения не было; `finally` - всегда, даже при `return` и при исключении. Конструкция `raise ... from exc` сохраняет исходную причину в `__cause__`: в traceback видны обе ошибки.

**Пример 3. Иерархия исключений и порядок `except`.**

```python run
print(issubclass(KeyError, LookupError), issubclass(FileNotFoundError, OSError), issubclass(ValueError, Exception))
try:
    {}["a"]
except KeyError:
    print("KeyError")
except LookupError:
    print("LookupError")
```
```output
True True True
KeyError
```

Исключения образуют иерархию классов. Выполняется первая подходящая ветка `except`, поэтому частные типы ставят выше общих. Пустой `except:` и `except BaseException` перехватывают даже `KeyboardInterrupt`; для общего случая используют `except Exception`.

**Пример 4. Файлы и `with`.**

```python run
import tempfile
from pathlib import Path

with tempfile.TemporaryDirectory() as tmp:
    path = Path(tmp) / "notes.txt"
    path.write_text("один\nдва\n", encoding="utf-8")
    with open(path, encoding="utf-8") as f:
        for line in f:
            print(repr(line.rstrip("\n")))
    print(f.closed)
    try:
        open(Path(tmp) / "missing.txt", encoding="utf-8")
    except FileNotFoundError as exc:
        print(type(exc).__name__, exc.errno)
```
```output
'один'
'два'
True
FileNotFoundError 2
```

Файловый объект итерируется по строкам (с символом конца строки). Блок `with` закрывает файл при выходе, в том числе при исключении. Всегда указывайте `encoding="utf-8"`: умолчание зависит от системы и настроек. `pathlib.Path` склеивает пути оператором `/`.

**Пример 5. Менеджер контекста из стандартной библиотеки.**

```python run
from contextlib import suppress

with suppress(ZeroDivisionError):
    print(1 / 0)
print("дальше")
```
```output
дальше
```

`with` работает с любым объектом, реализующим протокол контекста; `suppress` игнорирует перечисленные исключения. Свои менеджеры вы напишете в [курсе «Python: продвинутый уровень»](lesson:python.advanced.context-managers).

**Пример 6. Модуль выполняется при импорте один раз.**

```python run
import sys
import tempfile
import textwrap
from pathlib import Path

with tempfile.TemporaryDirectory() as tmp:
    Path(tmp, "greet.py").write_text(textwrap.dedent("""
        print("загрузка greet, __name__ =", __name__)

        def hello():
            return "привет"

        if __name__ == "__main__":
            print("запуск как программы")
    """), encoding="utf-8")
    sys.path.insert(0, tmp)
    import greet
    import greet
    print(greet.hello(), "greet" in sys.modules)
    sys.path.remove(tmp)
```
```output
загрузка greet, __name__ = greet
привет True
```

Тело модуля выполняется при первом импорте, объект кэшируется в `sys.modules`, повторный `import` ничего не выполняет. Если файл запущен как программа, его `__name__` равен `"__main__"`; при импорте - имени модуля. Поэтому код запуска прячут под `if __name__ == "__main__":`, а всё остальное остаётся пригодным для импорта и тестов.

<!-- section: precise -->
## Точные правила

* **Выбор ветки:** выполняется первая подходящая `except`; `else` - только без исключения; `finally` - всегда.
* **Перехват:** `except (A, B) as e`. Имя `e` удаляется при выходе из блока.
* **Повторный выброс:** `raise` без аргументов внутри `except` пробрасывает текущее исключение; `raise New(...) from old` задаёт причину, `from None` её скрывает.
* **`return` в `finally`** отменяет исключение и возвращаемое значение - так не пишут.
* **Файлы:** режим `"r"` (по умолчанию), `"w"` (перезапись), `"a"` (добавление); текстовый режим декодирует по `encoding`, бинарный (`"rb"`) отдаёт `bytes`. `read()` читает всё, итерация по файлу - построчно и экономно по памяти.
* **Импорт:** поиск по `sys.path`; первый `import` выполняет файл и кэширует модуль. Циклические импорты возможны, но дают частично инициализированный модуль.
* **`__name__`:** `"__main__"` у запускаемого файла. Программу удобно оформлять функцией `main()` и защитой `if __name__ == "__main__": main()`.
* **Имена:** `import a.b`, `from a import b`, `from a import *` (не рекомендуется). Пакет - каталог с модулями.

<!-- section: mistakes -->
## Типичные ошибки

**1. Слишком широкий `except`.**

```python
try:
    value = int(text)
except:
    value = 0
# скрывает и опечатку в имени, и Ctrl+C
```

Перехватывайте конкретные типы (`ValueError`), а для общего случая - `Exception`, и логируйте причину.

**2. Файл без `with` и без кодировки.**

```python
f = open("data.txt")
text = f.read()
# файл не закрыт при исключении; кодировка зависит от системы
```

Пишите `with open("data.txt", encoding="utf-8") as f:`.

**3. Проверка перед действием вместо исключения.**

```python
if os.path.exists(path):
    f = open(path)   # файл могли удалить между строками
```

Откройте файл и обработайте `FileNotFoundError`.

**4. Код запуска на уровне модуля.**

```python
# tools.py
print(compute())   # выполнится при любом import tools
```

Из-за этого модуль нельзя импортировать в тестах: он сразу что-то печатает или читает. Перенесите код в `main()` под `if __name__ == "__main__":`.

**5. Потерянная причина.** `raise ValueError("ошибка")` внутри `except` без `from exc` всё же показывает контекст, но не помечает его как причину. Явное `from` делает цепочку читаемой.

**6. Файл с именем стандартного модуля.** Файл `random.py` или `json.py` в каталоге проекта затеняет стандартный и ломает чужой импорт (`AttributeError: partially initialized module`). Не называйте свои модули как стандартные.

<!-- section: summary -->
## Итог

* Ловите конкретные исключения (EAFP); `else` и `finally` отделяют успешный путь и очистку.
* Иерархия исключений определяет порядок `except`; `raise ... from` сохраняет причину.
* Файлы открывают через `with` и с явной `encoding`.
* Модуль выполняется при первом импорте; запуск программы прячут за `if __name__ == "__main__"`.

Дальше можно перейти к [функциям как объектам](lesson:python.advanced.functions-as-objects) в продвинутом курсе. Базовые уроки, которые закрывает это упражнение: [Исключения и работа с файлами](lesson:python.fundamentals.errors-and-files) и [Модули и import](lesson:python.fundamentals.modules).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* **Исключения вместо кодов возврата:** как в Java и C#, но без checked-исключений (сигнатура не объявляет, что функция бросает). В Go возвращаемая ошибка - значение, в Python - исключение, и пропустить её труднее, чем игнорировать `err`.
* **Идиома EAFP:** использование исключений в штатном потоке нормально и недорого; `StopIteration`, `KeyError`, `ValueError` встречаются постоянно.
* **`with`** соответствует `try-with-resources` (Java), `using` (C#), `defer` (Go) и `try/finally`; выполняет `__enter__`/`__exit__`.
* **Нет `catch` по условиям:** фильтр делается типом и проверкой внутри блока.
* **Модули:** один файл - один модуль, `import` выполняет код (как `require` в Node.js с кэшем), а не только объявляет зависимость (как `import` в Java). Нет `public/private`: соглашение - имена с `_` приватные.
* **Точка входа:** нет `main` как особого имени; роль `public static void main` играет `if __name__ == "__main__"`.
* **Пакеты и среда:** зависимости ставит `pip` в виртуальное окружение (`python -m venv`), аналог `node_modules`, а не глобальная установка.
