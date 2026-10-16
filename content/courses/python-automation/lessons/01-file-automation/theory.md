<!-- section: simple -->
## Скрипт, которому можно доверить свои файлы

Скрипт, который наводит порядок в папке «Загрузки», выглядит безобидно, пока не случится одно из трёх: он перезаписал чужой файл с таким же именем, он запустился второй раз и «разложил» уже разложенное, или он пошёл по ярлыку (симлинку) в системную папку. Надёжная автоматизация файлов - это не умение вызвать `shutil.move`, а набор привычек, которые не дают этому случиться.

Четыре привычки этого урока:

* **Сначала план, потом действие.** Одна функция только смотрит на диск и возвращает список «что откуда куда». Другая выполняет этот список. Сухой прогон (`dry-run`) - это просто «показать план и остановиться».
* **Идемпотентность.** Повторный запуск на уже обработанных данных ничего не меняет. Скрипт можно поставить на расписание и не бояться дублей.
* **Не терять данные.** Если имя занято, файл получает другое имя (`report (1).pdf`) или пропускается с сообщением, но никогда не затирает существующий.
* **Не выходить за пределы корня.** Симлинки не обрабатываются как обычные файлы, а целевые пути проверяются после `resolve()`.

Инструменты вы уже знаете из урока [«pathlib, os, shutil, tempfile»](lesson:python.stdlib.pathlib-os-shutil-tempfile); здесь они складываются в безопасный шаблон. Следующий урок применяет тот же подход к данным: [пакетная обработка CSV](lesson:python.automation.batch-processing-csv-reports).

<!-- section: precise -->
## Что именно делает скрипт надёжным

**Планирование и применение.** `plan_moves(root)` возвращает список пар `(src, dst)` и не меняет файловую систему; `apply(plan)` перемещает. Преимущества: план можно напечатать, сохранить, проверить тестом и показать пользователю до запуска. Правило: боевой режим и dry-run обязаны использовать *один и тот же* планировщик.

**Классификация.** По расширению: `os.path.splitext(name)[1]` - только последнее расширение (`archive.tar.gz` даёт `.gz`), приводите к нижнему регистру, пустое расширение - в папку `other`. По дате: время изменения `st_mtime` переводится в UTC (`datetime.fromtimestamp(ts, timezone.utc)`), чтобы результат не зависел от часового пояса машины.

**Идемпотентность.** Её даёт форма плана: планировщик обходит только корень (`os.scandir(root)`, без рекурсии) и берёт обычные файлы. Файл, уже лежащий в подпапке, в план не попадает, значит второй запуск строит пустой план. Если же скрипт обходит дерево рекурсивно, нужно явно исключить целевые папки или проверять, что файл уже на своём месте. Идемпотентность всегда проверяется тестом: «запустить дважды, сравнить снимки каталога».

**Коллизии.** Имя назначения может быть занято уже существующим файлом или другим файлом из того же плана. Решение - функция `unique_name(name, taken)`: множество `taken` содержит имена, уже лежащие в папке, и пополняется спланированными. Альтернативы: пропустить файл и записать это в отчёт; сравнить содержимое хешем и удалить дубликат (только осознанно и с отдельным флагом).

**Перемещение без перезаписи.** `os.replace(src, dst)` молча заменяет существующий `dst`. Поэтому перед ней проверяют `os.path.lexists(dst)` (`lexists` видит и битые симлинки). Между проверкой и переименованием остаётся окно гонки (TOCTOU): другой процесс успеет создать `dst`. Для локальной утилиты, выполняемой одним процессом, это допустимо; если конкурентные записи возможны, создавайте результат атомарно: новый файл - `os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)`, перемещение без перезаписи - `os.link(src, dst)` (падает с `FileExistsError`, если `dst` есть) и затем `os.unlink(src)`. Атомарность `os.replace` гарантируется только внутри одной файловой системы; между разными системами выдаст `OSError`, а `shutil.move` скопирует и удалит - это уже не атомарно, и при сбое может остаться копия.

