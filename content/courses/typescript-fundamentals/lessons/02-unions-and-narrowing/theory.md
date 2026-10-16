<!-- section: simple -->
## Значение может быть одним из нескольких видов

В JavaScript одна и та же переменная легко бывает то строкой, то числом, то `undefined`. TypeScript описывает это **объединением типов** (union): `string | number` читается «строка или число». Пока вы не знаете, какой именно вид у значения, компилятор разрешает только то, что есть у всех видов. Чтобы пользоваться особенностями конкретного вида, значение **сужают** (narrowing): проверяют в коде, и внутри ветки компилятор уже знает точный тип.

Самый удобный способ описать «один из нескольких вариантов» - **дискриминированное объединение**: у каждого варианта есть общее поле с уникальным литеральным значением (`kind: "circle"`, `kind: "rect"`). Проверка этого поля сужает вариант целиком. А если добавить в `switch` ветку, которая принимает значение типа `never`, компилятор сам сообщит, что вы забыли обработать новый вариант.

```typescript
type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number };

function area(s: Shape): number {
  if (s.kind === "circle") return Math.PI * s.r ** 2; // здесь s - круг
  return s.w * s.h;                                   // здесь s - прямоугольник
}
```

<!-- section: precise -->
## Объединения, литеральные типы и сужение

**Объединение и общие члены.** Для значения типа `string | number` доступны только члены, общие для обоих видов. Обращение к остальным - ошибка:

```typescript
function f(x: string | number) { return x.toFixed(1); }
// error TS2339: Property 'toFixed' does not exist on type 'string | number'.
//   Property 'toFixed' does not exist on type 'string'.
```

**Литеральные типы.** Тип может состоять из одного значения: `"left"`, `42`, `true`. Объединение литералов - это перечисление без дополнительного кода: `type Direction = "up" | "down" | "left" | "right"`. Переменная `const dir = "left"` имеет тип `"left"`, а `let dir2 = "left"` - `string` (литерал расширяется, как в прошлом уроке).

**Способы сужения.** Компилятор отслеживает поток управления и понимает такие проверки:

* `typeof x === "string"` (также `"number"`, `"boolean"`, `"bigint"`, `"symbol"`, `"function"`, `"object"`, `"undefined"`). Для `"object"` помните: `typeof null === "object"`, так что `null` остаётся в ветке;
* равенство `x === y`, `x === undefined`, `x !== null` (и в сочетании с литералами: `s.kind === "circle"`);
* истинность: `if (x)` отбрасывает `null`, `undefined`, но также `0`, `""`, `false` и `NaN` - это распространённая ловушка;
* оператор `in`: `"swim" in animal` сужает до вариантов, у которых есть свойство `swim`;
* `instanceof`: `a instanceof Dog` сужает до класса;
* `Array.isArray(x)` отделяет массивы от остального;
* присваивание: после `x = "text"` тип `x` в этой точке сужен до `string`;
* пользовательская функция-предикат `x is T` (подробнее - в следующем уроке).

**Дискриминированное объединение** требует общего поля-литерала (дискриминанта). Сужение работает и в `if`, и в `switch`. Все варианты остаются обычными объектами, поэтому структура данных не нуждается ни в классах, ни в дополнительном коде во время работы.

**Исчерпывающая проверка через `never`.** После того как все варианты разобраны, значение в `default` имеет тип `never`. Функция `assertNever(x: never): never` принимает только `never`, поэтому добавленный, но не обработанный вариант делает вызов некомпилируемым. Если добавить в `Shape` вариант `{ kind: "tri"; b: number }` и забыть про него, `tsc` 7.0.2 ответит: `Argument of type '{ kind: "tri"; b: number; }' is not assignable to parameter of type 'never'`. Ветка `default: return 0` такую защиту отключает: ошибку вы не получите, а неверный ответ получите.

**Сужение и замыкания.** Компилятор сохраняет сужение внутри колбэка, только если уверен, что переменная не изменится. В проверенных примерах (`tsc` 7.0.2) для параметра функции и для локальной `let`-переменной, которой после создания замыкания ничего не присваивают, сужение внутри стрелочной функции сохраняется. Если переменную переназначают после создания замыкания или проверяют свойство объекта (`o.v !== undefined`), внутри колбэка тип снова широкий. Надёжный способ: скопировать значение в `const` сразу после проверки.

<!-- section: examples -->
## Примеры

**Пример 1. typeof.**

```typescript run
function pad(value: string | number, width: number): string {
  if (typeof value === "number") {
    return value.toFixed(1).padStart(width);
  }
  return value.padStart(width);
}

console.log(`[${pad(3.14159, 6)}]`);
console.log(`[${pad("ab", 6)}]`);
```
```output
[   3.1]
[    ab]
```

