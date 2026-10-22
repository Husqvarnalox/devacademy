<!-- section: simple -->
## Зачем этот урок

Массивы, словари и функции вы знаете по другим языкам. В TypeScript их тип выглядит строго, но под ним лежит JavaScript со всеми его особенностями: массивы и объекты сравниваются по ссылке, `sort()` без компаратора сортирует как строки, отсутствующее значение превращается в `undefined`, а тип иногда обещает больше, чем есть на самом деле. Урок показывает, где типы вас защитят, а где нет. Успешное прохождение (упражнение и квиз) засчитывает уроки 14-21 курса «TypeScript с нуля».

## Главная мысль

Типизация в TypeScript **структурная**: совместимость определяется формой значения, а не названием типа. Функции, которой нужен `{ x: number; y: number }`, подойдёт любой объект с такими полями. Отсутствие значения в типе записывают явно: `price?: number` и `number | undefined`. Но проверка работает по собственным правилам, и нужно знать три исключения: индекс массива не добавляет `undefined` к типу, `Record<string, T>` не знает про унаследованные ключи, а данные, пришедшие снаружи, тип только обещает.

<!-- section: examples -->
## Пример 1. Массивы: индекс, `at`, сортировка, ссылки

```typescript run
const scores: number[] = [90, 85];
const third: number = scores[2];
console.log(third, typeof third, scores.at(2), scores.at(-1));

const nums: number[] = [5, 25, 100, 1];
console.log([...nums].sort());
console.log([...nums].sort((a, b) => a - b));
console.log(nums);
const alias = nums;
alias.push(7);
const twin: number[] = [5, 25, 100, 1, 7];
console.log(nums.length, alias === nums, twin === nums);
const nested: number[][] = [[1], [2]];
const shallow = [...nested];
shallow[0].push(99);
const deep = nested.map((row) => [...row]);
deep[1].push(55);
console.log(nested, deep);
```
```output
undefined undefined undefined 85
[ 1, 100, 25, 5 ]
[ 1, 5, 25, 100 ]
[ 5, 25, 100, 1 ]
5 true false
[ [ 1, 99 ], [ 2 ] ] [ [ 1, 99 ], [ 2, 55 ] ]
```

`scores[2]` по типу - `number`, и проверка пропускает запись `const third: number = ...`, хотя в массиве двух элементов третьего нет, и на самом деле там `undefined` (по умолчанию для индекса `noUncheckedIndexedAccess` выключен). Метод `at` честно возвращает `number | undefined`, и компилятор заставит проверить результат. Метод `sort()` без компаратора сравнивает элементы как строки: `"100" < "25" < "5"`. Копия `[...nums]` поверхностная: во вложенных массивах внутренние ссылки общие, и `shallow[0].push(99)` изменил `nested`. Равенство `===` у массивов сравнивает ссылки: `twin` с теми же числами не равен `nums`, а `alias` - тот же объект.

## Пример 2. Форма, необязательные свойства, словари

```typescript run
interface Point {
  x: number;
  y: number;
}
function norm(p: Point): number {
  return Math.hypot(p.x, p.y);
}
const labeled = { x: 3, y: 4, label: "A" };
console.log(norm(labeled));

interface Item {
  name: string;
  price?: number;
}
const items: Item[] = [{ name: "чай" }, { name: "кофе", price: 200 }];
for (const it of items) {
  console.log(it.name, it.price ?? "нет цены", it.price?.toFixed(1));
}

const counts: Record<string, number> = {};
const word = "constructor";
counts[word] = (counts[word] ?? 0) + 1;
console.log(counts[word]);
const map = new Map<string, number>();
map.set(word, (map.get(word) ?? 0) + 1);
console.log(map.get(word));

console.log(["1", "2", "3"].map(parseInt));
console.log(["1", "2", "3"].map((s) => parseInt(s, 10)));
```
```output
5
чай нет цены undefined
кофе 200 200.0
function Object() { [native code] }1
1
[ 1, NaN, NaN ]
[ 1, 2, 3 ]
```

`labeled` имеет лишнее поле `label`, но функция `norm` принимает его: важны наличие `x` и `y`, а не название типа. Лишнее поле отвергается только тогда, когда объектный **литерал** записан прямо в месте передачи (см. «Ошибки»). `price?: number` означает «свойство может отсутствовать», и обращение к нему даёт `number | undefined`; оператор `?.` пропускает вызов, а `??` подставляет значение для `undefined`. `Record<string, number>` обещает числа для любого ключа, но `counts["constructor"]` - унаследованная функция, и запись `?? 0` ничего не исправляет: в итоге получилась склеенная строка. Для словарей с чужими ключами берите `Map`. Последние строки: `map(parseInt)` компилируется, потому что колбэк `(string, number) => number` подходит по типам, но второй аргумент `parseInt` - основание системы счисления, а `map` передаёт туда индекс.

