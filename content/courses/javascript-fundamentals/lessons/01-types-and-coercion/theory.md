<!-- section: simple -->
## Значения бывают двух видов

В JavaScript каждое значение - либо **примитив**, либо **объект**.

* Примитивы: `undefined`, `null`, `boolean`, `number`, `bigint`, `string`, `symbol`. Они неизменяемы и сравниваются по значению.
* Всё остальное - объекты: `{}`, массивы, функции, даты. В переменной лежит ссылка на объект.

Язык **динамически типизирован**: тип есть у значения, а не у переменной. Поэтому в выражениях вроде `"5" * 2` или `[] + {}` движок сам превращает значения из одного типа в другой. Это называется **приведением типов** (coercion), и оно устроено по строгим правилам из спецификации ECMAScript, а не случайно.

```javascript run
console.log(typeof 42, typeof "hi", typeof undefined, typeof 10n);
console.log(typeof null, typeof [], typeof function () {});
console.log("5" * 2, "5" + 2, 5 + 2);
```
```output
number string undefined bigint
object object function
10 52 7
```

Здесь `"5" * 2` даёт число, а `"5" + 2` - строку: оператор `*` всегда работает с числами, а `+` при наличии строки склеивает.

Что относится к языку, а что к среде. Типы, `typeof`, приведения, `Number`, `BigInt` определены в стандарте ECMAScript и работают одинаково в браузере и в Node.js. А `console.log`, `setTimeout`, `process` - это API среды выполнения, не часть языка.

<!-- section: precise -->
## Типы, приведения и числа точнее

### typeof и его особенности

Оператор `typeof` возвращает строку: `"undefined"`, `"boolean"`, `"number"`, `"bigint"`, `"string"`, `"symbol"`, `"object"` или `"function"`. Две особенности:

* `typeof null === "object"`. Это историческая ошибка ранних версий языка, которую сохранили ради совместимости ([MDN: typeof](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Operators/typeof)). `null` по спецификации - отдельный примитивный тип Null.
* Функции - тоже объекты, но `typeof` для них возвращает `"function"`. Массивы дают `"object"`; их отличают через `Array.isArray`.

### Три абстрактные операции приведения

