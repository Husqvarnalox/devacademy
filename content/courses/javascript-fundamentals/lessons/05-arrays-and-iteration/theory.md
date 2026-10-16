<!-- section: simple -->
## Массив - упорядоченный список, а обход - договорённость

Массив хранит значения по порядку и умеет много: `map` преобразует каждый элемент, `filter` отбирает, `reduce` сворачивает всё в одно значение, `find`, `some`, `every` ищут и проверяют. Эти методы **не требуют индексов** и записываются как конвейер.

```javascript run
const prices = [120, 80, 45, 300];
const expensive = prices.filter((p) => p > 100);
const withTax = expensive.map((p) => p * 1.2);
const total = withTax.reduce((sum, p) => sum + p, 0);
console.log(expensive, withTax, total);
console.log(prices.some((p) => p < 50), prices.every((p) => p > 50), prices.find((p) => p > 100));
```
```output
[ 120, 300 ] [ 144, 360 ] 504
true false 120
```

Но важнее другое: в JavaScript есть общий **протокол итерации**. `for...of`, спрэд `[...x]`, деструктуризация и `Array.from` работают с любым **итерируемым** объектом: массивом, строкой, `Map`, `Set`, генератором. Протокол - часть языка (ECMAScript), а не библиотека.

Чаще всего ломает код не итерация, а `sort`: без компаратора он сортирует элементы **как строки**.

```javascript run
console.log([10, 9, 1, 100].sort());
console.log([10, 9, 1, 100].sort((a, b) => a - b));
```
```output
[ 1, 10, 100, 9 ]
[ 1, 9, 10, 100 ]
```

<!-- section: precise -->
## Методы, сортировка, протокол итерации, генераторы

### Мутирующие и немутирующие методы

* `push`, `pop`, `shift`, `unshift`, `splice`, `sort`, `reverse`, `fill` **изменяют** массив на месте.
* `map`, `filter`, `slice`, `concat`, `flat`, `flatMap` возвращают **новый** массив.
* Копирующие версии мутирующих операций появились в ECMAScript 2023: `toSorted`, `toReversed`, `toSpliced`, `with(index, value)`. В Node.js 22 они доступны ([MDN: Array](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Array)). Для поиска с конца есть `findLast` и `findLastIndex`.
* `Object.groupBy(items, fn)` (ECMAScript 2024) группирует элементы по ключу в объект.

### sort и компаратор

