<!-- section: simple -->
## Тип как параметр

Функция `first(xs)` должна работать с массивом чего угодно: чисел, строк, пользователей. Если написать `xs: any[]`, она вернёт `any`, и вся информация о типе потеряется. Если написать три версии, получится дублирование. Решение - **обобщение** (generic): тип становится параметром функции, как значение становится аргументом.

```typescript
function first<T>(xs: readonly T[]): T | undefined {
  return xs[0];
}
```

Здесь `T` - параметр типа. Вы его не передаёте: компилятор **выводит** его из аргументов. Для `first([1, 2])` получится `T = number`, и результат будет `number | undefined`; для `first(["a"])` - `string | undefined`. Связь «что вошло, то и вышло» компилятор проверяет сам.

Обобщения становятся полезнее с **ограничениями**: `K extends keyof T` говорит, что `K` - только имя существующего поля `T`. Тогда `pluck(users, "name")` вернёт `string[]`, а `pluck(users, "email")` не скомпилируется.

<!-- section: precise -->
## Обобщённые функции, ограничения, keyof и значения по умолчанию

**Параметры типа и вывод.** Тип `T` в `function first<T>(xs: readonly T[])` - это переменная на уровне типов, которая стирается при компиляции, как и все типы. Обычно её не указывают явно: `first<number>(xs)` нужен редко, когда вывод неточен. Если `T` встречается в нескольких параметрах, он должен подходить им всем: `same<T>(a: T, b: T)` и вызов `same(1, "x")` дают TS2345 (`'"x"' is not assignable to parameter of type '1'`); явно `same<string | number>(1, "x")` скомпилируется.

**Неограниченный `T` ничего не умеет.** Внутри `function len<T>(x: T)` про `T` известно только, что он существует: `x.length` - ошибка TS2339. Чтобы пользоваться свойствами, `T` ограничивают: `<T extends { length: number }>`. Вызов `len2(5)` тогда не компилируется (TS2345), а `len2("abc")` и `len2([1])` проходят: ограничение проверяется структурно.

**`keyof` и индексированный доступ.** `keyof T` - объединение имён полей `T`. Запись `T[K]` - тип значения поля `K`. Вместе: `function get<T, K extends keyof T>(o: T, k: K): T[K]`. Для `get({ a: 1, b: "x" }, "b")` вывод даст `K = "b"` и результат `string`; для несуществующего поля - ошибка TS2345 уже на вызове. Это самый частый способ связать два параметра: второй зависит от первого.

**Значения по умолчанию.** Как у обычных параметров, у параметров типа бывают значения: `type Result<T, E = Error> = ...`. Тогда `Result<number>` означает `Result<number, Error>`. Параметры с умолчанием должны идти в конце.

**Результат, зависящий только от `T`, - обман.** Функция `function make<T>(): T` ничего не получает на вход, из чего можно вывести `T`: вызов `make()` даёт `unknown`, а `make<User>()` - непроверенное утверждение, как `as User`. Параметр типа должен встречаться во входе (в идеале хотя бы дважды: вход и выход); иначе используйте `unknown` и проверку из третьего урока.

**Литералы и `const T`.** Параметр без ограничения может сохранять литеральный тип (`id("lit")` даёт `"lit"` при `const`), а параметр с ограничением `T extends string` выводит литерал всегда. Модификатор `const T` (TypeScript 5.0) выводит литеральные и `readonly`-типы вложенных структур, как `as const`: для `idc({ a: [1, 2] })` вы получите `{ readonly a: readonly [1, 2] }`.

**Обобщённые типы.** Параметры бывают у `type`, `interface` и классов: `Box<T>`, `Map<K, V>`, `Promise<T>`. Правила те же: параметр задаёт связь между частями типа, а значения по умолчанию и ограничения применимы везде.

<!-- section: examples -->
## Примеры

**Пример 1. first и вывод параметра.**

```typescript run
function first<T>(xs: readonly T[]): T | undefined {
  return xs[0];
}

const a = first([10, 20]);        // number | undefined
const b = first(["x", "y"]);      // string | undefined
const c = first([]);              // undefined: T выведен как never

console.log(a, b, c);
console.log(a === undefined ? "пусто" : a.toFixed(1));
```
```output
10 x undefined
10.0
```