В спецификации приведения описаны как абстрактные операции (ECMAScript, раздел «Type Conversion»: [tc39.es/ecma262](https://tc39.es/ecma262/#sec-type-conversion)). Их стоит знать по именам:

* **ToString** - в строку: `String(123)` даёт `"123"`, `String(null)` - `"null"`, `String([1, 2])` - `"1,2"`.
* **ToNumber** - в число. Пустая строка и строка из пробелов дают `0`, непонятная строка (`"12px"`) - `NaN`, `null` даёт `0`, `undefined` - `NaN`, `true` - `1`, `false` - `0`.
* **ToBoolean** - в булево. Ложными (falsy) являются ровно: `false`, `0`, `-0`, `0n`, `""`, `null`, `undefined`, `NaN`. Всё остальное, включая `"0"`, `[]` и `{}`, истинно.

Когда операция получает **объект**, но нужен примитив, вызывается **ToPrimitive**. У неё есть подсказка (hint): `"string"`, `"number"` или `"default"`. Для обычного объекта порядок такой: при подсказке `"string"` сначала вызывается `toString()`, затем `valueOf()`; при `"number"` и `"default"` - наоборот. Объект может переопределить поведение методом `[Symbol.toPrimitive](hint)`. Например, у массива `valueOf()` возвращает сам массив (не примитив), поэтому в итоге работает `toString()`: `[1, 2]` превращается в `"1,2"`.

```javascript run
const price = {
  amount: 250,
  [Symbol.toPrimitive](hint) {
    return hint === "string" ? `${this.amount} руб.` : this.amount;
  },
};
console.log(`${price}`);   // подсказка "string"
console.log(price + 1);    // подсказка "default"
console.log(price * 2);    // подсказка "number"
```
```output
250 руб.
251
500
```

### Операторы + и ==

* **`+`**: оба операнда проходят ToPrimitive (с подсказкой `"default"`). Если после этого хотя бы один - строка, происходит конкатенация (оба операнда приводятся ToString), иначе оба приводятся к числам и складываются. Все остальные арифметические операторы (`-`, `*`, `/`) всегда приводят к числам.
* **`==`** (абстрактное равенство, [IsLooselyEqual](https://tc39.es/ecma262/#sec-islooselyequal)) для разных типов действует по шагам: `null == undefined` истинно и больше `null`/`undefined` ни с чем не равны; число и строка - строка идёт в ToNumber; булево идёт в ToNumber; объект и примитив - объект идёт в ToPrimitive. Для одинаковых типов `==` ведёт себя как `===`.

Рекомендация практики: используйте `===`, а `== null` оставляйте как осознанную краткую проверку «null или undefined».

### Числа: NaN, -0 и точность

`number` - это 64-битное число с плавающей точкой двойной точности по стандарту IEEE 754. Отдельного целого типа у `number` нет.

* **`NaN`** («не число») - значение типа `number`; `NaN !== NaN`. Проверять нужно через `Number.isNaN(x)` (он не приводит аргумент, в отличие от глобальной `isNaN`) или `Object.is(x, NaN)`.
* **`-0`** существует: `0 === -0` истинно, различает их `Object.is(0, -0)`. Например, `Math.round(-0.4)` даёт `-0`, а `1 / -0` равно `-Infinity`.
* **Точность.** Числа хранятся в двоичном виде, поэтому `0.1 + 0.2 !== 0.3`. Целые числа безопасно представимы до `Number.MAX_SAFE_INTEGER` (2^53 - 1 = 9007199254740991). Выше этого значения разные целые могут совпадать: `2**53 === 2**53 + 1`. Деньги хранят в целых минимальных единицах (копейках).
* **`BigInt`** - отдельный примитивный тип для целых произвольной длины: литерал `10n`, функция `BigInt("123")`. Смешивать `BigInt` и `number` в арифметике нельзя (`TypeError`), приведение явное; `JSON.stringify` не умеет сериализовать `BigInt`, а `Math.*` с ним не работает.

```javascript run
console.log(0.1 + 0.2, 0.1 + 0.2 === 0.3);
console.log(Number.MAX_SAFE_INTEGER, 2 ** 53 === 2 ** 53 + 1);
console.log(2n ** 64n, typeof (2n ** 64n));
console.log(NaN === NaN, Object.is(NaN, NaN), 0 === -0, Object.is(0, -0));
```
```output
0.30000000000000004 false
9007199254740991 true
18446744073709551616n bigint
false true true false
```

<!-- section: examples -->
## Примеры

**Приведения в арифметике и сравнениях.**

```javascript run
console.log(1 + "2", "3" - 1, "3" + 1, +"", +" ", +"12px", +[], +[5], +null, +undefined);
console.log(JSON.stringify([] + []), [1, 2] + [3], {} + "x");
console.log(null == undefined, null == 0, "" == 0, "0" == false, [] == false, [1] == 1);
```
```output
12 2 31 0 0 NaN 0 5 0 NaN
"" 1,23 [object Object]x
true false true true true true
```

**Разница `Number`, `parseInt` и унарного плюса.**

```javascript run
console.log(Number("12px"), parseInt("12px", 10), parseFloat("3.5abc"));
console.log(Number(""), parseInt(""), Number("  7  "), Number("0x10"), Number("1e3"));
console.log(Number(true), Number(null), Number(undefined), Number([]), Number([1, 2]));
```
```output
NaN 12 3.5
0 NaN 7 16 1000
1 0 NaN 0 NaN
```

`Number("12px")` отвергает строку целиком, `parseInt` читает число с начала строки и останавливается на первом лишнем символе.

**BigInt и граница безопасных целых.**

```javascript run
const big = BigInt(Number.MAX_SAFE_INTEGER) + 2n;
console.log(big, Number(big), Number.isSafeInteger(Number(big)));
try {
  1n + 1;
} catch (e) {
  console.log(e.name + ": " + e.message);
}
console.log(1n + BigInt(1), 7n / 2n, typeof Number(7n));
```
```output
9007199254740993n 9007199254740992 false
TypeError: Cannot mix BigInt and other types, use explicit conversions
2n 3n number
```

**Falsy-значения.**

```javascript run
for (const v of [0, -0, 0n, "", null, undefined, NaN, "0", [], {}]) {
  console.log((Object.is(v, -0) ? "-0" : typeof v === "string" ? JSON.stringify(v) : String(v)).padEnd(16), Boolean(v));
}
```
```output
0                false
-0               false
0                false
""               false
null             false
undefined        false
NaN              false
"0"              true
                 true
[object Object]  true
```

<!-- section: mistakes -->
## Типичные ошибки

* **Проверка `typeof x === "object"` без исключения `null`.** Для `null` она истинна, а `null.foo` бросает `TypeError`. Пишите `x !== null && typeof x === "object"`.
* **`Number(input)` для разбора пользовательского ввода.** Пустая строка превращается в `0`, и «ничего не введено» неотличимо от нуля. Сначала проверьте `trim() !== ""`.
* **Сравнение с `NaN` через `===`.** `x === NaN` всегда ложно. Используйте `Number.isNaN(x)`.
* **Дробные числа для денег.** `0.1 + 0.2` не равно `0.3`. Храните целые копейки или используйте `BigInt`.
* **Целые больше `2**53` как `number`.** Идентификаторы из JSON-ответов (например, 64-битные id) теряют младшие разряды при `JSON.parse`. Передавайте такие значения строками.
* **Смесь `BigInt` и `number`.** `1n + 1` бросает `TypeError`; приводите явно.
* **Опора на сложные правила `==`.** Результаты вроде `[] == false` истинны по спецификации, но читателям кода их не вычислить. Используйте `===`.
* **Глобальная `isNaN("abc")` истинна.** Она сначала приводит аргумент к числу. Используйте `Number.isNaN`.

<!-- section: summary -->
## Итоги

* Семь примитивов и объекты; `typeof null` равен `"object"` по историческим причинам, массивы - тоже `"object"`.
* Приведения - это ToString, ToNumber, ToBoolean и ToPrimitive для объектов; `+` склеивает, если среди примитивов есть строка, остальные арифметические операторы приводят к числам.
* `==` приводит операнды по таблице шагов, `===` не приводит; используйте `===`.
* `number` - IEEE 754 double: есть `NaN`, `-0`, ограничение `Number.MAX_SAFE_INTEGER`; для больших целых есть `BigInt`.
* Первоисточники: спецификация [ECMAScript](https://tc39.es/ecma262/) и справочник [MDN](https://developer.mozilla.org/en-US/docs/Web/JavaScript).