**Симлинки и границы корня.** `entry.is_symlink()` для записи `os.scandir` определяет ссылку, не переходя по ней; `entry.is_file(follow_symlinks=False)` не принимает ссылку за файл. Для произвольного пути используйте проверку после `resolve()`: `Path(candidate).resolve().relative_to(Path(root).resolve())` поднимает `ValueError`, если путь ведёт за пределы корня. Строковая проверка `str(path).startswith(str(root))` не заменяет её: `root/link/x` выглядит внутренним, но ведёт наружу. Если целевая подпапка - симлинк, файлы в неё не перемещают.

**Другие защитные приёмы.** Не обрабатывайте скрытые и служебные файлы без явного флага; ограничьте скрипт одним корнем, переданным явно; печатайте итог («перемещено 12, пропущено 3»); при ошибке на одном файле решите заранее: прервать или продолжить и собрать ошибки (для пакетной работы обычно второе).

<!-- section: examples -->
## Примеры

Примеры создают временный каталог внутри рабочей папки и удаляют его при выходе.

**Пример 1. План, сухой прогон, применение и идемпотентность.**

```python run
import os
import tempfile
from pathlib import Path


def plan_moves(root):
    plan = []
    for entry in sorted(os.scandir(root), key=lambda e: e.name):
        if entry.is_symlink() or not entry.is_file(follow_symlinks=False):
            continue
        ext = os.path.splitext(entry.name)[1].lstrip(".").lower() or "other"
        plan.append((entry.name, f"{ext}/{entry.name}"))
    return plan


def organize(root, dry_run=False):
    plan = plan_moves(root)
    if not dry_run:
        for src, dst in plan:
            (Path(root) / dst).parent.mkdir(exist_ok=True)
            os.replace(Path(root) / src, Path(root) / dst)
    return plan


with tempfile.TemporaryDirectory(dir=".") as tmp:
    for name in ["a.TXT", "b.csv", "notes"]:
        (Path(tmp) / name).write_text(name)
    print("dry-run:", organize(tmp, dry_run=True))
    print("на месте:", sorted(os.listdir(tmp)))
    print("первый запуск:", organize(tmp))
    print("второй запуск:", organize(tmp))
    print("итог:", sorted(str(p.relative_to(tmp)) for p in Path(tmp).rglob("*")))
```
```output
dry-run: [('a.TXT', 'txt/a.TXT'), ('b.csv', 'csv/b.csv'), ('notes', 'other/notes')]
на месте: ['a.TXT', 'b.csv', 'notes']
первый запуск: [('a.TXT', 'txt/a.TXT'), ('b.csv', 'csv/b.csv'), ('notes', 'other/notes')]
второй запуск: []
итог: ['csv', 'csv/b.csv', 'other', 'other/notes', 'txt', 'txt/a.TXT']
```

**Пример 2. Коллизия имён и безопасное перемещение.**

```python run
import os
import tempfile
from pathlib import Path


def unique_name(name, taken):
    if name not in taken:
        return name
    stem, ext = os.path.splitext(name)
    n = 1
    while f"{stem} ({n}){ext}" in taken:
        n += 1
    return f"{stem} ({n}){ext}"


def safe_move(src, dst):
    if os.path.lexists(dst):
        raise FileExistsError(dst)
    os.replace(src, dst)


taken = {"a.txt", "a (1).txt"}
print(unique_name("a.txt", taken), unique_name("b.txt", taken))

with tempfile.TemporaryDirectory(dir=".") as tmp:
    root = Path(tmp)
    (root / "a.txt").write_text("новый")
    (root / "txt").mkdir()
    (root / "txt" / "a.txt").write_text("старый")
    try:
        safe_move(root / "a.txt", root / "txt" / "a.txt")
    except FileExistsError:
        print("отказ: целевой файл существует")
    print((root / "txt" / "a.txt").read_text(), (root / "a.txt").exists())
    new = unique_name("a.txt", set(os.listdir(root / "txt")))
    safe_move(root / "a.txt", root / "txt" / new)
    print(sorted(os.listdir(root / "txt")))
```
```output
a (2).txt b.txt
отказ: целевой файл существует
старый True
['a (1).txt', 'a.txt']
```

