<!-- section: simple -->
## Запускать чужие программы и вести журнал

Две задачи встречаются почти в любом реальном проекте.

* **`subprocess`** запускает другую программу (`git`, `ffmpeg`, другой интерпретатор Python), передаёт ей аргументы, получает вывод и код завершения. Главная опасность - построить команду из строки с данными пользователя.
* **`logging`** записывает события работы программы: что произошло, когда, насколько это важно и откуда. Это замена россыпи `print`: уровни можно включать и выключать, записи можно направлять в консоль, файл или внешнюю систему, не меняя код, который их порождает.

В уроке [«Ошибки и файлы»](lesson:python.fundamentals.errors-and-files) вы работали с исключениями, а в [«Менеджерах контекста»](lesson:python.advanced.context-managers) видели, как гарантируется освобождение ресурсов. Здесь оба знания пригодятся: процессы нужно останавливать по таймауту, а исключения нужно не терять, а записывать вместе с трассировкой.

<!-- section: precise -->
## Контракты subprocess и logging

**`subprocess.run(args, *, capture_output=False, text=False, timeout=None, check=False, cwd=None, env=None, input=None, shell=False)`** запускает процесс, ждёт завершения и возвращает `CompletedProcess` с полями `args`, `returncode`, `stdout`, `stderr`.

* `args` - **список** `["prog", "arg1", ...]`: оболочка не участвует, каждый элемент приходит программе ровно одним аргументом.
* `capture_output=True` перехватывает stdout и stderr; `text=True` декодирует их в строки (кодировка по умолчанию - локальная, при необходимости задайте `encoding="utf-8"`).
* `check=True` превращает ненулевой код в `CalledProcessError` (с полями `returncode`, `cmd`, `stdout`, `stderr`). Без `check` исключения нет, код нужно проверять самому. Если программа не найдена - `FileNotFoundError`.
* `timeout=секунды`: по истечении процесс убивается и бросается `TimeoutExpired`. Без timeout зависший процесс заблокирует вашу программу навсегда.
* `input="текст"` подаёт данные в stdin.
* Для запуска Python-кода текущим интерпретатором используйте `sys.executable`, а не строку `"python"`: так вы запустите именно тот интерпретатор (и то же окружение), что исполняет вашу программу.

**shell=True.** Строка исполняется оболочкой, поэтому `;`, `&&`, `|`, `$(...)`, обратные кавычки и перенаправления работают как команды. Если в строке есть данные, которые контролирует пользователь, это инъекция команд. Правило: используйте список; если оболочка нужна, экранируйте через `shlex.quote`, но предпочтительно её избегать. Списки аргументов не защищают от того, что сама программа примет значение, начинающееся с `-`, за опцию, - для таких программ используйте разделитель `--`.

**Потоки и буферы.** `capture_output` хранит весь вывод в памяти: для гигантского вывода читайте потоково через `Popen`. `run` не годится для долгоживущих процессов, с которыми нужен диалог.

**logging.** Основные объекты:

* **Logger** - источник записей, получается `logging.getLogger(name)`. Один и тот же `name` всегда даёт один и тот же объект. Имена с точкой образуют иерархию: `app.db` - потомок `app`, все - потомки корневого логгера (`getLogger()` без имени).
* **Уровни:** `DEBUG` 10, `INFO` 20, `WARNING` 30, `ERROR` 40, `CRITICAL` 50. Запись проходит, если её уровень не ниже эффективного уровня логгера (заданного, иначе унаследованного от ближайшего предка). У корневого логгера по умолчанию `WARNING`.
* **Handler** - куда писать: `StreamHandler`, `FileHandler`, `RotatingFileHandler`, `NullHandler`. У handler есть свой уровень и **Formatter** (`"%(asctime)s %(levelname)s %(name)s: %(message)s"`).
* **propagate.** После своих handlers запись по умолчанию передаётся handlers предков. Из-за этого запись может появиться дважды, если handler добавлен и логгеру, и его предку.
* **basicConfig(level=..., format=...)** настраивает корневой логгер, но делает это только если у него ещё нет handlers (иначе нужен `force=True`).

