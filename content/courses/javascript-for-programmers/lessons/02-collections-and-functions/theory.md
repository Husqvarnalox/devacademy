<!-- section: simple -->
## Зачем этот урок

Массивы, объекты и функции в JavaScript выглядят знакомо, но у каждого есть своя особенность: массивы и объекты сравниваются по ссылке, `sort()` без аргументов сортирует как строки, обычный объект - плохая «карта» для произвольных ключей, а стрелочная функция и `function` ведут себя по-разному. Успешное прохождение (упражнение и квиз) засчитывает уроки 13-19 и 22 курса «JavaScript с нуля».

## Главная мысль

Массивы, объекты и функции - **объекты**, и переменные хранят ссылки на них. Присваивание копирует ссылку, `===` сравнивает ссылки, а встроенные методы делятся на мутирующие (`push`, `sort`, `reverse`, `splice`) и возвращающие новое значение (`map`, `filter`, `slice`, `toSorted`). Если держать это различие в голове, большая часть странностей исчезает.

<!-- section: examples -->
## Пример 1. Ссылки, копии и равенство

```javascript run
const a = [1, 2], b = [1, 2];
console.log(a === b, a == b, JSON.stringify(a) === JSON.stringify(b));
const alias = a;
alias.push(3);
console.log(a, a === alias);
const copy = [...a];
copy.push(4);
console.log(a, copy);
const nested = [[1], [2]];
const shallow = [...nested];
shallow[0].push(9);
console.log(nested);
const deep = structuredClone(nested);
deep[0].push(7);
console.log(nested, deep);
```
```output
false false true
[ 1, 2, 3 ] true
[ 1, 2, 3 ] [ 1, 2, 3, 4 ]
[ [ 1, 9 ], [ 2 ] ]
[ [ 1, 9 ], [ 2 ] ] [ [ 1, 9, 7 ], [ 2 ] ]
```

`===` для массивов и объектов проверяет, что это **один и тот же** объект, а не равное содержимое. Копирование через `[...a]`, `Object.assign` или `{ ...o }` **поверхностное**: вложенные объекты остаются общими. Для полного копирования данных есть `structuredClone`. Параметры функции получают копию ссылки: функция может менять содержимое переданного массива, но не может заменить переменную вызывающего.

## Пример 2. sort, parseInt и методы массива

```javascript run
console.log([10, 9, 1, 100].sort());
console.log([10, 9, 1, 100].sort((x, y) => x - y));
console.log(["b", "a", "C"].sort(), ["b", "a", "C"].sort((x, y) => x.localeCompare(y)));
const orig = [3, 1, 2];
const sorted = orig.toSorted((x, y) => x - y);
console.log(orig, sorted);
console.log(orig.reverse() === orig, orig);
console.log(["1", "2", "3"].map(parseInt), ["1", "2", "3"].map(Number));
console.log([].reduce((s, x) => s + x, 0));
try { [].reduce((s, x) => s + x); } catch (e) { console.log(e.name); }
console.log([NaN].indexOf(NaN), [NaN].includes(NaN), [1, 2, 3].at(-1), new Array(3));
console.log(Array.from({ length: 3 }, (_, i) => i * i), [1, 2].flatMap((x) => [x, x * 10]));
```
```output
[ 1, 10, 100, 9 ]
[ 1, 9, 10, 100 ]
[ 'C', 'a', 'b' ] [ 'a', 'b', 'C' ]
[ 3, 1, 2 ] [ 1, 2, 3 ]
true [ 2, 1, 3 ]
[ 1, NaN, NaN ] [ 1, 2, 3 ]
0
TypeError
-1 true 3 [ <3 empty items> ]
[ 0, 1, 4 ] [ 1, 10, 2, 20 ]
```

Без компаратора `sort` приводит элементы к строкам и сравнивает по кодам: `[10, 9, 1, 100]` превращается в `[1, 10, 100, 9]`. Компаратор возвращает отрицательное, нулевое или положительное число; `x - y` годится для обычных чисел. `sort` и `reverse` **меняют массив на месте**, `toSorted` и `toReversed` (Node 20+) возвращают копию. `map(parseInt)` передаёт индекс вторым аргументом (основанием системы счисления), поэтому получаются `NaN`. `reduce` без начального значения на пустом массиве бросает `TypeError`. `indexOf` использует `===` и не находит `NaN`, `includes` находит.