Без аргумента `sort` приводит элементы к строкам и сравнивает их по кодовым единицам UTF-16: `[10, 9, 1].sort()` даёт `[1, 10, 9]`. Для чисел нужен компаратор `(a, b) => a - b`. Компаратор должен вернуть отрицательное число, ноль или положительное и быть **согласованным** (одинаковые входы - одинаковый ответ, `cmp(a, b)` противоположен `cmp(b, a)`). Булев результат (`a > b`) - ошибка: `true` и `false` приводятся к `1` и `0`, а отрицательного значения нет. Начиная с ES2019 сортировка **стабильна**: равные по ключу элементы сохраняют исходный порядок ([ECMAScript: Array.prototype.sort](https://tc39.es/ecma262/#sec-array.prototype.sort)). Строки для людей сравнивают через `a.localeCompare(b)`. Значения `undefined` всегда уходят в конец, компаратор для них не вызывается.

### reduce и пустой массив

`reduce` без начального значения на пустом массиве бросает `TypeError`. Всегда указывайте начальное значение, если массив может быть пустым.

### Протокол итерации

* **Итерируемый объект** (iterable) имеет метод `[Symbol.iterator]()`, возвращающий итератор.
* **Итератор** имеет метод `next()`, возвращающий `{ value, done }`. Необязательный `return()` вызывается при досрочном выходе (`break`, `return`, исключение, деструктуризация короче источника), чтобы освободить ресурсы.
* Итерируемые: массивы, строки (по символам Unicode, а не по кодовым единицам), `Map`, `Set`, `arguments`, генераторы.
* Потребители: `for...of`, спрэд, `Array.from`, деструктурирование, `new Map(iterable)`, `Promise.all`.

### Генераторы

Функция `function*` возвращает **генератор** - итератор, который выполняет тело по частям: выполнение приостанавливается на `yield` и продолжается при следующем `next()`. Это даёт ленивые, в том числе бесконечные последовательности. Выход из `for...of` вызывает `return()` генератора, и его блоки `finally` выполняются ([MDN: Iteration protocols](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Iteration_protocols)).

```javascript run
function* naturals() {
  try {
    for (let n = 1; ; n++) yield n;
  } finally {
    console.log("закрыто");
  }
}
for (const n of naturals()) {
  if (n > 3) break;
  console.log(n);
}
```
```output
1
2
3
закрыто
```

### Spread, rest и деструктуризация

* Спрэд `...x` в литерале массива или вызове разворачивает итерируемый объект: `[...a, ...b]`, `f(...args)`. Копия получается поверхностной.
* Rest `...rest` в параметрах функции или в деструктуризации собирает остаток: `const [first, ...others] = list`, `function f(a, ...rest)`.
* Деструктуризация позволяет задавать **значения по умолчанию**: `const { a = 1 } = obj`. Умолчание подставляется только когда значение равно `undefined`. Для `null`, `0` и `""` оно **не** применяется.

```javascript run
const { name = "аноним", age = 18, role = "user" } = { age: 0, role: null };
const [first, , third = "нет", ...tail] = [1, 2, undefined, 4, 5];
console.log(name, age, role);
console.log(first, third, tail);
```
```output
аноним 0 null
1 нет [ 4, 5 ]
```

Стандартные итераторы-помощники (`map`, `filter`, `take` и т. д. у самих итераторов) появились в ECMAScript 2025 и в Node.js 22 уже доступны, но для универсального кода (и для браузеров старее) пока полезно уметь написать их самому на генераторах.

<!-- section: examples -->
## Примеры

**Копирующие методы ES2023 и группировка.**

```javascript run
const nums = [3, 1, 2];
const sorted = nums.toSorted((a, b) => a - b);
const reversed = nums.toReversed();
console.log(nums, sorted, reversed, nums.with(0, 99));
const people = [{ n: "Аня", dept: "A" }, { n: "Боб", dept: "B" }, { n: "Вера", dept: "A" }];
const groups = Object.groupBy(people, (p) => p.dept);
console.log(Object.keys(groups), groups.A.map((p) => p.n), Object.getPrototypeOf(groups));
```
```output
[ 3, 1, 2 ] [ 1, 2, 3 ] [ 2, 1, 3 ] [ 99, 1, 2 ]
[ 'A', 'B' ] [ 'Аня', 'Вера' ] null
```

**Собственный итерируемый объект.**

```javascript run
class Range {
  constructor(from, to) { this.from = from; this.to = to; }
  *[Symbol.iterator]() {
    for (let i = this.from; i <= this.to; i++) yield i;
  }
}
const r = new Range(1, 4);
console.log([...r], Math.max(...r), Array.from(r, (x) => x * x));
const [a, b] = r;
console.log(a, b);
```
```output
[ 1, 2, 3, 4 ] 4 [ 1, 4, 9, 16 ]
1 2
```

**Ловушки map и sort.** Порядок сравнения строк в `localeCompare` зависит от локали, а умолчательный `sort` сравнивает кодовые единицы UTF-16: заглавная латиница идёт раньше строчной, а кириллица позже латиницы.

```javascript run
console.log(["1", "2", "3"].map(parseInt));
console.log(["1", "2", "3"].map(Number));
const words = ["б", "а", "В", "a", "Z"];
console.log([...words].sort());
console.log([1, [2, [3, [4]]]].flat(), [1, [2, [3, [4]]]].flat(Infinity));
```
```output
[ 1, NaN, NaN ]
[ 1, 2, 3 ]
[ 'Z', 'a', 'В', 'а', 'б' ]
[ 1, 2, [ 3, [ 4 ] ] ] [ 1, 2, 3, 4 ]
```

`parseInt` принимает вторым аргументом основание, а `map` передаёт индекс, поэтому получается `parseInt("2", 1)` и `parseInt("3", 2)`, то есть `NaN`.

**Индексы и разреженные массивы.**

```javascript run
const arr = [1, 2, 3];
arr.length = 5;
console.log(arr, 3 in arr, arr.map((x) => x * 2));
for (const i in ["a", "b"]) console.log(typeof i, i);
```
```output
[ 1, 2, 3, <2 empty items> ] false [ 2, 4, 6, <2 empty items> ]
string 0
string 1
```

`for...in` перебирает ключи (строки), а не значения; для массивов нужен `for...of`. Пропущенные элементы разреженного массива `map` пропускает.

<!-- section: mistakes -->
## Типичные ошибки

* **`sort()` без компаратора для чисел.** Получаете строковый порядок: `[1, 10, 9]`. Пишите `(a, b) => a - b`.
* **Компаратор, возвращающий булево** (`(a, b) => a > b`). Порядок не определён. Возвращайте число.
* **Ожидание, что `sort` и `reverse` вернут копию.** Они изменяют исходный массив (используйте `toSorted`/`toReversed` или `[...arr].sort()`).
* **`reduce` без начального значения** на возможно пустом массиве.
* **`map(parseInt)`** и другие функции с необязательными параметрами, переданные как колбэк: `map` даёт им `(element, index, array)`.
* **`forEach` с `async`-колбэком.** `forEach` не ждёт промисов. Берите `for...of` с `await` или `Promise.all(arr.map(...))`.
* **Изменение массива во время итерации.** `splice` внутри `forEach` пропускает элементы.
* **`for...in` по массиву.** Получаете строки-ключи, а в цепочке прототипов могут оказаться чужие свойства.
* **Спрэд как «глубокая копия».** `[...arr]` копирует только верхний уровень.
* **Умолчание в деструктуризации для `null`.** `const { x = 1 } = { x: null }` даёт `null`, а не `1`.

<!-- section: summary -->
## Итоги

* Конвейер `filter` -> `map` -> `reduce` выразительнее индексных циклов; `toSorted`, `toReversed`, `with` не изменяют исходный массив.
* `sort` без компаратора сортирует как строки; компаратор возвращает число и должен быть согласованным; сортировка стабильна.
* Итерируемый объект - объект с `[Symbol.iterator]()`; `for...of`, спрэд и деструктуризация работают с любым таким объектом.
* Генераторы дают ленивые последовательности; выход из `for...of` закрывает генератор (`finally` выполняется).
* Значения по умолчанию при деструктуризации подставляются только вместо `undefined`.