**Пример 2. keyof, K extends keyof T и T[K].**

```typescript run
function pluck<T, K extends keyof T>(items: readonly T[], key: K): T[K][] {
  return items.map((item) => item[key]);
}

const users = [
  { name: "Ада", age: 36 },
  { name: "Билл", age: 29 },
];

const names: string[] = pluck(users, "name");
const ages: number[] = pluck(users, "age");
// pluck(users, "email");   // TS2345: Argument of type '"email"' is not assignable to parameter of type '"age" | "name"'

console.log(names, ages);
```
```output
[ 'Ада', 'Билл' ] [ 36, 29 ]
```

**Пример 3. Ограничение extends.**

```typescript run
function longest<T extends { length: number }>(a: T, b: T): T {
  return a.length >= b.length ? a : b;
}

console.log(longest("abc", "de"));
console.log(longest([1, 2], [3, 4, 5]));
// longest(1, 2);   // TS2345: Argument of type 'number' is not assignable to parameter of type '{ length: number; }'
```
```output
abc
[ 3, 4, 5 ]
```

**Пример 4. Обобщённый тип со значением по умолчанию.**

```typescript run
type Result<T, E = Error> = { ok: true; value: T } | { ok: false; error: E };

function parseIntSafe(text: string): Result<number> {
  const n = Number.parseInt(text, 10);
  return Number.isNaN(n) ? { ok: false, error: new Error(`не число: ${text}`) } : { ok: true, value: n };
}

function unwrapOr<T, E>(r: Result<T, E>, fallback: T): T {
  return r.ok ? r.value : fallback;
}

console.log(unwrapOr(parseIntSafe("42"), 0));
console.log(unwrapOr(parseIntSafe("abc"), 0));
```
```output
42
0
```

**Пример 5. Группировка с обобщённым ключом.**

```typescript run
function groupBy<T, K>(items: readonly T[], keyOf: (item: T) => K): Map<K, T[]> {
  const groups = new Map<K, T[]>();
  for (const item of items) {
    const key = keyOf(item);
    const bucket = groups.get(key);
    if (bucket === undefined) groups.set(key, [item]);
    else bucket.push(item);
  }
  return groups;
}

const g = groupBy([1, 2, 3, 4, 5], (n) => (n % 2 === 0 ? "чёт" : "нечёт"));
console.log(g);
```
```output
Map(2) { 'нечёт' => [ 1, 3, 5 ], 'чёт' => [ 2, 4 ] }
```

<!-- section: mistakes -->
## Типичные ошибки

* **`any` вместо параметра типа.** `function first(xs: any[]): any` компилируется, но тип результата потерян и ошибки уходят. Проверка в упражнении сравнивает точные выведенные типы.
* **Параметр типа, который встречается один раз.** `function parse<T>(s: string): T` - замаскированное приведение типа. Если `T` не связывает вход и выход, он не нужен.
* **Забыть ограничение.** `x.length` на `T` без `extends` - ошибка TS2339; решение - ограничить `T`, а не привести `(x as any).length`.
* **Лишние явные аргументы.** `first<number>([1, 2])` повторяет то, что компилятор выводит сам. Явный аргумент уместен, если вывод не работает (пустой массив, `JSON.parse`).
* **Путать `K extends keyof T` и `K = keyof T`.** Первое - ограничение (подойдёт любой ключ, и `K` станет конкретным), второе - значение по умолчанию.
* **Забывать про `undefined` при чтении по индексу.** `xs[0]` для `readonly T[]` при `noUncheckedIndexedAccess` имеет тип `T | undefined`, и это нужно отразить в сигнатуре результата.

<!-- section: summary -->
## Коротко

* Обобщение связывает типы входа и выхода: `first<T>(xs: readonly T[]): T | undefined`.
* Компилятор выводит параметры типа из аргументов; явно их пишут редко.
* `T extends X` даёт доступ к членам `X`, `K extends keyof T` связывает ключ с объектом, `T[K]` - тип значения.
* Параметры типа могут иметь значения по умолчанию (`Result<T, E = Error>`).
* Параметр типа, не связывающий вход и выход, - скрытое приведение типа.
