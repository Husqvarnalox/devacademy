<!-- section: simple -->
## Файлы - это не строки

Путь к файлу легко принять за обычную строку: `"data/" + name + ".csv"`. Пока имена простые, это работает, а потом ломается: на Windows разделитель другой, в имени оказывается `..`, лишний `/` склеивается в `//`, а расширение приходится вырезать срезами. Модуль **`pathlib`** описывает путь объектом `Path`: он знает, где имя, где расширение, как соединять части, как найти файлы по шаблону и как прочитать файл.

Рядом работают три модуля:

* **`os`** - низкоуровневые операции операционной системы: текущий каталог, переменные окружения, `os.replace` (атомарная замена), `os.scandir`, `os.walk`. Старый `os.path` делает то же, что `pathlib`, но на строках.
* **`shutil`** - операции над деревьями: копирование (`copytree`), удаление (`rmtree`), перемещение, архивы.
* **`tempfile`** - временные файлы и каталоги, безопасно созданные и автоматически удаляемые.

Основная опасность этого урока - необратимость: `rmtree` не спрашивает подтверждения, неудачная запись способна оставить наполовину записанный файл, а путь из недоверенного ввода может вывести за пределы нужного каталога. Поэтому мы разбираем не только API, но и безопасные шаблоны. Обработку исключений и открытие файлов вы уже знаете из уроков [«Ошибки и файлы»](lesson:python.fundamentals.errors-and-files) и [«Контекстные менеджеры»](lesson:python.advanced.context-managers).

<!-- section: precise -->
## API, шаблоны и ограничения

**Path и PurePath.** `PurePath` (`PurePosixPath`, `PureWindowsPath`) - только вычисления над именами без обращения к диску; `Path` дополнительно умеет обращаться к файловой системе и на текущей платформе превращается в `PosixPath` или `WindowsPath`.

* Соединение: `Path("a") / "b" / "c.txt"` или `Path("a").joinpath("b", "c.txt")`. Если правый операнд **абсолютный**, левая часть отбрасывается: `Path("/srv") / "/etc"` равно `/etc`.
* Части: `name`, `stem`, `suffix`, `suffixes`, `parent`, `parents`, `parts`; изменение: `with_name`, `with_suffix`, `relative_to`, `is_relative_to` (3.9+). `as_posix()` даёт строку с `/`.
* `str(path)` или `os.fspath(path)` - строка пути; большинство функций стандартной библиотеки принимают `Path` напрямую (протокол `os.PathLike`).

**Обращения к диску.**

* `exists()`, `is_file()`, `is_dir()`, `is_symlink()`; `stat()` возвращает размер (`st_size`) и время изменения (`st_mtime`).
* `read_text(encoding=...)`, `write_text`, `read_bytes`, `write_bytes`; всегда указывайте `encoding="utf-8"` для текста, иначе используется кодировка платформы.
* `mkdir(parents=True, exist_ok=True)`, `unlink(missing_ok=True)` (3.8+), `rename`, `replace`, `iterdir()` (порядок не определён - сортируйте).
* `glob("*.py")` - файлы текущего каталога; `rglob("*.py")` или `glob("**/*.py")` - все уровни. Результат - генератор, порядок не гарантирован; сортируйте. В отличие от оболочки, `Path.glob("*")` находит и скрытые файлы (имена с точки).
* `resolve()` - абсолютный путь с раскрытием символических ссылок и `..` (`strict=False` допускает несуществующие компоненты). `absolute()` ссылки не раскрывает.

**Атомарная запись.** Прямой `open(path, "w")` усекает файл сразу; если процесс прервётся посреди записи, останется обрезанный файл. Надёжная схема: 1) создать временный файл **в том же каталоге**; 2) записать данные, `flush()` и при необходимости `os.fsync`; 3) `os.replace(tmp, path)`; 4) при любом сбое удалить временный файл. `os.replace` на одной файловой системе заменяет цель целиком; читатели видят либо старый, либо новый файл. Между файловыми системами атомарность не гарантируется. Это же даёт правило: временный файл рядом с целевым, а не в системном `/tmp`.

**tempfile.** `TemporaryDirectory()` - контекстный менеджер, удаляет каталог с содержимым при выходе; `NamedTemporaryFile` - файл с именем (на Windows нельзя открыть второй раз, пока он открыт, если не `delete=False`); `mkstemp()` возвращает `(fd, путь)`, закрывать и удалять его должны вы; параметр `dir=` задаёт место. Имя создаётся атомарно и непредсказуемо, права ограничены владельцем. `mktemp()` небезопасна и устарела.

