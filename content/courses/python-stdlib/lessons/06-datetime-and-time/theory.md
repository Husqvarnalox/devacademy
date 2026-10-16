<!-- section: simple -->
## Время - это не просто числа

Дата «10 марта 2024, 12:30» выглядит однозначно, но на самом деле это только половина данных. Неизвестно, в каком часовом поясе эти 12:30: в Москве, в Берлине или в Нью-Йорке. Значения «через 24 часа» и «в это же время завтра» тоже различаются, если между ними переход на летнее время. Большинство ошибок с датами возникает не из-за неправильного синтаксиса, а из-за того, что эти различия не замечены.

В стандартной библиотеке три модуля:

* **`datetime`** - типы `date` (только дата), `time` (только время суток), `datetime` (дата и время), `timedelta` (длительность) и `timezone` (фиксированное смещение).
* **`zoneinfo`** - настоящие часовые пояса по базе IANA (`"Europe/Berlin"`), которые знают про летнее время.
* **`time`** - низкоуровневые часы: `time.time()`, `time.monotonic()`, `time.perf_counter()`, `time.sleep()`.

Главное правило: **храните и передавайте время в UTC (или с явным смещением), переводите в местный пояс только для показа пользователю**. А для измерения «сколько это заняло» используйте монотонные часы, а не календарное время. Вычисления с числами вы уже знаете из урока [«Числа и операторы»](lesson:python.fundamentals.numbers-and-operators), форматы данных - из [предыдущего урока](lesson:python.stdlib.json-csv-struct).

<!-- section: precise -->
## Модель, API и ловушки

**Naive и aware.** Объект `datetime` без `tzinfo` называется *naive* (наивным): он не знает своего пояса; с `tzinfo` - *aware* (осведомлённым). Правила:

* Сравнение `==` naive и aware даёт `False`, упорядочение (`<`, `>`) и вычитание - `TypeError`. Смешивать их нельзя.
* `aware.astimezone(zone)` переводит **тот же момент** в другой пояс. `naive.replace(tzinfo=zone)` присваивает поясу **те же числа** (это интерпретация, а не пересчёт). У naive-объекта `astimezone()` без аргумента считает его системным местным временем - зависит от машины.
* Текущее время: `datetime.now(timezone.utc)` (aware). `datetime.utcnow()` и `utcfromtimestamp()` возвращают naive-объекты и устарели с Python 3.12.

**Пояса.** `timezone.utc` (или `datetime.UTC` с 3.11) и `timezone(timedelta(hours=3))` - фиксированные смещения без перехода на летнее время. `ZoneInfo("Europe/Berlin")` (модуль `zoneinfo`, 3.9+) использует базу IANA; на Windows системной базы нет, и нужен пакет `tzdata` (иначе `ZoneInfoNotFoundError`). Не используйте сокращения вроде `"EST"` как имена зон.

**Летнее время.** В ночь перехода *весной* часы перескакивают вперёд, поэтому часть локальных значений **не существует** (в Berlin 31.03.2024 нет времени 02:00-02:59). *Осенью* часы переводят назад, и часть значений **повторяется дважды**; параметр `fold` (0 - первое прохождение, 1 - второе) различает их. Если `fold=0` и `fold=1` дают разные `utcoffset()`, локальное время либо отсутствует, либо неоднозначно. Поэтому «локальное время -> UTC» может быть некорректной операцией, а «UTC -> локальное» - всегда определена.

**Арифметика.** `timedelta` хранит дни, секунды и микросекунды (`total_seconds()`). Для aware-значений сложение `dt + timedelta(days=1)` с `ZoneInfo` идёт по **стенным часам**: то же время суток на следующий день, даже если реально прошло 23 или 25 часов. Для реального интервала переводите в UTC: `(b.astimezone(utc) - a.astimezone(utc))`. Если у обоих объектов один и тот же объект `tzinfo`, вычитание вообще игнорирует смещения и считает разность локальных значений.

**ISO 8601 и форматы.** `dt.isoformat()` и `datetime.fromisoformat(text)` - основной обмен данными (`2024-03-10T12:30:00+02:00`); с Python 3.11 `fromisoformat` разбирает большинство вариантов ISO 8601, включая суффикс `Z`. `strftime(fmt)` и `datetime.strptime(text, fmt)` - произвольные форматы (`%Y-%m-%d %H:%M`); `strptime` строг и медленнее. Названия месяцев и дней в `%B`/`%A` зависят от локали. Для календарных вычислений также есть `date.isocalendar()`, `date.weekday()`, модуль `calendar`.

