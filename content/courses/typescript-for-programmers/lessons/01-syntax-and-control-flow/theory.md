<!-- section: simple -->
## Зачем этот урок

Вы уже пишете на другом языке (Python, Java, C#, Go, чаще всего JavaScript), поэтому циклы, условия и функции в TypeScript узнаете сразу. Урок собирает то, что здесь устроено иначе. Главное отличие не в синтаксисе, а в том, **кто и когда проверяет типы**. Успешное прохождение (упражнение и квиз) засчитывает уроки 01-13 курса «TypeScript с нуля».

## Главная мысль

TypeScript - это JavaScript плюс система типов, которая работает **до запуска** и **полностью исчезает** при запуске. Программу сначала читает компилятор `tsc`: он находит несоответствия типов и сообщает о них, но не меняет поведения программы. Затем Node.js выполняет тот же код, из которого просто вычеркнуты аннотации типов. Отсюда три следствия:

* тип - это обещание программиста, которое проверено только внутри вашего кода. Данные, пришедшие снаружи (ввод, файл, JSON), никто не проверяет, пока вы не напишете проверку сами;
* всё, что в JavaScript происходит во время работы (`NaN`, `0.1 + 0.2`, приведение при `+`, хитрости `for...in`), в TypeScript остаётся как есть: система типов ловит только часть этих ловушек;
* ошибка типов - это ошибка проверки, а не исключение: если она есть, программа в этом приложении не запускается.

<!-- section: examples -->
## Пример 1. Типы стираются

```typescript run
const raw: string = "5";
const lie = raw as unknown as number;
console.log(typeof raw, typeof lie);
console.log(lie + 1);
console.log(Number(raw) + 1);
```
```output
string string
51
6
```

Запись `raw as unknown as number` - «утверждение типа»: вы приказываете компилятору считать значение числом. Компилятор подчиняется, ведь он не знает, что внутри. А во время работы `lie` по-прежнему строка (`typeof` показывает это), поэтому `lie + 1` склеивает текст и даёт `"51"`. Утверждения типа (`as`) используют редко и осознанно: они отключают проверку именно там, где она нужна. Настоящее преобразование делает `Number(raw)`.

## Пример 2. Числа: один тип на всё

```typescript run
const price: number = 19.99;
const count: number = 3;
console.log(price * count);
console.log(0.1 + 0.2, 0.1 + 0.2 === 0.3);
console.log(Math.round((0.1 + 0.2) * 100) / 100);
console.log(7 / 2, Math.trunc(7 / 2), -7 % 3);
console.log(2 ** 53 === 2 ** 53 + 1, Number.MAX_SAFE_INTEGER);
console.log(5 / 0, -5 / 0, 0 / 0);
console.log(Number.isNaN(0 / 0), Number.isInteger(2.0), Number.isInteger(2.5));
console.log(typeof 5, typeof "5", typeof NaN, typeof null, typeof undefined);
```
```output
59.97
0.30000000000000004 false
0.3
3.5 3 -1
true 9007199254740991
Infinity -Infinity NaN
true true false
number string number object undefined
```

В TypeScript один числовой тип `number`: нет `int`, `long`, `float` (есть ещё `bigint` для очень больших целых, но он не смешивается с `number`). Это 64-битная дробь IEEE 754, как `double` в Java или C#. Поэтому `7 / 2` равно `3.5`, а целую часть берут через `Math.trunc` или `Math.floor`; `0.1 + 0.2` неточно, как и везде; целые безопасны до `2 ** 53`. Деление на ноль не бросает исключение, а даёт `Infinity` или `NaN`. Остаток `%` берёт знак делимого: `-7 % 3` равно `-1`. Тип `NaN` - `number`, а `typeof null` даёт `"object"`; это историческая ошибка JavaScript, исправлять её не стали.

## Пример 3. Преобразования при `+` и `Number`

```typescript run
console.log(1 + "2", "3" + 4 + 5, 3 + 4 + "5");
console.log(Number("42"), Number(""), Number("  "), Number("12px"), parseInt("12px"), Number("0x10"));
const empty = "";
const zeroText = "0";
const list: number[] = [];
console.log(Boolean(empty), Boolean(zeroText), Boolean(list), Boolean(0), Boolean(NaN));
const stored = 0;
console.log(stored || 10, stored ?? 10);
```
```output
12 345 75
42 0 0 NaN 12 16
false true true false false
10 0
```

Оператор `+` с любой строкой склеивает: сложение идёт слева направо, поэтому `3 + 4 + "5"` - это `7 + "5"`, то есть `"75"`. Типы это разрешают: `number + string` законно и даёт `string`. Зато `"5" * 2` и `"5" - 2` компилятор отвергает (см. «Ошибки»). `Number("")` и `Number("  ")` дают `0`, поэтому пустой ввод незаметно становится числом; `Number("12px")` строг и даёт `NaN`, а `parseInt` читает префикс и возвращает `12`; `Number("0x10")` понимает шестнадцатеричную запись. Ложны только `""`, `0`, `NaN`, `null`, `undefined` и `false`, а `"0"` и пустой массив истинны. Оператор `||` подставляет запасное значение для любого ложного, а `??` - только для `null` и `undefined`: `0 || 10` даёт `10`, `0 ?? 10` даёт `0`.

## Пример 4. `const`, литеральные типы и циклы

```typescript run
const mode = "read";
let current = "read";
current = "write";
console.log(mode, current);

const prices: number[] = [10, 20];
for (const i in prices) {
  console.log(typeof i, i);
}
for (const p of prices) {
  console.log(typeof p, p);
}

function describe(day: number): string {
  let result = "";
  switch (day) {
    case 6:
    case 7:
      result = "выходной";
      break;
    case 5:
      result = "пятница ";
    default:
      result += "рабочий";
  }
  return result;
}
console.log(describe(7), "|", describe(5), "|", describe(2));
```
```output
read write
string 0
string 1
number 10
number 20
выходной | пятница рабочий | рабочий
```

Для `const mode = "read"` TypeScript выводит не `string`, а **литеральный тип** `"read"`: константа не изменится, поэтому тип сужен до одного значения. Для `let current = "read"` выводится широкий `string`. Эта разница важна при сравнениях (пример в «Ошибках»). `for...in` перебирает **ключи**, а не значения, причём ключи массива - строки (`"0"`, `"1"`), поэтому для массивов берут `for...of`. В `switch` нет автоматического выхода: без `break` выполнение «проваливается» в следующую ветку, и `describe(5)` печатает «пятница рабочий». Сравнивает `switch` строго (как `===`).

Попробуйте: замените в `describe` `break` на отсутствие и посмотрите, как изменится результат для `7`; затем замените `for...of` на обычный `for (let i = 0; ...)`.

<!-- section: precise -->
## Точные правила

* `tsc --strict` включает `strictNullChecks` (`null` и `undefined` не входят в другие типы), `noImplicitAny` (параметр без типа - ошибка, а не молчаливый `any`) и родственные проверки. Приложение всегда запускает код со `strict`.
* Ошибка типов не меняет и не останавливает JavaScript сама по себе. Сгенерированный или «срезанный» код выполнился бы. Приложение же запускает программу только после успешной проверки.
* Тип вычисляется по значению: `let n = 5` - `number`, `const n = 5` - литерал `5`, `const word = "a"` - `"a"`. Аннотацию пишут для параметров и результатов функций, пустых коллекций и там, где вывод слишком узкий.
* `===` не приводит типы, но TypeScript дополнительно запрещает сравнение значений, у которых нет общих вариантов (`"read" === "write"`, `number === string`): ошибка `TS2367`. `==` для разных типов тоже отвергается; в курсе `==` не используют.
* Арифметика `-`, `*`, `/`, `%`, `**` требует `number` или `bigint`; для `string` и `boolean` ошибка `TS2362`. Оператор `+` допускает смесь `number` и `string`.
* `NaN === NaN` всегда ложно; компилятор замечает это прямо в записи и сообщает `TS2845`. Проверяйте `Number.isNaN(x)`.
* Целая часть: `Math.trunc` (к нулю), `Math.floor` (вниз), `Math.round` (к ближайшему; `.5` округляется вверх, `Math.round(-2.5)` равно `-2`). Денежные суммы лучше хранить целыми копейками.
* Версия библиотеки - `es2022`: нет `toSorted`, `toReversed`, `Object.groupBy` (см. «Ошибки»); `at`, `replaceAll`, `Object.hasOwn` есть.

<!-- section: mistakes -->
## Ошибки и сообщения

**1. Сравнение со значением, которого у константы быть не может.**

```typescript
const mode = "read";
if (mode === "write") {
  console.log("запись");
}
```

```text
main.ts(2,5): error TS2367: This comparison appears to be unintentional because the types '"read"' and '"write"' have no overlap.
```

У `mode` литеральный тип `"read"`, поэтому сравнение с `"write"` невозможно. Если значение приходит снаружи, объявите переменную с типом `string`: `const mode: string = ...`.

**2. Арифметика над строкой.**

```typescript
let level = "debug";
const label = level * 2;
```

```text
main.ts(2,15): error TS2362: The left-hand side of an arithmetic operation must be of type 'any', 'number', 'bigint' or an enum type.
```

Переведите явно: `Number(level) * 2`, и проверьте результат через `Number.isNaN`.

**3. Параметр без типа.**

```typescript
function double(x) {
  return x * 2;
}
```

```text
main.ts(1,17): error TS7006: Parameter 'x' implicitly has an 'any' type.
```

Режим `strict` не позволяет молча превратить параметр в `any`: напишите `x: number`.

**4. Сравнение с `NaN`.**

```typescript
const value = Number("abc");
if (value === NaN) {
  console.log("не число");
}
```

```text
main.ts(2,5): error TS2845: This condition will always return 'false'.
```

Правильно: `Number.isNaN(value)`.

**5. Метод, которого нет в `es2022`.**

```typescript
const sorted = [3, 1, 2].toSorted();
```

```text
main.ts(1,26): error TS2550: Property 'toSorted' does not exist on type 'number[]'. Do you need to change your target library? Try changing the 'lib' compiler option to 'es2023' or later.
```

Отсортированную копию получают так: `[...numbers].sort((a, b) => a - b)`.

**6. Ошибки, которые проверка не видит.** `0.1 + 0.2 === 0.3`, `Number("")` равно `0`, `for...in` по массиву, забытый `break` в `switch`, `"5" + 1` равно `"51"`: всё это типы разрешают, и ловить такие случаи должны вы и тесты.

<!-- section: summary -->
Теперь вы знаете, что типы TypeScript существуют только до запуска, что `number` - одно 64-битное число с `NaN` и неточными дробями, что `+` склеивает, `===` строг, а `const` даёт литеральный тип. Следующий шаг - массивы, объекты и функции: [Массивы, объекты и функции: типы, ссылки, сортировка](lesson:typescript.programmers.collections-and-functions). Подробнее об отдельных темах: [Первая программа](lesson:typescript.from-zero.first-program), [Чтение ошибок](lesson:typescript.from-zero.reading-errors), [Значения и типы](lesson:typescript.from-zero.values-and-types), [Переменные](lesson:typescript.from-zero.variables), [Проверка типов](lesson:typescript.from-zero.type-checking), [Арифметика](lesson:typescript.from-zero.arithmetic), [Строки](lesson:typescript.from-zero.strings), [Сравнение и логика](lesson:typescript.from-zero.comparison-and-logic), [Функции](lesson:typescript.from-zero.functions), [Условия if/else](lesson:typescript.from-zero.if-else), [Цикл while](lesson:typescript.from-zero.while-loop), [Цикл for](lesson:typescript.from-zero.for-loop), [Управление циклами](lesson:typescript.from-zero.loop-control).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Типы проверяются только при компиляции и стираются при запуске. В отличие от Java, C#, Go, в готовой программе нет информации о типах: `instanceof` работает с классами, но не с интерфейсами и объединениями. Данные снаружи (ввод, JSON, сеть) не проверяются: проверка - ваша работа.
* Один числовой тип `number` (64-битная дробь). Нет `int` и деления с остатком: `7 / 2` равно `3.5`. Целые безопасны до `Number.MAX_SAFE_INTEGER`. Для денег - целые копейки.
* Деление на ноль не бросает исключение: `Infinity`, `-Infinity` или `NaN`. `NaN` не равен самому себе; проверка - `Number.isNaN`.
* `+` допускает `number + string` и склеивает. `-`, `*`, `/` над строкой - ошибка компиляции, но `Number("")` равно `0` и `Number("12px")` равно `NaN`.
* Только `===` и `!==`; `==` в проекте запрещают. Ложны `false`, `0`, `""`, `NaN`, `null`, `undefined`; `"0"` и `[]` истинны. `??` отличает `null`/`undefined` от `0` и `""`, `||` - нет.
* `let`/`const` блочные, `var` не используют. `const` с примитивом даёт литеральный тип.
* `switch` проваливается без `break`; `for...in` - ключи (строки), `for...of` - значения.
* `typeof null` равно `"object"`; для `null` проверяют `=== null`.

<!-- section: deep-dive -->
## Углубление: что делает «срезание» типов

Node.js с флагом `--experimental-strip-types` заменяет аннотации пробелами и выполняет получившийся JavaScript, не читая типы. Поэтому поддерживается только «стираемый» синтаксис: `enum` и пространства имён (`namespace`) создают код, а не просто аннотации, и такой режим их не принимает. Полноценный `tsc` дополнительно умеет генерировать `.js`, но в этом курсе его используют как проверку (`--noEmit`).