## Пример 3. Функции: тело стрелки и `sort` на месте

```typescript run
function make() {
  return () => { a: 1 };
}
const f = make();
console.log(f());
const g = () => ({ a: 1 });
console.log(g());
const letters = ["b", "a", "c"];
const result = letters.sort();
console.log(result === letters, letters);
```
```output
undefined
{ a: 1 }
true [ 'a', 'b', 'c' ]
```

Фигурные скобки после `=>` открывают тело функции, а `a: 1` внутри - метка с выражением, поэтому функция возвращает `undefined`, и тип результата выводится как `void`. Чтобы вернуть объект, литерал берут в круглые скобки. Метод `sort` изменяет массив на месте и возвращает тот же массив, поэтому `result === letters`.

Попробуйте: объявите `g` с аннотацией `(): { a: number } => ...` и посмотрите, что скажет проверка про вариант без скобок.

<!-- section: precise -->
## Точные правила

* Тип массива пишут `T[]` или `Array<T>`; неизменяемый - `readonly T[]` (нет `push`, `sort`, `splice` и других мутаций). Обычный массив подходит там, где ждут `readonly`, обратного преобразования нет.
* `arr[i]` имеет тип `T`, даже если `i` вне границ. Безопасное чтение - `at`, `find` (оба возвращают `T | undefined`) или явная проверка длины. Чтение несуществующего индекса не бросает исключение.
* Методы, меняющие массив: `push pop shift unshift splice sort reverse fill`. Возвращают новый: `map filter slice concat flat flatMap`. В `es2022` нет `toSorted`, `toReversed`, `toSpliced`, `with`.
* `sort` стабилен; без компаратора сравнивает строки (по кодовым единицам UTF-16). Для чисел - `(a, b) => a - b`.
* `===` у массивов и объектов сравнивает ссылки. Компилятор отвергает такое сравнение, когда один из операндов - свежий литерал (`TS2839`).
* Структурная совместимость: значение подходит, если у него есть все требуемые свойства нужных типов. Проверка избыточных свойств (`TS2353`) применяется только к литералам, записанным прямо в присваивании или аргументе.
* `readonly` в типе запрещает присваивание свойства (`TS2540`), но не замораживает объект при запуске: это ограничение компилятора. `Object.freeze` замораживает по-настоящему, но поверхностно.
* `p?: T` равно `p: T | undefined` с возможным отсутствием свойства. Значение по умолчанию подставляет `??` (только для `null`/`undefined`).
* `Map<K, V>` сравнивает ключи по SameValueZero, хранит порядок вставки и не наследует ключей. Обычный объект наследует `constructor`, `toString`, `__proto__`.
* Функциональные типы `(x: number) => string` тоже структурные: функция с меньшим числом параметров подходит там, где ждут больше (поэтому `map(parseInt)` проходит проверку).
* Стрелка не имеет собственных `this` и `arguments`; `function` определяет `this` в момент вызова. Метод, вынутый из объекта, теряет `this`.

<!-- section: mistakes -->
## Ошибки и сообщения

**1. Не хватает обязательного свойства.**

```typescript
interface Point { x: number; y: number }
const p: Point = { x: 1 };
```

```text
main.ts(2,7): error TS2741: Property 'y' is missing in type '{ x: number; }' but required in type 'Point'.
```

**2. Лишнее свойство в литерале.**

```typescript
function label(user: { name: string }): string {
  return user.name;
}
console.log(label({ name: "Аня", age: 30 }));
```

```text
main.ts(4,34): error TS2353: Object literal may only specify known properties, and 'age' does not exist in type '{ name: string; }'.
```

Та же запись через промежуточную переменную `const u = { name: "Аня", age: 30 }; label(u)` проходит: проверка избыточных свойств работает только для литерала на месте.

**3. Необязательное свойство без проверки.**

```typescript
interface Item { name: string; price?: number }
const it: Item = { name: "чай" };
const total: number = it.price + 1;
```

```text
main.ts(3,23): error TS18048: 'it.price' is possibly 'undefined'.
```

Исправление: `(it.price ?? 0) + 1` либо `if (it.price !== undefined) { ... }`.

