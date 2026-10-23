<!-- section: simple -->
## Зачем этот урок

Исключения в JavaScript похожи на привычные, но отличаются деталями: бросить можно любое значение, `finally` может перебить `return`, у собственного класса ошибки по умолчанию неверное имя, а ошибки разбора чаще не возникают вовсе: `Number("")` молча даёт `0`. Вторая половина урока - ввод в Node.js: как прочитать весь stdin и безопасно превратить текст в записи. Успешное прохождение (упражнение и квиз) засчитывает уроки 20 и 21 курса «JavaScript с нуля».

## Главная мысль

Данные снаружи (stdin, файл, JSON) нужно **разбирать и проверять явно**. Ошибки программиста (неверный тип, обращение к `undefined`) приходят исключениями `TypeError` и `ReferenceError`, а ошибки данных язык часто не замечает. Поэтому ожидаемые ошибки данных вы описываете своим классом исключения или собираете в список, а прочие исключения не глотаете.

<!-- section: examples -->
## Пример 1. throw, собственный класс и причина

```javascript run
function parseAge(text) {
  const n = Number(text);
  if (text.trim() === "" || !Number.isInteger(n)) {
    throw new RangeError(`bad age: ${JSON.stringify(text)}`);
  }
  return n;
}
for (const t of ["42", "", "4.5", "x"]) {
  try {
    console.log(parseAge(t));
  } catch (e) {
    console.log(e.name, e.message);
  }
}

class ValidationError extends Error {
  constructor(message, line) {
    super(message);
    this.name = "ValidationError";
    this.line = line;
  }
}
class Forgot extends Error {}
const e1 = new ValidationError("empty name", 3);
console.log(e1 instanceof ValidationError, e1 instanceof Error, e1.name, e1.line, String(e1));
console.log(new Forgot("x").name);
const wrapped = new Error("не удалось прочитать конфиг", { cause: e1 });
console.log(wrapped.cause === e1);
```
```output
42
RangeError bad age: ""
RangeError bad age: "4.5"
RangeError bad age: "x"
true true ValidationError 3 ValidationError: empty name
Error
true
```

`throw` принимает любое значение, но бросать нужно экземпляры `Error`: у них есть `name`, `message` и `stack`. У подкласса `name` надо задать самому (иначе будет `"Error"`), а для различения причин проверяют `instanceof`. Параметр `cause` сохраняет исходную ошибку при обёртывании.

## Пример 2. catch, finally и поток управления

```javascript run
try {
  throw "oops";
} catch (e) {
  console.log(typeof e, e instanceof Error);
}
function f() {
  try {
    return "try";
  } finally {
    console.log("finally");
  }
}
console.log(f());
function g() {
  try {
    return "try";
  } finally {
    return "finally";
  }
}
console.log(g());
function h() {
  try {
    throw new Error("x");
  } catch {
    return "caught";
  } finally {
    console.log("cleanup");
  }
}
console.log(h());
for (const code of ["null.x", "JSON.parse('{bad')", "missing()", "new Array(-1)"]) {
  try {
    eval(code);
  } catch (e) {
    console.log(e.name);
  }
}
```
```output
string false
finally
try
finally
cleanup
caught
TypeError
SyntaxError
ReferenceError
RangeError
```

`catch` ловит любое брошенное значение, поэтому `e` может не быть `Error`. `finally` выполняется всегда; `return` внутри `finally` **заменяет** результат и подавляет исключение, и так писать не следует. Блок `catch` без параметра допустим. Типичные встроенные ошибки: `TypeError` (операция над неподходящим значением), `ReferenceError` (нет переменной), `SyntaxError` (в том числе `JSON.parse`), `RangeError`. Ошибки в синхронном коде не ловятся внутри колбэков, выполняющихся позже; про асинхронные ошибки говорит курс «JavaScript: основы языка».

## Пример 3. Разбор текста без сюрпризов