## Пример 3. function, стрелка и this

```javascript run
const user = {
  name: "Ann",
  hi() { return "hi " + this?.name; },
  later() { return [1].map(() => this.name); },
  arrow: () => "стрелка не получает this объекта",
};
console.log(user.hi(), user.later());
const detached = user.hi;
console.log(detached());
console.log(declared(2));
function declared(x) { return x * 2; }
try { arrowFn(2); } catch (e) { console.log(e.name); }
const arrowFn = (x) => x * 2;
const make = () => ({ ok: true });
const broken = () => { ok: true };
console.log(make(), broken());
try { new arrowFn(); } catch (e) { console.log(e.name); }
```
```output
hi Ann [ 'Ann' ]
hi undefined
4
ReferenceError
{ ok: true } undefined
TypeError
```

`function` получает собственный `this`, который зависит от **способа вызова**: `user.hi()` даёт `user`, а `detached()` - уже не `user`. Стрелочная функция собственного `this` не имеет и берёт его из окружающего кода, поэтому внутри метода она удобна для колбэков. Отличия стрелки: нет `arguments`, нельзя вызвать через `new`, нет собственного `prototype`. Функции-объявления поднимаются целиком, а стрелка в `const` - нет (TDZ). Тело из `{ ... }` без `return` возвращает `undefined`, а для возврата литерала объекта его берут в скобки: `() => ({ ok: true })`.

## Пример 4. Объект или Map

```javascript run
const o = {};
console.log(o["constructor"] === Object, "toString" in o);
const keys = {};
keys[1] = "a";
keys["1"] = "b";
keys[{}] = "c";
console.log(Object.keys(keys));
console.log(Object.keys({ b: 1, 2: 1, a: 1, 1: 1 }));
const m = new Map();
m.set("constructor", 1).set(1, "число").set("1", "строка");
console.log(m.size, m.get(1), m.get("1"), m.has("toString"));
const bare = Object.create(null);
bare.x = 1;
console.log(Object.keys(bare), "toString" in bare);
console.log(JSON.stringify(new Map([[1, 2]])), JSON.stringify({ d: undefined, n: NaN, u: [undefined] }));
const { name, ...rest } = { name: "A", age: 3, role: "x" };
console.log(name, rest);
```
```output
true true
[ '1', '[object Object]' ]
[ '1', '2', 'b', 'a' ]
3 число строка false
[ 'x' ] false
{} {"n":null,"u":[null]}
A { age: 3, role: 'x' }
```

Ключи обычного объекта - только строки и символы (число превращается в строку), целочисленные ключи идут первыми по возрастанию, а затем строки в порядке добавления. У каждого объекта есть унаследованные свойства (`constructor`, `toString`, `__proto__`), и если ключ приходит из данных пользователя, они мешают. Для динамических ключей, ключей любого типа и подсчётов используйте `Map` (порядок вставки, `size`, быстрые добавление и удаление) либо `Object.create(null)`. Объект подходит для записей с фиксированными полями и для JSON; `Map` в JSON сериализуется в `{}`.

<!-- section: precise -->
* Массивы - объекты с числовыми ключами. `typeof [] === "object"`, проверка - `Array.isArray`. Запись `arr.length = 0` очищает массив; выход за границу читается как `undefined` и не бросает ошибку; присваивание за границей создаёт «дыры».
* Мутирующие методы: `push pop shift unshift splice sort reverse fill copyWithin`. Возвращающие новое: `map filter slice concat flat flatMap toSorted toReversed toSpliced with`. `forEach` ничего не возвращает; `await` внутри `forEach` не ждёт.
* `sort` без компаратора сортирует по строковому представлению (по кодовым единицам UTF-16); сортировка стабильная (с ES2019). Для текста с учётом языка используйте `localeCompare`.
* Копирование поверхностное: `[...a]`, `a.slice()`, `{ ...o }`, `Object.assign`. Глубокая копия данных - `structuredClone` (не копирует функции и методы).
* Аргументы передаются «по значению ссылки»; `Object.freeze` замораживает только один уровень.
* Деструктуризация: `const [x, , z] = arr`, `const { a, b = 5, ...rest } = obj`; значение по умолчанию подставляется на `undefined`, но не на `null`.
* `function` - собственные `this` и `arguments`, можно вызывать через `new`; стрелка - лексический `this`, без `arguments` и `new`. Остаточные параметры: `(...args) =>`.
* У функции есть `length` (число параметров до первого значения по умолчанию) и `name`; функции - значения первого класса и замыкания захватывают **переменные**, а не их значения.
* `Map` и `Set` сравнивают ключи по SameValueZero (`NaN` равен `NaN`, объекты по ссылке); итерация идёт в порядке вставки.
* `Object.keys/values/entries` возвращают только собственные перечислимые строковые ключи; `for...in` включает унаследованные.