**Часы модуля time.**

* `time.time()` - секунды с эпохи (Unix epoch, по настенным часам); может скачками меняться при коррекции времени (NTP, ручная смена).
* `time.monotonic()` - монотонные часы: никогда не идут назад, точка отсчёта не определена, подходят для таймаутов и измерения длительности.
* `time.perf_counter()` - монотонные часы наибольшей доступной точности для коротких интервалов (бенчмарки). Вариант в наносекундах - `perf_counter_ns()`.
* `time.sleep(seconds)` приостанавливает поток; точность ограничена планировщиком ОС.

**Тесты со временем.** Код, вызывающий `now()` внутри, трудно проверять. Передавайте момент параметром (`def is_expired(expires_at, now)`), а `now` берите на границе программы.

**Ограничения.** `datetime` хранит микросекунды (не наносекунды); диапазон лет 1-9999; правила часовых поясов меняются, поэтому актуальность зависит от версии базы IANA; високосные секунды не поддерживаются.

<!-- section: examples -->
## Примеры

В примерах нет `now()`: даты фиксированы, и вывод детерминирован.

**Пример 1. Naive и aware, сравнение, перевод между поясами.**

```python run
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

naive = datetime(2024, 3, 10, 12, 30)
aware = datetime(2024, 3, 10, 12, 30, tzinfo=timezone.utc)
print(naive == aware)
try:
    naive < aware
except TypeError as exc:
    print("TypeError:", exc)

berlin = ZoneInfo("Europe/Berlin")
print(aware.astimezone(berlin).isoformat())
print(naive.replace(tzinfo=berlin).isoformat())    # те же числа, другой момент
print(aware == aware.astimezone(berlin))           # один и тот же момент
plus3 = timezone(timedelta(hours=3))
print(aware.astimezone(plus3).isoformat(), aware.astimezone(plus3).utcoffset())
```
```output
False
TypeError: can't compare offset-naive and offset-aware datetimes
2024-03-10T13:30:00+01:00
2024-03-10T12:30:00+01:00
True
2024-03-10T15:30:00+03:00 3:00:00
```

**Пример 2. Летнее время: пропавший и повторяющийся час.**

```python run
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

berlin = ZoneInfo("Europe/Berlin")
utc = timezone.utc

# весна: 02:00-02:59 31.03.2024 не существует
gap = datetime(2024, 3, 31, 2, 30)
print(gap.replace(tzinfo=berlin, fold=0).utcoffset(), gap.replace(tzinfo=berlin, fold=1).utcoffset())
print(gap.replace(tzinfo=berlin).astimezone(utc).astimezone(berlin).isoformat())

# осень: 02:00-02:59 27.10.2024 встречается дважды
amb = datetime(2024, 10, 27, 2, 30)
first = amb.replace(tzinfo=berlin, fold=0)
second = amb.replace(tzinfo=berlin, fold=1)
print(first.isoformat(), second.isoformat(), first.astimezone(utc).isoformat())

# сутки на стенных часах и в реальности
a = datetime(2024, 3, 30, 12, tzinfo=berlin)
b = a + timedelta(days=1)
print(b.isoformat(), b - a, b.astimezone(utc) - a.astimezone(utc))
```
```output
1:00:00 2:00:00
2024-03-31T03:30:00+02:00
2024-10-27T02:30:00+02:00 2024-10-27T02:30:00+01:00 2024-10-27T00:30:00+00:00
2024-03-31T12:00:00+02:00 1 day, 0:00:00 23:00:00
```

**Пример 3. ISO 8601, strftime и strptime, timedelta.**