```javascript run
console.log(Number(""), Number(" 3 "), Number("1,5"), Number("1_0"), Number("0x1f"), Number(null));
console.log(parseFloat("3.5kg"), parseInt("0.9"), Number("Infinity"), Number("12px"));
console.log("a,b,,".split(","), "".split(",").length);
console.log("x\r\ny\n".split("\n"));
console.log("x\r\ny\n".split(/\r?\n/));
const strictInt = (s) => (/^[+-]?\d+$/.test(s.trim()) ? Number(s) : null);
console.log(strictInt("42"), strictInt(" 7 "), strictInt(""), strictInt("1.5"));
```
```output
0 3 NaN NaN 31 0
3.5 0 Infinity NaN
[ 'a', 'b', '', '' ] 1
[ 'x\r', 'y', '' ]
[ 'x', 'y', '' ]
42 7 null null
```

Для ввода нужен свой строгий разбор: `Number("")` и `Number("  ")` дают `0`, `Number(null)` - тоже `0`, а `"0x1f"` и `"Infinity"` оказываются «числами». `parseInt` и `parseFloat` читают префикс, поэтому `"12px"` у них не ошибка. `split(",")` на пустой строке возвращает массив из одной пустой строки, а строка, оканчивающаяся переводом строки, даёт лишний пустой элемент в конце. Файлы с Windows содержат `\r\n`: делите по `/\r?\n/`.

## Пример 4. Чтение stdin в Node.js

```javascript
import { readFileSync } from "node:fs";

const text = readFileSync(0, "utf8");        // весь stdin одним вызовом
const lines = text.split(/\r?\n/);
if (lines.at(-1) === "") lines.pop();         // убрать пустой хвост после последнего \n

const n = Number(lines[0]);
const nums = lines[1].split(/\s+/).filter((t) => t !== "").map(Number);
console.log(nums.reduce((s, x) => s + x, 0) * n);
```

В файле `.js` без `"type": "module"` вместо `import` пишут `const { readFileSync } = require("node:fs")`. Дескриптор `0` - это stdin. Для больших потоков и интерактивного ввода есть `process.stdin` с событиями `data`/`end` и `readline`, но для задач с готовым входом достаточно `readFileSync`. Результат выводят через `console.log` (stdout), сообщения об ошибках - через `console.error` (stderr). Если программа должна завершиться с ошибкой, ставят `process.exitCode = 1`, а не вызывают `process.exit()` посреди вывода.

```javascript run
const text = "3\n10 20  30\n\n";
const lines = text.split(/\r?\n/);
if (lines.at(-1) === "") lines.pop();
console.log(lines);
const nums = lines[1].split(/\s+/).filter((t) => t !== "").map(Number);
console.log(nums, nums.reduce((s, x) => s + x, 0));
try {
  JSON.parse("{\"a\": 1,}");
} catch (e) {
  console.log(e.name);
}
```
```output
[ '3', '10 20  30', '' ]
[ 10, 20, 30 ] 60
SyntaxError
```

<!-- section: precise -->
* `throw expr` бросает любое значение; хороший стиль - только `Error` и его подклассы. `new Error(message, { cause })` связывает причину. Имя подкласса задают через `this.name = ...` (или `static name`).
* `try { } catch (e) { } finally { }`: `finally` выполняется при любом выходе, включая `return` и `throw`; `return` в `finally` перекрывает остальные. Исключение в `catch` без обработки продолжает всплывать.
* Бросок вне `try` завершает программу с трассировкой и ненулевым кодом выхода. Незаконченный `await` и необработанные отказы промисов - тема асинхронного кода.
* Встроенные ошибки: `TypeError`, `ReferenceError`, `RangeError`, `SyntaxError` (включая `JSON.parse`), `URIError`; сообщение у них зависит от движка, поэтому на текст `message` в логике не опираются.
* `Number(x)`: `""`, пробелы и `null` дают `0`, `undefined` - `NaN`, `"0x1f"` и `"1e3"` разбираются, `"1,5"` и `"1_0"` - нет. `parseInt(s, 10)`/`parseFloat` читают префикс. Для строгого разбора проверяйте формат регулярным выражением.
* `JSON.parse` бросает `SyntaxError` на неверном тексте; `JSON.stringify` пропускает `undefined` и функции, превращает `NaN` в `null`, не умеет `BigInt` и циклические ссылки.
* `readFileSync(0, "utf8")` читает весь stdin синхронно; на пустом вводе возвращает `""`. Если stdin закрыт или это терминал без данных, вызов может выбросить ошибку - в учебных задачах ввод готовый.
* `str.split(sep)` - строки без `sep`; `str.split(/\s+/)` для строки с пробелами по краям даёт пустые элементы в начале или конце: убирайте их через `trim()` или `filter`.
* Вывод `console.log` добавляет `\n`; для записи без перевода строки есть `process.stdout.write`.