<!-- section: mistakes -->
**1. `sort()` для чисел.** `[10, 9, 1].sort()` даёт `[1, 10, 9]`. Всегда передавайте компаратор: `(x, y) => x - y`.

**2. Неожиданная мутация.** `const sorted = arr.sort(...)` сортирует и сам `arr`, а `sorted === arr`. Копируйте заранее (`[...arr].sort(...)`) или берите `toSorted`.

**3. Сравнение массивов и объектов через `===` или `==`.** Равны только ссылки на один объект. Сравнивайте поэлементно или через специализированную функцию; `JSON.stringify` не различает `undefined`, `NaN` и порядок ключей.

**4. `arr.map(parseInt)`.** Индекс элемента попадает в основание системы счисления. Пишите `arr.map((s) => parseInt(s, 10))` или `arr.map(Number)`.

**5. Поверхностная копия вместо глубокой.** После `const b = [...a]` вложенные массивы общие: изменение в `b[0]` видно в `a[0]`.

**6. Обычный объект как словарь для пользовательских ключей.** Ключи `"constructor"`, `"__proto__"`, `"toString"` дают неверные результаты: `counts[word] = (counts[word] || 0) + 1` для слова `constructor` склеит строку из функции. Используйте `Map`.

**7. Стрелка вместо метода и тело в фигурных скобках.** `() => { ok: true }` возвращает `undefined`, а метод-стрелка в литерале объекта не видит `this` объекта.

**8. `reduce` без начального значения.** На пустом массиве - `TypeError`, а на массиве объектов первый элемент становится аккумулятором. Всегда задавайте второй аргумент.

<!-- section: summary -->
Теперь вы знаете, как в JavaScript работают ссылки на массивы и объекты, почему `sort()` нужен компаратор, какие методы мутируют массив, чем стрелочная функция отличается от `function` и когда вместо объекта нужна `Map`. Следующий шаг - исключения и ввод в Node.js. Для подробностей: [Массивы](lesson:javascript.from-zero.arrays), [Обход массивов](lesson:javascript.from-zero.iterating-arrays), [Стрелочные функции](lesson:javascript.from-zero.arrow-functions), [Методы массивов](lesson:javascript.from-zero.array-methods), [Методы строк](lesson:javascript.from-zero.string-methods), [Объекты](lesson:javascript.from-zero.objects), [Массивы объектов](lesson:javascript.from-zero.arrays-of-objects), [Область видимости](lesson:javascript.from-zero.scope).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Массив и объект - ссылочные типы. `===` сравнивает ссылки; структурного равенства нет. Копии поверхностные (`[...a]`, `{ ...o }`); глубокая - `structuredClone`.
* `sort()` без компаратора сортирует как строки. Компаратор: `(a, b) => a - b`. `sort` и `reverse` мутируют; `toSorted` и `toReversed` нет. Сортировка стабильная.
* `map`, `filter`, `reduce`, `find`, `some`, `every`, `flatMap` работают как в других языках; `reduce` всегда с начальным значением; `forEach` нельзя прервать; `arr.map(parseInt)` - классическая ловушка.
* Ключи объекта - строки или символы; обычный объект наследует `toString` и другое. Для словарей с чужими ключами используйте `Map` (ключ любого типа, порядок вставки, `size`).
* `function` имеет динамический `this` (зависит от вызова), стрелка - лексический. Метод, вынутый из объекта, теряет `this`. Стрелка не годится в роли конструктора или метода объекта.
* Функции - значения первого класса, замыкания захватывают переменные. Объявления `function` поднимаются, стрелки в `const` - нет.
* Деструктуризация и spread - обычный способ копировать и объединять: `const next = { ...state, count: 1 }`.
* Отсутствующий индекс или свойство - `undefined`, а не исключение; `arr.at(-1)` читает с конца.