```python run
from datetime import date, datetime, timedelta

dt = datetime.fromisoformat("2024-03-10T12:30:00+02:00")
print(dt, dt.tzinfo, dt.isoformat(), datetime.fromisoformat("2024-03-10T10:30:00Z").utcoffset())
print(dt.strftime("%d.%m.%Y %H:%M"), datetime.strptime("10.03.2024 18:05", "%d.%m.%Y %H:%M"))
try:
    datetime.strptime("2024-13-01", "%Y-%m-%d")
except ValueError as exc:
    print("ValueError")

d1, d2 = date(2024, 2, 28), date(2024, 3, 1)
print(d2 - d1, (d2 - d1).days, d1 + timedelta(days=2), d1.weekday(), d1.isocalendar()[:2])
delta = timedelta(days=1, hours=2, minutes=30)
print(delta, delta.total_seconds(), delta * 2, timedelta(seconds=-1))
print(date(2024, 3, 31).replace(day=1), datetime(2024, 1, 31).replace(month=2, day=29))
```
```output
2024-03-10 12:30:00+02:00 UTC+02:00 2024-03-10T12:30:00+02:00 0:00:00
10.03.2024 12:30 2024-03-10 18:05:00
ValueError
2 days, 0:00:00 2 2024-03-01 2 (2024, 9)
1 day, 2:30:00 95400.0 2 days, 5:00:00 -1 day, 23:59:59
2024-03-01 2024-02-29 00:00:00
```

**Пример 4. Какие часы для чего.**

```python run
import time

t0 = time.monotonic()
p0 = time.perf_counter()
time.sleep(0.01)
elapsed_m = time.monotonic() - t0
elapsed_p = time.perf_counter() - p0
print(elapsed_m >= 0.009, elapsed_p >= 0.009, elapsed_p >= 0)

print(time.get_clock_info("monotonic").monotonic, time.get_clock_info("time").monotonic)
print(time.get_clock_info("perf_counter").monotonic, isinstance(time.time(), float))
print(time.gmtime(0).tm_year, time.strftime("%Y-%m-%d", time.gmtime(86400 * 365)))
```
```output
True True True
True False
True True
1970 1971-01-01
```

<!-- section: mistakes -->
## Типичные ошибки

* **Смешивание naive и aware.** `TypeError` при сравнении и вычитании; а `==` молча даёт `False`. Решите заранее: внутри программы - только aware (лучше UTC).
* **`datetime.utcnow()`.** Возвращает naive-объект, который легко принять за местное время; устарел с 3.12. Используйте `datetime.now(timezone.utc)`.
* **`replace(tzinfo=...)` вместо `astimezone`.** `replace` меняет только метку, а не момент; для перевода нужен `astimezone`.
* **Хранение локального времени без пояса.** Через год невозможно восстановить момент. Храните UTC и (при необходимости) имя зоны IANA.
* **Сложение суток как 24 часа.** В день перехода на летнее время сутки на стенных часах имеют 23 или 25 часов. Решите, что вам нужно: стенные часы или реальная длительность.
* **Вычитание двух значений с одним `ZoneInfo`.** Считается по локальным значениям; для реального интервала приведите оба к UTC.
* **Локальное время "02:30" в ночь перехода.** Оно может не существовать или повторяться; проверяйте `fold` и `utcoffset` либо принимайте время с явным смещением.
* **Сокращения поясов (`EST`, `CET`) и фиксированные смещения вместо зон.** Они не знают про летнее время и историю правил.
* **`ZoneInfoNotFoundError` на Windows.** Установите пакет `tzdata`.
* **`time.time()` для измерения длительности.** Календарные часы могут скакнуть; используйте `monotonic` или `perf_counter`.
* **`strptime` для ISO-строк.** Для обмена данными проще и надёжнее `fromisoformat`.
* **Тесты, зависящие от текущего времени.** Передавайте `now` параметром или подменяйте источник времени.
* **Зависимость формата от локали.** `%A`, `%B` зависят от настроек; для обмена данными используйте ISO.

<!-- section: summary -->
## Итог

* Различайте naive и aware; внутри программы работайте с UTC, в локальный пояс переводите на границе показа. `astimezone` переводит момент, `replace(tzinfo=)` лишь присваивает метку.
* `zoneinfo.ZoneInfo` знает летнее время (на Windows нужен `tzdata`); весной часть локальных значений не существует, осенью часть повторяется - различайте через `fold`.
* Арифметика с `ZoneInfo` идёт по стенным часам; реальный интервал - через UTC.
* `isoformat`/`fromisoformat` для обмена, `strftime`/`strptime` для особых форматов; `utcnow()` не используйте.
* Интервалы измеряйте `time.perf_counter()` или `time.monotonic()`, а не `time.time()`. В тестах передавайте время параметром.
* Дальше: [decimal, fractions, math, statistics, random](lesson:python.stdlib.decimal-fractions-math-statistics-random) - точные и статистические вычисления.