**Правила для кода.**

* Модуль создаёт `logger = logging.getLogger(__name__)` и только пишет записи. Handlers и уровни настраивает **точка входа приложения**, а библиотека добавляет максимум `NullHandler`.
* Ленивое форматирование: `logger.info("user=%s", user)`. Строка собирается только если запись проходит фильтры; значения лежат в `record.args`.
* `logger.exception(msg)` = `logger.error(msg, exc_info=True)`, вызывается внутри `except`: в запись попадает трассировка.
* Не логируйте пароли, токены и персональные данные.

<!-- section: examples -->
## Примеры

**Пример 1. subprocess.run: вывод, код возврата, check.**

```python run
import subprocess
import sys

r = subprocess.run([sys.executable, "-c", "print('привет'); import sys; print('warn', file=sys.stderr)"],
                   capture_output=True, text=True, encoding="utf-8")
print(r.returncode, repr(r.stdout), repr(r.stderr))

r = subprocess.run([sys.executable, "-c", "import sys; sys.exit(3)"], capture_output=True, text=True)
print(r.returncode)

try:
    subprocess.run([sys.executable, "-c", "import sys; sys.exit(3)"], check=True)
except subprocess.CalledProcessError as exc:
    print("CalledProcessError:", exc.returncode)

r = subprocess.run([sys.executable, "-c", "print(input().upper())"], input="abc\n", capture_output=True, text=True)
print(r.stdout.strip())
```
```output
0 'привет\n' 'warn\n'
3
CalledProcessError: 3
ABC
```

**Пример 2. Таймаут и отсутствующая программа.**

```python run
import subprocess
import sys

try:
    subprocess.run([sys.executable, "-c", "import time; time.sleep(10)"], timeout=0.5)
except subprocess.TimeoutExpired as exc:
    print("TimeoutExpired после", exc.timeout, "с")

try:
    subprocess.run(["no-such-program-xyz"])
except FileNotFoundError:
    print("FileNotFoundError")
```
```output
TimeoutExpired после 0.5 с
FileNotFoundError
```

**Пример 3. Почему shell=True опасен: аргумент как данные.**

```python run
import subprocess
import sys

evil = "x; echo INJECTED"
child = "import sys; print(sys.argv[1])"

safe = subprocess.run([sys.executable, "-c", child, evil], capture_output=True, text=True)
print("список:", safe.stdout.strip())

unsafe = subprocess.run(f'echo {evil}', shell=True, capture_output=True, text=True)
print("shell :", unsafe.stdout.split())
```
```output
список: x; echo INJECTED
shell : ['x', 'INJECTED']
```

**Пример 4. Логгеры, уровни, иерархия и propagate.**

```python run
import logging
import sys

root = logging.getLogger("app")
root.setLevel(logging.INFO)
handler = logging.StreamHandler(sys.stdout)
handler.setFormatter(logging.Formatter("%(levelname)s %(name)s: %(message)s"))
root.addHandler(handler)
root.propagate = False

db = logging.getLogger("app.db")
db.debug("не пройдёт: уровень DEBUG ниже INFO")
db.info("соединение открыто: %s", "localhost")
db.warning("медленный запрос: %.1f с", 2.5)
print(db.getEffectiveLevel() == logging.INFO, logging.getLogger("app.db") is db)
db.setLevel(logging.ERROR)
db.warning("теперь отброшено")
db.error("а это пройдёт")
```
```output
INFO app.db: соединение открыто: localhost
WARNING app.db: медленный запрос: 2.5 с
True True
ERROR app.db: а это пройдёт
```

**Пример 5. logger.exception и ленивое форматирование.**