**Пример 3. Граница корня и симлинки.**

```python run
import os
import tempfile
from pathlib import Path


def is_within(root, candidate):
    try:
        Path(candidate).resolve().relative_to(Path(root).resolve())
        return True
    except ValueError:
        return False


with tempfile.TemporaryDirectory(dir=".") as tmp:
    base = Path(tmp)
    (base / "root").mkdir()
    (base / "outside").mkdir()
    (base / "outside" / "secret.txt").write_text("secret")
    (base / "root" / "ok.txt").write_text("ok")
    try:
        os.symlink(base / "outside", base / "root" / "link")
        linked = True
    except OSError:
        linked = False
    print("ok.txt внутри:", is_within(base / "root", base / "root" / "ok.txt"))
    print("../outside внутри:", is_within(base / "root", base / "root" / ".." / "outside"))
    if linked:
        print("через симлинк внутри:", is_within(base / "root", base / "root" / "link" / "secret.txt"))
        entry = next(e for e in os.scandir(base / "root") if e.name == "link")
        print("is_symlink:", entry.is_symlink(), "is_file:", entry.is_file(follow_symlinks=False))
```
```output
ok.txt внутри: True
../outside внутри: False
через симлинк внутри: False
is_symlink: True is_file: False
```

<!-- section: mistakes -->
## Типичные ошибки

* **Нет сухого прогона.** Первый запуск на реальных данных - единственная проверка. Всегда добавляйте `--dry-run` и по умолчанию делайте его безопасным вариантом для новых пользователей.
* **Два разных кода для dry-run и боевого режима.** План и действие расходятся. Используйте один планировщик.
* **`shutil.move` или `os.replace` без проверки назначения.** Существующий файл молча затирается.
* **Рекурсивный обход, захватывающий целевые папки.** Скрипт перекладывает уже разложенные файлы и не идемпотентен.
* **Расширение по `name.split(".")[-1]`.** Для файла без точки получится всё имя; для `.bashrc` - тоже. Используйте `os.path.splitext`.
* **Время по локальному часовому поясу.** Один и тот же файл попадает в разные месяцы на разных машинах; переводите в UTC или задавайте зону явно.
* **Проверка границ строками.** `startswith(root)` пропускает `rootkit/..` и симлинки. Используйте `resolve()` и `relative_to`.
* **`os.path.exists` вместо `lexists`.** Битый симлинк на месте назначения не виден, и `os.replace` его заменит.
* **Молчаливое проглатывание ошибок.** Пропущенные файлы нужно показывать в отчёте; иначе «успех» скрывает потери.
* **Автоматическое удаление «дубликатов».** Удаление - отдельная операция с явным флагом, не побочный эффект перемещения.

<!-- section: summary -->
## Итог

* Разделяйте планирование (чистое чтение) и применение (изменение диска); dry-run - это план без применения.
* Идемпотентность следует из формы плана и проверяется тестом «запустить дважды».
* Коллизии решайте уникальным именем или пропуском, не перезаписью; перед `os.replace` проверяйте `os.path.lexists`, а при конкуренции используйте `O_EXCL` и `os.link`.
* Не следуйте симлинкам: `is_symlink()` для записей, `resolve()` и `relative_to` для путей; атомарность `os.replace` - только внутри одной файловой системы.
* Дальше: [пакетная обработка CSV](lesson:python.automation.batch-processing-csv-reports) - потоковое чтение, валидация строк и отчёт об ошибках.
