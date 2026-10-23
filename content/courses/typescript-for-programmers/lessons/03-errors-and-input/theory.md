<!-- section: simple -->
## Зачем этот урок

Типы TypeScript проверяют только ваш код. Как только в программу попадают данные извне (текст со стандартного ввода, JSON, ответ сети) или в игру вступает исключение, тип остаётся обещанием без гарантий. Урок показывает, как ловить исключения без потери информации, как собственные ошибки отличаются от чужих и как читать ввод строго. Успешное прохождение (упражнение и квиз) засчитывает уроки 22-23 курса «TypeScript с нуля».

## Главная мысль

Граница программы - единственное место, где тип нужно **доказать**, а не объявить. Всё, что пришло снаружи, имеет тип `unknown` (честный) или `any` (опасный); ваша задача - превратить его в точный тип проверкой. Две привычки решают большую часть проблем: в `catch` значение всегда `unknown`, поэтому его сужают через `instanceof`; а результат `JSON.parse` присваивают переменной типа `unknown` и проверяют.

<!-- section: examples -->
## Пример 1. `catch`, `unknown` и собственная ошибка

```typescript run
class ValidationError extends Error {
  line: number;
  constructor(message: string, line: number) {
    super(message);
    this.name = "ValidationError";
    this.line = line;
  }
}

function risky(kind: number): number {
  if (kind === 1) {
    throw new ValidationError("плохая строка", 7);
  }
  if (kind === 2) {
    throw "просто строка";
  }
  if (kind === 3) {
    throw new Error("обычная ошибка");
  }
  return 0;
}

function describeError(e: unknown): string {
  if (e instanceof ValidationError) {
    return `${e.name} (строка ${e.line}): ${e.message}`;
  }
  if (e instanceof Error) {
    return `${e.name}: ${e.message}`;
  }
  return `не Error: ${String(e)}`;
}

for (let kind = 1; kind <= 4; kind++) {
  try {
    console.log("результат", risky(kind));
  } catch (e) {
    console.log(describeError(e));
  } finally {
    console.log("finally", kind);
  }
}

function early(): string {
  try {
    return "из try";
  } finally {
    console.log("очистка выполнена");
  }
}
console.log(early());
```
```output
ValidationError (строка 7): плохая строка
finally 1
не Error: просто строка
finally 2
Error: обычная ошибка
finally 3
результат 0
finally 4
очистка выполнена
из try
```

Бросить можно любое значение, например строку, поэтому в `catch (e)` тип `e` равен `unknown`: компилятор не позволит прочитать `e.message`, пока вы не докажете, что это `Error`. Проверка `instanceof` идёт от частного к общему: сначала собственный класс, затем `Error`, затем всё остальное. Собственные ошибки задают `name` (иначе он останется `"Error"`) и дополнительные поля. Блок `finally` выполняется всегда, даже если в `try` уже стоит `return`: сначала очистка, потом возврат значения. Пишите `return` внутри `finally` только если понимаете, что он перекроет все остальные.

## Пример 2. `JSON.parse` возвращает `any`

```typescript run
const parsed = JSON.parse('{"name":"Аня","age":"30"}');
const age: number = parsed.age;
console.log(typeof age, age + 1);

function isPerson(v: unknown): v is { name: string; age: number } {
  return (
    typeof v === "object" &&
    v !== null &&
    "name" in v &&
    typeof v.name === "string" &&
    "age" in v &&
    typeof v.age === "number"
  );
}
const safe: unknown = JSON.parse('{"name":"Аня","age":"30"}');
console.log(isPerson(safe), isPerson({ name: "Ян", age: 20 }));

try {
  JSON.parse("{name: 1}");
} catch (e) {
  console.log(e instanceof SyntaxError, e instanceof Error);
}
```
```output
string 301
false true
true true
```

`JSON.parse` объявлена как возвращающая `any`, а `any` совместим с любым типом: запись `const age: number = parsed.age` проходит проверку, хотя в данных строка, и `age + 1` склеивает текст. Лекарство - объявить переменную как `unknown` и сузить её функцией-предикатом `v is ...`, которая проверяет каждое поле во время работы. Неверный JSON бросает `SyntaxError` (а он - наследник `Error`).