**shutil.** `copy`/`copy2` копируют один файл (`copy2` ещё и метаданные); `copytree(src, dst, ignore=shutil.ignore_patterns(...), dirs_exist_ok=False)` - дерево, по умолчанию `dst` не должен существовать (`FileExistsError`); `move`; `rmtree(path)` - рекурсивное удаление без возможности восстановления; `disk_usage`; `make_archive`.

**Path traversal.** Если часть пути приходит извне (имя файла в запросе, элемент архива), `base / name` может указать за пределы `base`. Правило: `resolved = (base / name).resolve()`, затем `resolved.is_relative_to(base.resolve())`, иначе отказ. Этого же требует распаковка архивов: имена внутри не доверенные. Проверка до обращения к диску не заменяет прав доступа: она лишь не даёт программе обработать чужой путь.

**os и os.path.** `os.getcwd()`, `os.environ` (отображение строк), `os.listdir`, `os.scandir` (быстрее, возвращает типы без лишних `stat`), `os.walk`. Новый код, как правило, пишут на `pathlib`; `os` нужен для `replace`, `fsync`, окружения и низкоуровневых операций.

**Ограничения.** Операции над файловой системой не атомарны как последовательность («проверил, что файла нет, и создал» - гонка; используйте режим `"x"` или `exist_ok`). Символические ссылки, регистр имён (macOS и Windows обычно не различают регистр), длина путей и разделители зависят от платформы. Время `st_mtime` имеет конечное разрешение.

<!-- section: examples -->
## Примеры

В примерах каталоги создаются через `TemporaryDirectory(dir=".")`: данные пишутся только в рабочую папку и удаляются при выходе.

**Пример 1. Части пути без обращения к диску.**

```python run
from pathlib import PurePosixPath, PureWindowsPath

p = PurePosixPath("/srv/app/data/report.final.tar.gz")
print(p.name, p.stem, p.suffix, p.suffixes)
print(p.parent, p.parents[1], p.parts[:3])
print(p.with_suffix(".zip"), p.with_name("x.txt"))
print(PurePosixPath("/srv/app") / "static" / "a.css")
print(PurePosixPath("/srv/app") / "/etc/passwd")   # абсолютный операнд отбрасывает левую часть
print(PurePosixPath("data/x.csv").relative_to("data"))
print(PureWindowsPath("C:/Users/me") / "docs", PureWindowsPath("a/b").as_posix())
print(str(p) == "/srv/app/data/report.final.tar.gz", type(str(p)).__name__)
```
```output
report.final.tar.gz report.final.tar .gz ['.final', '.tar', '.gz']
/srv/app/data /srv/app ('/', 'srv', 'app')
/srv/app/data/report.final.tar.zip /srv/app/data/x.txt
/srv/app/static/a.css
/etc/passwd
x.csv
C:\Users\me\docs a/b
True str
```

**Пример 2. Файлы, glob и rglob, stat.**

```python run
import tempfile
from pathlib import Path

with tempfile.TemporaryDirectory(dir=".") as tmp:
    root = Path(tmp)
    (root / "pkg" / "sub").mkdir(parents=True, exist_ok=True)
    (root / "a.py").write_text("print(1)\n", encoding="utf-8")
    (root / "pkg" / "b.py").write_text("x = 2\n", encoding="utf-8")
    (root / "pkg" / "sub" / "c.txt").write_text("hello", encoding="utf-8")

    print(sorted(p.name for p in root.glob("*.py")))
    print(sorted(p.relative_to(root).as_posix() for p in root.rglob("*.py")))
    print(sorted(p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()))
    print((root / "pkg" / "sub" / "c.txt").stat().st_size, (root / "nope").exists())
    print((root / "pkg").is_dir(), (root / "a.py").is_file())
    try:
        (root / "x" / "y.txt").write_text("1")
    except FileNotFoundError:
        print("FileNotFoundError: нет каталога x")
```
```output
['a.py']
['a.py', 'pkg/b.py']
['a.py', 'pkg/b.py', 'pkg/sub/c.txt']
5 False
True True
FileNotFoundError: нет каталога x
```

**Пример 3. Атомарная запись и сбой посреди записи.**

```python run
import os
import tempfile
from pathlib import Path

def atomic_write(path, text):
    path = Path(path)
    fd, tmp = tempfile.mkstemp(dir=path.parent, prefix=path.name + ".", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(text)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp, path)       # атомарная замена на одной файловой системе
    except BaseException:
        Path(tmp).unlink(missing_ok=True)
        raise

with tempfile.TemporaryDirectory(dir=".") as tmp:
    target = Path(tmp) / "config.txt"
    atomic_write(target, "v1\n")
    atomic_write(target, "v2\n")
    print(target.read_text(), [p.name for p in Path(tmp).iterdir()])
    try:
        atomic_write(target, 42)    # не строка: запись упадёт
    except TypeError as exc:
        print("TypeError")
    print(target.read_text(), [p.name for p in Path(tmp).iterdir()])
```
```output
v2
 ['config.txt']
TypeError
v2
 ['config.txt']
```

