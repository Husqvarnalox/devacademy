<!-- section: simple -->
## Три способа превратить данные в байты

Программа живёт со словарями, списками и числами, а файл, сеть и другая программа ждут текст или байты. Перевод между ними называется *сериализацией*. В стандартной библиотеке для самых частых случаев есть три модуля.

* **`json`** - универсальный текстовый формат для вложенных данных: объекты, массивы, строки, числа, `true`/`false`/`null`. Читается человеком, понимается любым языком, но не знает о датах, множествах и точных десятичных числах.
* **`csv`** - таблицы: строки и столбцы. Выглядит как «значения через запятую», но настоящие CSV-файлы содержат запятые внутри значений, кавычки и переводы строк, поэтому разбирать их вручную нельзя.
* **`struct`** - двоичные данные фиксированной формы: заголовки файлов и сетевых пакетов, где «1 байт версия, 4 байта длина» нужно записать точно.

Общее правило этого урока: *формат - это договорённость*. Какая кодировка, какой порядок байт, как экранировать запятую, что делать с отсутствующим значением - всё это нужно указывать явно, а не надеяться на значения по умолчанию. Строки и кодировки вы уже знаете из урока [«Строки»](lesson:python.fundamentals.strings), а запись файлов - из [предыдущего урока](lesson:python.stdlib.pathlib-os-shutil-tempfile).

<!-- section: precise -->
## Типы, параметры и ограничения

**JSON и Python.** `dumps`/`dump` кодируют: `dict` -> object, `list` и `tuple` -> array, `str` -> string, `int` и `float` -> number, `True`/`False` -> `true`/`false`, `None` -> `null`. `loads`/`load` декодируют обратно: object -> `dict`, array -> `list`, число с точкой или экспонентой -> `float`, целое -> `int`. Следствия:

* Ключи объекта в JSON - только строки: `{1: "a"}` превратится в `{"1": "a"}`; кортеж вернётся списком. Поэтому `loads(dumps(x)) == x` выполняется не всегда.
* `set`, `datetime`, `Decimal`, `bytes`, пользовательские классы стандартным кодировщиком не поддерживаются - `TypeError`. Выход - параметр `default=функция`: она вызывается для неизвестного объекта и возвращает JSON-совместимое представление (или поднимает `TypeError` для неподдерживаемых типов).
* `NaN`, `Infinity` по умолчанию записываются, но не являются стандартным JSON; `allow_nan=False` запрещает их при записи.
* Параметры вывода: `ensure_ascii=True` (по умолчанию) экранирует не-ASCII как `\uXXXX`, `False` оставляет буквы как есть (тогда файл пишите в UTF-8); `indent=2` форматирует; `sort_keys=True` даёт детерминированный порядок; `separators=(",", ":")` убирает пробелы.
* Параметры чтения: `parse_float=Decimal` читает дробные числа без потери точности; `parse_int`, `object_hook`, `object_pairs_hook` (например, для сохранения дубликатов ключей) позволяют строить свои типы. Дублирующиеся ключи по умолчанию молча перезаписывают друг друга.
* Размер документа ограничьте до разбора: `json.loads` читает всё в память, а глубокая вложенность может вызвать `RecursionError`.

**CSV.** Основные объекты: `csv.reader(file)` -> списки строк, `csv.writer(file)` -> `writerow`/`writerows`, `csv.DictReader` -> словари (первая строка - заголовок; параметры `restkey`, `restval` для строк другой длины), `csv.DictWriter` с `fieldnames` и `writeheader()`.

* **Правило `newline=''`.** Файл открывают как `open(path, newline="", encoding="utf-8")`: тогда модуль сам видит `\r\n` и `\n` и корректно обрабатывает переводы строк внутри значений в кавычках; для `StringIO` используйте `StringIO(text, newline="")`. Без этого записи могут искажаться, а на Windows появляются пустые строки.
* **Кавычки и диалекты.** `quoting=csv.QUOTE_MINIMAL` (по умолчанию) берёт в кавычки только поля со спецсимволами; есть `QUOTE_ALL`, `QUOTE_NONNUMERIC`, `QUOTE_NONE`. Кавычки внутри значения удваиваются (`""`). Параметры `delimiter` (например, `";"` или `"\t"`), `quotechar`, `lineterminator` и готовые диалекты `excel`, `excel-tab`, `unix` описывают вариант формата; `csv.Sniffer` угадывает диалект и ненадёжен.
* **Типы.** CSV хранит только текст: все значения читаются как `str`, числа и даты приводите сами. `None` пишется пустой строкой и обратно не отличается от `""`.
* **Кодировка.** Файл из Excel часто в `utf-8-sig` (с BOM); открывайте с этой кодировкой, иначе первое имя столбца получит невидимый символ.