## Пример 3. Строки ввода и строгие числа

```typescript run
const text = "яблоко,3\r\n\r\nхлеб,x\r\nмолоко,2\n";
console.log(JSON.stringify(text.split("\n")));
console.log(JSON.stringify(text.trim().split(/\r?\n/)));
const tokens = ["12", "-3", "2.5", "1e3", "0x10", "", " ", "Infinity"];
const strict = /^[+-]?\d+(\.\d+)?$/;
for (const t of tokens) {
  console.log(JSON.stringify(t), Number(t), strict.test(t));
}
```
```output
["яблоко,3\r","\r","хлеб,x\r","молоко,2",""]
["яблоко,3","","хлеб,x","молоко,2"]
"12" 12 true
"-3" -3 true
"2.5" 2.5 true
"1e3" 1000 false
"0x10" 16 false
"" 0 false
" " 0 false
"Infinity" Infinity false
```

Разделение по `"\n"` оставляет `"\r"` в конце строк, если файл создан в Windows, и пустую строку после последнего перевода строки; разделитель `/\r?\n/` понимает оба варианта. `Number` слишком щедр: `""` и `" "` дают `0`, `"1e3"` - `1000`, `"0x10"` - `16`, `"Infinity"` - бесконечность. Для строгого разбора проверяют формат регулярным выражением и только потом приводят к числу.

## Пример 4. Чтение стандартного ввода в Node.js

Эта программа читает ввод целиком и показывает размер результата. Она подключает модуль Node.js, поэтому здесь показана без запуска:

```typescript
import { readFileSync } from "node:fs";

const text: string = readFileSync(0, "utf8");
const lines: string[] = text.split(/\r?\n/);
console.log(lines.length, JSON.stringify(lines));
```

Для ввода `a`, `b` с переводом строки в конце программа печатает `3 ["a","b",""]`. Число `0` в `readFileSync(0, ...)` - дескриптор стандартного ввода. Проверка типов знает про `readFileSync` только при установленных типах Node.js (Настройки - Среды выполнения); возвращаемое значение `string` вы получаете благодаря кодировке `"utf8"`, без неё это `Buffer`.

Попробуйте: добавьте после чтения фильтр, который пропускает пустые строки, и вычислите номера строк так, чтобы пустые всё равно учитывались в нумерации (это нужно в упражнении).

<!-- section: precise -->
## Точные правила

* Под `strict` (`useUnknownInCatchVariables`) значение в `catch` имеет тип `unknown`. Можно написать `catch (e: unknown)` явно, но не `catch (e: Error)`.
* `throw` принимает любое значение. Признак «это ошибка» - `e instanceof Error`; для своих типов - `instanceof` с вашим классом. Интерфейсы и объединения во время работы не существуют, поэтому `instanceof` с ними невозможен.
* В классе, который наследует `Error`, вызов `super(message)` обязателен до обращения к `this`. Свойство `name` стоит задать в конструкторе, иначе в сообщениях останется `"Error"`.
* Класс с полем без начального значения (`line: number;`) в строгом режиме требует присваивания в конструкторе. Приложение запускает файлы со «срезанными» типами, поэтому недопустимы параметры-свойства (`constructor(public line: number)`) и `enum`: они создают код.
* `finally` выполняется при любом выходе из `try` и `catch`; `return` в `finally` перекрывает возврат и подавляет исключение.
* `JSON.parse` возвращает `any`; `JSON.stringify` не различает `undefined` и пропускает функции. Присвойте результат `unknown`.
* Предикат типа `v is T` - обещание: компилятор верит вашему `return`, но не проверяет его. Ошибка в предикате превращается в неверный тип, поэтому проверяйте каждое поле.
* `Number(text)` понимает пустую строку, пробелы, `0x`, `1e3`, `Infinity`; `parseInt` читает префикс; `NaN` проверяйте через `Number.isNaN`.
* `readFileSync(0, "utf8")` блокирует и читает весь ввод; для небольших задач этого достаточно. Ввод заканчивается переводом строки, поэтому после `split` часто остаётся пустой последний элемент.