```python run
import logging

class Collect(logging.Handler):
    def __init__(self):
        super().__init__()
        self.records = []

    def emit(self, record):
        self.records.append(record)

log = logging.getLogger("demo")
collector = Collect()
log.addHandler(collector)
log.setLevel(logging.INFO)
log.propagate = False

class Costly:
    def __str__(self):
        print("(форматирование выполнено)")
        return "costly"

log.debug("значение: %s", Costly())     # str() не вызывается
log.info("значение: %s", Costly())      # запись проходит; str() вызывается при getMessage()
rec = collector.records[0]
print(rec.msg, rec.args[0].__class__.__name__)
print(rec.getMessage())

def parse(text):
    try:
        return int(text)
    except ValueError:
        log.exception("не удалось разобрать %r", text)

parse("12x")
rec = collector.records[-1]
print(rec.levelname, rec.getMessage(), rec.exc_info[0].__name__)
```
```output
значение: %s Costly
(форматирование выполнено)
значение: costly
ERROR не удалось разобрать '12x' ValueError
```

**Пример 6. basicConfig работает один раз.**

```python run
import logging
import sys

logging.basicConfig(stream=sys.stdout, level=logging.INFO, format="%(levelname)s:%(message)s")
logging.info("первая настройка")
logging.basicConfig(stream=sys.stdout, level=logging.DEBUG, format="IGNORED %(message)s")
logging.debug("не видно: вторая настройка не применилась")
logging.basicConfig(stream=sys.stdout, level=logging.DEBUG, format="NEW %(message)s", force=True)
logging.debug("видно после force=True")
```
```output
INFO:первая настройка
NEW видно после force=True
```

<!-- section: mistakes -->
## Типичные ошибки

* **`shell=True` со строкой, куда попадают данные.** Инъекция команд. Передавайте список аргументов.
* **Нет `timeout`.** Зависшая программа блокирует вашу навсегда.
* **Игнорирование `returncode`.** Без `check=True` ошибка не вызывает исключения, и скрипт идёт дальше с мусорным результатом.
* **`"python"` вместо `sys.executable`.** Запустится другой интерпретатор или окажется, что команды нет.
* **Чтение stdout без `capture_output`.** `r.stdout` будет `None`.
* **Без `text=True`.** Вывод приходит как `bytes`, сравнение со строкой не сработает.
* **Огромный вывод в `capture_output`.** Всё копится в памяти; для больших потоков читайте построчно через `Popen`.
* **Путать `FileNotFoundError` и ненулевой код.** Первое - программа не найдена, второе - она запустилась и завершилась с ошибкой.
* **`logging.info(f"...")` и `+` для строк.** Строка создаётся всегда и ломает группировку одинаковых сообщений; передавайте аргументы.
* **`logging.error(str(e))` вместо `logger.exception`.** Трассировка теряется.
* **`logger.exception` вне `except`.** Трассировки нет (`NoneType: None`).
* **`basicConfig` в библиотеке.** Библиотека навязывает настройки приложению; настраивает только точка входа.
* **Handler добавляется при каждом вызове функции.** Запись начинает дублироваться; проверяйте `logger.handlers` или настраивайте один раз.
* **Дублирование записей из-за propagate.** Handler и у `app`, и у корня - запись выйдет дважды.
* **Путаница уровней логгера и handler.** Запись должна пройти оба фильтра.
* **Секреты в журнале.** Пароли, токены и персональные данные попадают в файлы и системы сбора логов.

<!-- section: summary -->
## Итог

* `subprocess.run([...], capture_output=True, text=True, timeout=..., check=True)` - стандартный безопасный вызов; аргументы списком, без оболочки.
* `shell=True` с внешними данными - инъекция команд. Для Python-кода используйте `sys.executable`.
* Исключения: `CalledProcessError` (код не 0, только при `check=True`), `TimeoutExpired`, `FileNotFoundError`.
* Логгер получайте через `getLogger(__name__)`; handlers и уровни настраивает точка входа, `basicConfig` вызывается один раз.
* Пишите `logger.info("x=%s", x)`, в `except` вызывайте `logger.exception`, секреты не логируйте.
* Тестируйте журнал, подключая собственный `Handler`, и смотрите на `record.levelno`, `record.args`, `record.exc_info`.
* Вы завершили курс: следующий шаг - применять эти модули в своих проектах и читать документацию на docs.python.org.