**struct.** `struct.pack(fmt, *values)` -> `bytes`; `unpack(fmt, data)` -> кортеж (длина данных должна совпасть с `calcsize(fmt)`, иначе `struct.error`); `unpack_from(fmt, buffer, offset)` не требует точного размера; `struct.Struct(fmt)` компилирует формат один раз.

* **Порядок байт, размер и выравнивание** задаёт первый символ: `@` (по умолчанию) - нативные порядок, размеры и выравнивание; `=` - нативный порядок, стандартные размеры, без выравнивания; `<` little-endian и `>` или `!` (сетевой порядок, big-endian) - стандартные размеры, без выравнивания. Нативный `@BI` содержит байты выравнивания (обычно 8 байт вместо 5) и непереносим; для форматов файлов и протоколов всегда пишите `<`, `>` или `!`.
* Коды: `B`/`H`/`I`/`Q` - беззнаковые 1/2/4/8 байт (`b`, `h`, `i`, `q` - знаковые), `f`/`d` - float32/float64, `?` - bool, `Ns` - N байт (`bytes`, дополняется нулями), `x` - байт-заполнитель. Значение вне диапазона даёт `struct.error`.
* Строки в `struct` не хранятся: кодируйте их сами (`text.encode("utf-8")`) и записывайте длину отдельно.

**Ограничения.** `json` не сохраняет типы Python и порядок не определяет смысла; CSV - не стандарт, а набор вариантов; `struct` фиксирует только простые плоские структуры. Для недоверенных данных не применяйте `pickle`: его загрузка может выполнить произвольный код.

<!-- section: examples -->
## Примеры

**Пример 1. JSON: соответствие типов, параметры вывода, круговое преобразование.**

```python run
import json

data = {"name": "Анна", "tags": ("a", "b"), "ok": True, "none": None, 1: 2.5}
s = json.dumps(data)
print(s)
print(json.dumps(data, ensure_ascii=False, sort_keys=False, separators=(",", ":")))
print(json.loads(s))
print(json.loads(json.dumps(data)) == data)

print(json.dumps({"b": 1, "a": [1, 2]}, indent=2, sort_keys=True))
print(json.loads("[1, 2.0, 1e3, null]"), json.loads('"\\u044f"'))
try:
    json.loads("{'a': 1}")
except json.JSONDecodeError as exc:
    print("JSONDecodeError, позиция", exc.pos)
```
```output
{"name": "\u0410\u043d\u043d\u0430", "tags": ["a", "b"], "ok": true, "none": null, "1": 2.5}
{"name":"Анна","tags":["a","b"],"ok":true,"none":null,"1":2.5}
{'name': 'Анна', 'tags': ['a', 'b'], 'ok': True, 'none': None, '1': 2.5}
False
{
  "a": [
    1,
    2
  ],
  "b": 1
}
[1, 2.0, 1000.0, None] я
JSONDecodeError, позиция 1
```

**Пример 2. default= и parse_float=Decimal.**

```python run
import datetime
import json
from decimal import Decimal

def default(o):
    if isinstance(o, datetime.date):
        return o.isoformat()
    if isinstance(o, set):
        return sorted(o)
    raise TypeError(f"{type(o).__name__} is not JSON serializable")

print(json.dumps({"d": datetime.date(2024, 3, 1), "s": {3, 1, 2}}, default=default, sort_keys=True))
try:
    json.dumps({"x": 1 + 2j}, default=default)
except TypeError as exc:
    print("TypeError:", exc)

text = '{"price": 0.1, "tax": 0.2}'
as_float = json.loads(text)
as_decimal = json.loads(text, parse_float=Decimal)
print(as_float["price"] + as_float["tax"], as_decimal["price"] + as_decimal["tax"])
print(json.dumps(float("nan")), json.loads("NaN"))
try:
    json.dumps(float("nan"), allow_nan=False)
except ValueError:
    print("ValueError: NaN запрещён")
```
```output
{"d": "2024-03-01", "s": [1, 2, 3]}
TypeError: complex is not JSON serializable
0.30000000000000004 0.3
NaN nan
ValueError: NaN запрещён
```

**Пример 3. CSV: кавычки, DictReader, правило newline.**