**Пример 4. copytree, rmtree и защита от выхода из каталога.**

```python run
import shutil
import tempfile
from pathlib import Path

def safe_join(base, *parts):
    base = Path(base).resolve()
    result = base.joinpath(*parts).resolve()
    if not result.is_relative_to(base):
        raise ValueError("выход за пределы базового каталога")
    return result

with tempfile.TemporaryDirectory(dir=".") as tmp:
    base = Path(tmp).resolve()
    src = base / "src"
    (src / "pkg" / "__pycache__").mkdir(parents=True)
    (src / "pkg" / "a.py").write_text("1")
    (src / "pkg" / "__pycache__" / "a.pyc").write_text("x")
    (src / "log.tmp").write_text("t")

    dst = base / "dst"
    shutil.copytree(src, dst, ignore=shutil.ignore_patterns("__pycache__", "*.tmp"))
    print(sorted(p.relative_to(dst).as_posix() for p in dst.rglob("*")))
    try:
        shutil.copytree(src, dst)
    except FileExistsError:
        print("FileExistsError: dst уже есть")

    print(safe_join(base, "dst", "pkg").relative_to(base).as_posix())
    for bad in ("../outside", "/etc/passwd"):
        try:
            safe_join(base, bad)
        except ValueError as exc:
            print("отказ:", bad)

    target = safe_join(base, "dst")
    shutil.rmtree(target)           # необратимо: путь проверен заранее
    print(target.exists())
```
```output
['pkg', 'pkg/a.py']
FileExistsError: dst уже есть
dst/pkg
отказ: ../outside
отказ: /etc/passwd
False
```

<!-- section: mistakes -->
## Типичные ошибки

* **Склейка путей строками.** `base + "/" + name` ломается на разделителях и пустых частях. Используйте `/` и `joinpath`.
* **Присоединение недоверенного компонента без проверки.** `Path(base) / user_input` допускает `..` и абсолютные пути. Сравнивайте `resolve()` результата с `resolve()` базы.
* **Запись напрямую в целевой файл.** При сбое получится обрезанный файл. Пишите во временный файл рядом и `os.replace`.
* **Временный файл в другом каталоге.** `os.replace` через границу файловых систем не атомарна (и может завершиться ошибкой).
* **Забытая очистка временного файла в ветке ошибки.** Используйте `try/except BaseException` либо `TemporaryDirectory`.
* **`tempfile.mktemp` и «случайные» имена.** Между выбором имени и созданием есть гонка; берите `mkstemp` и `TemporaryDirectory`.
* **Текст без `encoding`.** `read_text()` использует кодировку платформы; на Windows она обычно не UTF-8. Указывайте `encoding="utf-8"`.
* **Полагаться на порядок `glob`/`iterdir`.** Порядок не определён; сортируйте перед выводом и сравнением.
* **`rmtree` без проверки.** Пустая переменная, корень проекта или домашний каталог - частые причины потери данных. Проверяйте, что путь внутри ожидаемой базы и не равен ей.
* **Поиск файлов шаблоном `**` без `is_file()`.** В результат попадают и каталоги.
* **`exists()` перед созданием.** Это гонка; используйте `open(..., "x")`, `mkdir(exist_ok=True)`, `unlink(missing_ok=True)`.
* **Ожидание, что `suffix` вернёт всё расширение.** У `archive.tar.gz` это `.gz`; полный набор - `suffixes`.

<!-- section: summary -->
## Итог

* Пути - объекты `Path`, а не строки: `/` для соединения, `name`/`stem`/`suffix` для частей, `glob`/`rglob` и `stat` для обращений к диску. Абсолютный правый операнд отбрасывает левую часть.
* Атомарная запись: временный файл **в том же каталоге**, `flush`+`fsync`, `os.replace`, удаление временного файла при любой ошибке.
* `TemporaryDirectory`, `mkstemp`, `NamedTemporaryFile` создают объекты безопасно; `mktemp` не используйте.
* `copytree` по умолчанию требует несуществующий `dst`; `rmtree` необратим - проверяйте путь заранее.
* Защита от path traversal: `resolve()` и `is_relative_to(base.resolve())`.
* Дальше: [json, csv и struct](lesson:python.stdlib.json-csv-struct) - как превращать данные в файлы и обратно.