<!-- section: mistakes -->
## Ошибки и сообщения

**1. Чтение свойства у значения из `catch`.**

```typescript
try {
  throw new Error("x");
} catch (e) {
  console.log(e.message);
}
```

```text
main.ts(4,15): error TS18046: 'e' is of type 'unknown'.
```

Исправление: `if (e instanceof Error) { console.log(e.message); }`.

**2. Ошибка, которую типы не видят.** Данные из `JSON.parse` и чужих функций с типом `any` не проверяются вовсе:

```typescript
const data = JSON.parse('{"count":"5"}');
const count: number = data.count;
console.log(count + 1);
```

Проверка молчит, программа печатает `51`. Объявляйте результат как `unknown` и проверяйте поля.

**3. Строка вместо числа после `Number("")`.** Пустая строка ввода превращается в `0` и проходит как верная оценка. Проверяйте формат регулярным выражением или `trim() === ""` заранее.

**4. Пустая последняя строка.** `"a\nb\n".split("\n")` даёт три элемента. Либо `trim()` перед `split`, либо пропускайте пустые строки в цикле (если нужна нумерация, то с учётом пустых).

**5. `\r` в конце строки.** Файл из Windows содержит `\r\n`; после `split("\n")` в конце каждой строки остаётся `\r`, и `"5\r" === "5"` ложно. Делите по `/\r?\n/` или вызывайте `trim()`.

**6. Проглоченные ошибки программиста.** `catch (e) { /* пусто */ }` скрывает не только ошибки данных, но и опечатки в коде. Ловите конкретные типы ошибок, а чужие перебрасывайте: `throw e`.

<!-- section: summary -->
Теперь вы знаете, как работает `catch` с типом `unknown`, как отличать собственные ошибки через `instanceof`, почему `JSON.parse` нужно проверять, как читать ввод целиком и как разбирать числа строго. Следующий шаг - первые шаги в TypeScript как языке со своей инфраструктурой: [Как выполняется TypeScript: tsc, стирание типов, stdin](lesson:typescript.first-steps.how-typescript-runs). Для подробностей: [Ошибки и исключения](lesson:typescript.from-zero.errors-and-exceptions), [Чтение ввода](lesson:typescript.from-zero.reading-input).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Исключения как в Java и C#, но без проверяемых: тип функции ничего не говорит о возможных `throw`. Бросить можно что угодно, поэтому `catch (e)` даёт `unknown`; шаблон: `e instanceof Error ? e.message : String(e)`.
* Свои ошибки - `class X extends Error` с заданным `name`. Интерфейс не проверить через `instanceof`: во время работы его нет.
* `finally` выполняется при `return`; `return` внутри `finally` перекрывает всё. Асинхронные ошибки - отдельная тема (промисы и `await`).
* `JSON.parse` и многие функции стандартной библиотеки возвращают `any`, который отключает проверку. Присваивайте `unknown` и сужайте предикатом `v is T` или проверкой каждого поля.
* `Number("")` равно `0`, `Number("1e3")` равно `1000`, `Number("0x10")` равно `16`; для строгого разбора - регулярное выражение до приведения.
* Стандартный ввод в Node.js: `readFileSync(0, "utf8")`; делите по `/\r?\n/` и помните про пустой хвост.
* Типы не заменяют валидацию: схема данных на границе (ввод, сеть, файл) пишется отдельно.
* В приложении TypeScript запускается со «срезанными» типами (`--experimental-strip-types`): `enum`, `namespace` и параметры-свойства конструкторов не поддерживаются.

<!-- section: deep-dive -->
## Углубление: Result вместо исключения

Исключения удобны для действительно исключительных ситуаций. Для ожидаемых ошибок данных (строка ввода не разобралась) часто возвращают результат с признаком: `{ ok: true, value } | { ok: false, error }`. Компилятор тогда заставляет вызывающий код проверить `ok` прежде чем обращаться к `value`. Объединения с признаком и сужение разбираются в `typescript-fundamentals`.