**4. Результат `find` без проверки.**

```typescript
const xs = [1, 2, 3];
const m = xs.find((x) => x > 5);
const doubled: number = m * 2;
```

```text
main.ts(3,25): error TS18048: 'm' is possibly 'undefined'.
```

**5. Запись в `readonly`.**

```typescript
interface Item { readonly name: string }
const it: Item = { name: "чай" };
it.name = "кофе";
```

```text
main.ts(3,4): error TS2540: Cannot assign to 'name' because it is a read-only property.
```

**6. Мутирующий метод у `readonly`-массива.**

```typescript
const nums: readonly number[] = [3, 1, 2];
nums.sort();
```

```text
main.ts(2,6): error TS2339: Property 'sort' does not exist on type 'readonly number[]'.
```

Отсортированная копия: `[...nums].sort((a, b) => a - b)`.

**7. Передача `readonly`-массива туда, где ждут изменяемый.**

```typescript
function sum(xs: number[]): number { return xs.reduce((a, b) => a + b, 0); }
const frozen: readonly number[] = [1, 2];
console.log(sum(frozen));
```

```text
main.ts(3,17): error TS4104: The type 'readonly number[]' is 'readonly' and cannot be assigned to the mutable type 'number[]'.
```

Если функция не меняет массив, объявите параметр как `readonly number[]`: так она принимает оба вида.

**8. Сравнение свежего массива.**

```typescript
console.log([1, 2] === [1, 2]);
```

```text
main.ts(1,13): error TS2839: This condition will always return 'false' since JavaScript compares objects by reference, not value.
```

Если же сравнивать две переменные, ошибки не будет, а результат останется `false`.

**9. Что проверка не видит.** `scores[2]` без `at`, `sort()` без компаратора, `() => { a: 1 }` (возвращает `undefined`), `counts["constructor"]` в `Record`, общие вложенные ссылки после `[...nested]` - всё это компилируется.

<!-- section: summary -->
Теперь вы знаете, как работают структурная совместимость и необязательные свойства, почему `arr[i]` не включает `undefined`, чем `readonly` отличается от `Object.freeze`, как не получить сюрпризов от `sort()` и когда вместо объекта нужна `Map`. Следующий шаг - исключения и чтение ввода: [Исключения и ввод: что тип не проверяет](lesson:typescript.programmers.errors-and-input). Для подробностей: [Массивы](lesson:typescript.from-zero.arrays), [Обход массивов](lesson:typescript.from-zero.iterating-arrays), [Стрелочные функции](lesson:typescript.from-zero.arrow-functions), [Методы массивов](lesson:typescript.from-zero.array-methods), [Необязательные значения](lesson:typescript.from-zero.optional-values), [Методы строк](lesson:typescript.from-zero.string-methods), [Объекты](lesson:typescript.from-zero.objects), [Массивы объектов](lesson:typescript.from-zero.arrays-of-objects).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Массив и объект - ссылки. `===` сравнивает ссылки, структурного равенства нет. Копии поверхностные (`[...a]`, `{ ...o }`); для глубокой копии данных - `structuredClone` (нужны типы Node.js или DOM).
* `arr[i]` типизируется как `T` без `undefined`; `at(i)`, `find` и `Map.get` дают `T | undefined`, и `strict` заставляет проверить. Выход за границы не бросает исключение.
* `readonly T[]` и `readonly` у свойств - ограничения компилятора. Объект при запуске не защищён.
* Структурная типизация, как в Go-интерфейсах: имя типа не важно, важна форма. Исключение - проверка лишних свойств у литералов.
* `sort()` без компаратора сортирует как строки; `sort` и `reverse` меняют массив на месте.
* `map`, `filter`, `reduce`, `find`, `some`, `every`, `flatMap` - как в других языках; всегда давайте `reduce` начальное значение.
* Для словаря с произвольными ключами - `Map`; `Record<string, T>` и `{ [key: string]: T }` обещают значение для любого ключа, включая унаследованные.
* Тип не заменяет проверку: `arr.map(parseInt)` и `() => { a: 1 }` компилируются.

<!-- section: deep-dive -->
## Углубление: noUncheckedIndexedAccess

Параметр `noUncheckedIndexedAccess` в настройках `tsc` добавляет `| undefined` к чтению по индексу и по строковому ключу, и тогда `scores[2]` честно имеет тип `number | undefined`. Он не входит в `strict`, и в приложении выключен, а в больших проектах его часто включают именно из-за ловушки из первого примера.