```python run
import csv
import io

rows = [["name", "note"], ["Анна", 'say "hi", ok'], ["Боб", "line1\nline2"], ["Вика", ""]]
out = io.StringIO()
csv.writer(out, lineterminator="\n").writerows(rows)
text = out.getvalue()
print(text, end="")
print(len(text.splitlines()), len(list(csv.reader(io.StringIO(text, newline="")))))

for rec in csv.DictReader(io.StringIO(text, newline="")):
    print(dict(rec))

print(list(csv.reader(io.StringIO("a;b;'c;d'\n"), delimiter=";", quotechar="'")))
print("a,b".split(","), '"x,y",z'.split(","))
out = io.StringIO()
csv.writer(out, quoting=csv.QUOTE_ALL, lineterminator="\n").writerow([1, "a", None])
print(out.getvalue(), end="")

dw_out = io.StringIO()
dw = csv.DictWriter(dw_out, fieldnames=["id", "title"], lineterminator="\n")
dw.writeheader()
dw.writerow({"id": 1, "title": "Война и мир"})
print(dw_out.getvalue(), end="")
```
```output
name,note
Анна,"say ""hi"", ok"
Боб,"line1
line2"
Вика,
5 4
{'name': 'Анна', 'note': 'say "hi", ok'}
{'name': 'Боб', 'note': 'line1\nline2'}
{'name': 'Вика', 'note': ''}
[['a', 'b', 'c;d']]
['a', 'b'] ['"x', 'y"', 'z']
"1","a",""
id,title
1,Война и мир
```

**Пример 4. struct: порядок байт, размеры и ошибки.**

```python run
import struct

print(struct.pack(">I", 1), struct.pack("<I", 1), struct.pack("!H", 258))
print(struct.calcsize("<BI"), struct.calcsize(">BHI"), struct.calcsize("@BI") > struct.calcsize("<BI"))

header = struct.Struct(">BI")
payload = "Привет".encode("utf-8")
packet = header.pack(1, len(payload)) + payload
print(packet.hex(" "), len(payload), len("Привет"))
version, length = header.unpack_from(packet)
print(version, length, packet[header.size:header.size + length].decode("utf-8"))

print(struct.unpack("<hH", b"\xff\xff\xff\xff"))
for bad in (lambda: struct.pack("<B", 256), lambda: struct.unpack("<I", b"\x01\x02")):
    try:
        bad()
    except struct.error as exc:
        print("struct.error")
```
```output
b'\x00\x00\x00\x01' b'\x01\x00\x00\x00' b'\x01\x02'
5 7 True
01 00 00 00 0c d0 9f d1 80 d0 b8 d0 b2 d0 b5 d1 82 12 6
1 12 Привет
(-1, 65535)
struct.error
struct.error
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ожидание точного кругового преобразования JSON.** Числовые ключи становятся строками, кортежи - списками, `NaN` не равен себе. Проверяйте на своих данных.
* **`TypeError: Object of type ... is not JSON serializable`.** Добавьте `default=` и явно перечислите поддерживаемые типы; для остальных поднимайте `TypeError`, а не возвращайте `str(o)` молча.
* **Потеря точности денежных значений.** Числа с точкой читаются как `float`; для денег и точных величин используйте `parse_float=Decimal`.
* **`ensure_ascii=False` и неправильная кодировка файла.** Записывайте такой JSON в файл с `encoding="utf-8"`.
* **Разбор CSV через `split(",")`.** Ломается на запятых и переводах строк внутри значений. Используйте `csv`.
* **Открытие CSV без `newline=''`.** Переводы строк внутри значений искажаются, при записи на Windows получаются пустые строки.
* **Ожидание типов из CSV.** Все значения - строки; число приводите сами и проверяйте пустые ячейки.
* **BOM в первом заголовке.** Файл из Excel может начинаться с `﻿`; читайте как `utf-8-sig`.
* **Нативный формат в `struct`.** `@BI` (или формат без префикса) даёт выравнивание и зависит от платформы; указывайте `<`, `>` или `!`.
* **Путать длину строки и длину байт.** `len("Привет")` равно 6, а UTF-8 занимает 12 байт; в заголовок кладите длину `bytes`.
* **Верить длине из входных данных.** При разборе сообщения проверяйте, что данных достаточно и что объявленная длина совпадает с реальной.
* **`pickle` для данных из внешнего источника.** Загрузка может выполнить код; используйте JSON или `struct`.

<!-- section: summary -->
## Итог

* JSON: ключи - строки, кортеж становится массивом, `set`, `datetime`, `Decimal` требуют `default=`; `ensure_ascii`, `sort_keys`, `separators` управляют видом, `parse_float=Decimal` сохраняет точность.
* CSV читайте и пишите модулем `csv` с `newline=""` и явной кодировкой; значения - только строки; кавычки удваиваются; `DictReader`/`DictWriter` удобнее индексов.
* `struct` с явным порядком байт (`<`, `>`, `!`) даёт стандартные размеры без выравнивания; нативный `@` непереносим; строки кодируйте сами и храните длину в байтах.
* Все три формата - договорённости: фиксируйте кодировку, порядок байт, правила пустых значений и проверяйте внешний ввод.
* Дальше: [datetime и time](lesson:python.stdlib.datetime-and-time) - даты, часовые пояса и измерение времени.