**Пример 2. Литеральные типы вместо перечисления.** `Record<Direction, ...>` обязывает перечислить все направления: пропуск - ошибка компиляции.

```typescript run
type Direction = "up" | "down" | "left" | "right";

const delta: Record<Direction, readonly [number, number]> = {
  up: [0, -1],
  down: [0, 1],
  left: [-1, 0],
  right: [1, 0],
};

function move(pos: readonly [number, number], d: Direction): [number, number] {
  const [dx, dy] = delta[d];
  return [pos[0] + dx, pos[1] + dy];
}

console.log(move([2, 2], "left"));
console.log(move([0, 0], "down"));
```
```output
[ 1, 2 ]
[ 0, 1 ]
```

**Пример 3. in, instanceof и Array.isArray.**

```typescript run
type Fish = { swim(): string };
type Bird = { fly(): string };

function move(a: Fish | Bird): string {
  return "swim" in a ? a.swim() : a.fly();
}

class Dog { bark() { return "гав"; } }
class Cat { meow() { return "мяу"; } }

function sound(a: Dog | Cat): string {
  return a instanceof Dog ? a.bark() : a.meow();
}

function toList(x: string | string[] | undefined): string[] {
  if (x === undefined) return [];
  return Array.isArray(x) ? x : [x];
}

console.log(move({ swim: () => "плывёт" }), move({ fly: () => "летит" }));
console.log(sound(new Dog()), sound(new Cat()));
console.log(toList(undefined), toList("a"), toList(["b", "c"]));
```
```output
плывёт летит
гав мяу
[] [ 'a' ] [ 'b', 'c' ]
```

**Пример 4. Ловушка истинности.** Проверка `if (count)` считает `0` отсутствием значения. Явное сравнение с `undefined` ведёт себя верно.

```typescript run
function labelWrong(count: number | undefined): string {
  return count ? `в наличии: ${count}` : "нет данных";
}

function labelRight(count: number | undefined): string {
  return count !== undefined ? `в наличии: ${count}` : "нет данных";
}

console.log(labelWrong(0), "|", labelRight(0));
console.log(labelWrong(5), "|", labelRight(undefined));
```
```output
нет данных | в наличии: 0
в наличии: 5 | нет данных
```

**Пример 5. Дискриминированное объединение и assertNever.**

```typescript run
type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number };

function assertNever(x: never): never {
  throw new Error(`Необработанный вариант: ${JSON.stringify(x)}`);
}

function area(s: Shape): number {
  switch (s.kind) {
    case "circle":
      return Math.PI * s.r ** 2;
    case "rect":
      return s.w * s.h;
    default:
      return assertNever(s);
  }
}

console.log(area({ kind: "rect", w: 3, h: 4 }));
console.log(area({ kind: "circle", r: 1 }).toFixed(3));
```
```output
12
3.142
```

Добавьте в `Shape` третий вариант, и `assertNever(s)` перестанет компилироваться, пока вы не напишете для него `case`.

<!-- section: mistakes -->
## Типичные ошибки

* **`default: return 0` вместо проверки исчерпываемости.** Код компилируется всегда, а новый вариант молча даёт неверный результат. Пишите `default: return assertNever(s)`.
* **Сужение истинностью для чисел и строк.** `if (count)` и `if (name)` отбрасывают `0` и `""`. Если «нет значения» означает `undefined`/`null`, сравнивайте явно.
* **Забыть, что `typeof null === "object"`.** Проверку на объект дополняют `x !== null`.
* **Дискриминант без литерального типа.** Если поле объявлено как `kind: string`, сужения по нему не будет. Значения должны быть литералами: `kind: "circle"`.
* **Приводить тип вместо сужения.** `(s as Circle).r` работает ровно до тех пор, пока `s` действительно круг; проверка `s.kind === "circle"` делает то же самое безопасно.
* **Рассчитывать на сужение через границы функций и изменения.** Если значение могло измениться, компилятор вернёт широкий тип; сохраняйте проверенное значение в `const`.

<!-- section: summary -->
## Коротко

* `A | B` - значение одного из видов; доступны только общие члены, остальное требует сужения.
* Сужают проверками `typeof`, `===`, `in`, `instanceof`, `Array.isArray` и истинностью (осторожно с `0` и `""`).
* Литеральные типы и объединения литералов заменяют перечисления без дополнительного кода.
* Дискриминированное объединение + `switch` + `assertNever` превращают забытый вариант в ошибку компиляции.
* `default: return 0` отключает эту защиту, поэтому в упражнении её проверяет специальный тест.