<!-- section: mistakes -->
**1. `Number("")` как «пустого ввода нет».** Пустая строка превращается в `0` и проходит дальше. Проверяйте формат до приведения.

**2. Проглатывание всех ошибок.** `try { ... } catch (e) { }` скрывает и ожидаемые ошибки данных, и настоящие баги. Ловите нужный класс: `if (!(e instanceof ValidationError)) throw e;`.

**3. Подкласс `Error` без имени.** `class MyError extends Error {}` печатает имя `Error`; задайте `this.name` в конструкторе.

**4. `return` в `finally`.** Он тихо отменяет брошенное исключение и прежний `return`. Используйте `finally` только для очистки.

**5. Лишняя пустая строка после `split("\n")`.** Файл заканчивается `\n`, и последний элемент пуст; на Windows остаётся ещё `\r`. Делите по `/\r?\n/` и убирайте пустой хвост.

**6. `split(" ")` вместо `split(/\s+/)`.** Двойные пробелы и табуляции дают пустые токены, которые `Number` превращает в `0`.

**7. Бросок строки или объекта.** `throw "bad"` не даёт трассировки, а `e.message` оказывается `undefined`. Бросайте `new Error("bad")`.

**8. Полагаться на текст сообщения.** `e.message === "Unexpected token"` ломается при смене версии Node. Различайте ошибки по классу или по своему полю (`code`).

<!-- section: summary -->
Теперь вы знаете, как в JavaScript устроены исключения (любое значение, `Error` и подклассы, `finally`, `cause`), почему ошибки разбора нужно проверять самому и как прочитать весь stdin в Node.js. Следующий шаг - курс «JavaScript: первые шаги» и дальше «JavaScript: основы языка». Для подробностей: [Ошибки и исключения](lesson:javascript.from-zero.errors-and-exceptions), [Чтение ввода](lesson:javascript.from-zero.reading-input).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Исключения как в Java и Python: `throw`, `try/catch/finally`, подклассы `Error`. Бросить можно что угодно, поэтому `catch (e)` проверяйте через `instanceof`. Нет типизированных `catch`: фильтруйте внутри блока и пробрасывайте остальное (`throw e`).
* У `Error` есть `name`, `message`, `stack`, `cause`; в подклассе `name` задаётся вручную.
* Языковые операции часто **не** бросают ошибку: деление на ноль, чтение отсутствующего свойства, `Number("abc")` (дают `Infinity`, `undefined`, `NaN`). Проверяйте результат сами.
* Встроенный разбор нестрогий: `Number("")` равно `0`, `parseInt("12px")` равно `12`. Для ввода пишите строгие функции с регулярным выражением.
* Весь stdin: `readFileSync(0, "utf8")` (ESM: `import { readFileSync } from "node:fs"`; CommonJS: `require("node:fs")`). Делите по `/\r?\n/`, убирайте хвост, числа берите через `Number` с проверкой `Number.isFinite`.
* stdout - `console.log`, stderr - `console.error`; код выхода - `process.exitCode`.
* Асинхронные ошибки (промисы, `await`, события) - отдельная тема: `try/catch` вокруг вызова без `await` их не поймает.
