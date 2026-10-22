<!-- section: simple -->
## Зачем нужны массивы объектов

Почти любые реальные данные - это список однотипных записей: ученики с оценками, покупки с ценами, письма с отправителями. В прошлом уроке вы научились описывать одну запись объектом, а в уроке про массивы - хранить много значений подряд. Если соединить оба приёма, получается **массив объектов**: список записей одной формы. Вопросы к нему задают одни и те же: найти запись, выбрать подходящие, посчитать итог, упорядочить.

## Одна новая идея: необязательное свойство

Не у каждой записи есть все данные. У одного товара есть скидка, у другого нет. Если сделать поле `discount` обязательным, придётся писать `discount: 0` везде. В интерфейсе рядом с именем можно поставить знак вопроса.

```text
interface Product {
  name: string;
  price: number;
  discount?: number;
}
```

Запись `discount?: number` значит «свойство может отсутствовать». Тогда его тип - `number | undefined`: число или `undefined` (так вы уже видели в уроке «Необязательные значения»). Прочитать такое свойство и сразу вычислять с ним нельзя: нужно либо проверить `!== undefined`, либо подставить запасное значение через `??`.

<!-- section: examples -->
## Пример 1. Поиск и отбор

```typescript run
interface Student {
  name: string;
  grade: number;
}

const students: Student[] = [
  { name: "Анна", grade: 5 },
  { name: "Борис", grade: 3 },
  { name: "Вера", grade: 4 },
];

const vera = students.find((s) => s.name === "Вера");
if (vera !== undefined) {
  console.log(vera.name, vera.grade);
}

const nobody = students.find((s) => s.name === "Глеб");
console.log(nobody);

const good = students.filter((s) => s.grade >= 4);
console.log(good.length);
console.log(good.map((s) => s.name));
```
```output
Вера 4
undefined
2
[ 'Анна', 'Вера' ]
```

Методы массивов из урока 17 работают с объектами без изменений, только в функции-условии теперь читаются свойства: `s.name === "Вера"`. `find` возвращает **первый** подходящий объект или `undefined`, если такого нет, поэтому тип результата - `Student | undefined`, и перед использованием нужна проверка `!== undefined`. `filter` возвращает новый массив подходящих объектов (возможно, пустой), `map` собирает из них массив названий.

## Пример 2. Необязательное свойство

```typescript run
interface Product {
  name: string;
  price: number;
  discount?: number;
}

const items: Product[] = [
  { name: "хлеб", price: 40 },
  { name: "сыр", price: 300, discount: 50 },
];

console.log(items[0].discount);
console.log(items[1].discount);

for (const item of items) {
  const discount: number = item.discount ?? 0;
  console.log(item.name, item.price - discount);
}
```
```output
undefined
50
хлеб 40
сыр 250
```

У хлеба `discount` не указан: чтение даёт `undefined`, а не ошибку. Выражение `item.discount ?? 0` означает «значение скидки, а если его нет - 0». После него тип `number`, и вычитать можно. Объекты с разным набором необязательных свойств свободно живут в одном массиве, потому что оба подходят под интерфейс.

## Пример 3. Итог по группе

```typescript run
interface Expense {
  title: string;
  category: string;
  amount: number;
}

const expenses: Expense[] = [
  { title: "метро", category: "транспорт", amount: 60 },
  { title: "обед", category: "еда", amount: 250 },
  { title: "такси", category: "транспорт", amount: 400 },
  { title: "кофе", category: "еда", amount: 120 },
];

function totalFor(list: Expense[], category: string): number {
  let total: number = 0;
  for (const e of list) {
    if (e.category === category) {
      total = total + e.amount;
    }
  }
  return total;
}

console.log(totalFor(expenses, "еда"));
console.log(totalFor(expenses, "транспорт"));
console.log(totalFor(expenses, "кино"));
```
```output
370
460
0
```

Типовая схема «итог по группе»: накопитель `total`, цикл по записям, условие на нужное свойство и добавление другого. Для категории без записей цикл ничего не прибавит, и результат - начальный 0. Эта же схема подходит для подсчёта количества, поиска максимума и других итогов.

## Пример 4. Сортировка по свойству

```typescript run
interface Product {
  name: string;
  price: number;
}

const items: Product[] = [
  { name: "сыр", price: 300 },
  { name: "хлеб", price: 40 },
  { name: "чай", price: 120 },
];

const byPrice = [...items].sort((a, b) => a.price - b.price);
console.log(byPrice.map((p) => p.name));
console.log(items.map((p) => p.name));
console.log(items.some((p) => p.price > 200));
console.log(items.some((p) => p.price > 1000));
```
```output
[ 'хлеб', 'чай', 'сыр' ]
[ 'сыр', 'хлеб', 'чай' ]
true
false
```

Компаратор получает два объекта и возвращает число: отрицательное, если первый должен стоять раньше. `a.price - b.price` упорядочивает по возрастанию цены. Сортировке отдаётся **копия** `[...items]`, поэтому исходный порядок сохранился (вторая строка вывода). Метод `some` отвечает «есть ли хотя бы один подходящий элемент» и возвращает `true` или `false`: в массиве есть товар дороже 200, но дороже 1000 нет.

**Попробуйте.** Опишите интерфейс `Book` с полями `title` (строка), `pages` (число) и необязательным `author` (строка). Создайте три книги, у одной не указывайте автора. Выведите названия книг, у которых больше 200 страниц, а для каждой книги напечатайте автора или слово «неизвестен».

<!-- section: precise -->
## Точные правила

* Тип массива объектов: `Product[]` (или `Array<Product>`, но в курсе используется первая запись). Элементы читаются `items[i]`, обходятся `for (const item of items)`.
* `title?: string` в интерфейсе делает свойство необязательным; его тип при чтении - `string | undefined`.
* Чтение необязательного свойства у объекта, где его нет, даёт `undefined`, а не ошибку.
* Объект без необязательного свойства подходит под интерфейс. Лишние свойства, которых в интерфейсе нет, в литерале по-прежнему запрещены (`TS2353`).
* `find` возвращает `T | undefined`: сужайте проверкой `=== undefined`, а запасное значение подставляйте через `??`.
* `filter` возвращает новый массив того же типа (возможно, пустой). `map` возвращает массив значений, которые вернула функция.
* `some(условие)` - есть ли хоть один подходящий элемент; `every(условие)` - подходят ли все (для пустого массива `true`).
* `sort` меняет массив **на месте**; чтобы сохранить исходный, сортируйте копию `[...items]`.
* Компаратор для чисел: `(a, b) => a.price - b.price` по возрастанию, `(a, b) => b.price - a.price` по убыванию. Сортировка стабильная: при равных значениях исходный порядок сохраняется.
* Индекс вне границ массива не даёт ошибки проверки: `items[5]` имеет тип `Product` и равен `undefined` во время работы. Чтение его свойства приводит к `TypeError`.
* Накопитель для итога объявляется перед циклом через `let` с явным типом `number`.
* Если нужны данные, у которых форма отличается, лучше сделать отдельный интерфейс, чем набивать один необязательными полями.

<!-- section: mistakes -->
## Типичные ошибки

Во всех примерах ниже использованы одни и те же первые девять строк, поэтому номера строк в сообщениях начинаются с десятой.

```typescript
interface Product {
  name: string;
  price: number;
  discount?: number;
}
const items: Product[] = [
  { name: "хлеб", price: 40 },
  { name: "сыр", price: 300, discount: 50 },
];
```

**1. Вычисление с необязательным свойством.**

```typescript
const cheese = items[1];
console.log(cheese.discount * 2);
```

```text
main.ts(11,13): error TS18048: 'cheese.discount' is possibly 'undefined'.
```

Проверка помнит, что скидки может не быть, и не даёт умножать `undefined`. Лечится подстановкой: `(cheese.discount ?? 0) * 2`.

**2. Использование результата `find` без проверки.**

```typescript
const found = items.find((p) => p.name === "сыр");
console.log(found.price);
```

```text
main.ts(11,13): error TS18048: 'found' is possibly 'undefined'.
```

Если подходящего объекта нет, `find` вернёт `undefined`. Сначала `if (found !== undefined) { ... }`.

**3. Результат `find` присвоен переменной типа без `undefined`.**

```typescript
const found: Product = items.find((p) => p.name === "сыр");
```

```text
main.ts(10,7): error TS2322: Type 'Product | undefined' is not assignable to type 'Product'.
  Type 'undefined' is not assignable to type 'Product'.
```

Аннотация `: Product` обещает, что объект точно есть. Уберите аннотацию или укажите `Product | undefined`.

**4. Опечатка в свойстве внутри функции-условия.**

```typescript
console.log(items.map((p) => p.titel));
```

```text
main.ts(10,32): error TS2339: Property 'titel' does not exist on type 'Product'.
```

Параметр `p` получил тип `Product` из массива, поэтому опечатку находят и внутри стрелочной функции.

**5. Добавлен объект неполной формы.**

```typescript
items.push({ name: "чай", discount: 5 });
```

```text
main.ts(10,12): error TS2741: Property 'price' is missing in type '{ name: string; discount: number; }' but required in type 'Product'.
```

Необязательна только скидка; цена обязательна.

**6. Неверный тип массива результата.**

```typescript
const names: string[] = items.map((p) => p.price);
```

```text
main.ts(10,7): error TS2322: Type 'number[]' is not assignable to type 'string[]'.
  Type 'number' is not assignable to type 'string'.
```

`map` вернул массив цен, то есть `number[]`, а не названий.

**7. Выход за границы массива.**

```typescript
console.log(items[5].name);
```

```text
TypeError: Cannot read properties of undefined (reading 'name')
```

Проверка типов тут молчит: тип `items[5]` считается `Product`. Ошибка появляется только при запуске. Индекс проверяйте сравнением с `items.length` или пользуйтесь `find`.

**8. Сортировка исходного массива.** `items.sort(...)` изменяет `items`, и порядок, на который опирается другой код, теряется. Сортируйте копию: `[...items].sort(...)`.

<!-- section: summary -->
## Итоги

* Массив объектов (`Product[]`) - список записей одной формы; методы `find`, `filter`, `map`, `some` и обычные циклы работают с ним так же, как с числами, но внутри читаются свойства.
* `find` возвращает `T | undefined`: результат надо проверить, прежде чем читать свойства.
* `discount?: number` делает свойство необязательным; его значение - `number | undefined`, а запасное значение подставляет `??`.
* Итог по группе считается схемой «накопитель + цикл + условие».
* Для сортировки по свойству используется компаратор `a.price - b.price` и копия массива.
* Ошибки в именах свойств и в формах объектов находятся до запуска; выход индекса за границы - нет.

Дальше - что делать, когда данные заведомо плохие: [Ошибки и исключения](lesson:typescript.from-zero.errors-and-exceptions).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `interface` с массивом - это аналог списка DTO (Java `List<Dto>`, C# `List<T>`, Go slice of struct). Отличие: у объектов нет класса и конструктора, а совместимость определяется формой.
* `prop?: T` не равно «значение по умолчанию»: если свойство не задано, чтение даёт `undefined`. С включённой по умолчанию в курсе строгой проверкой его нужно обработать. Обратите внимание, что `{ discount: undefined }` и объект без ключа различаются через `in` и `Object.keys`, хотя при чтении дают одно и то же.
* `find` возвращает `T | undefined` (в Java это `Optional`, в Rust `Option`); сужение делается проверкой, а не обёрткой.
* Индексация `items[i]` по умолчанию имеет тип `T`, а не `T | undefined` (настройка `noUncheckedIndexedAccess` выключена): выход за границы - это ошибка выполнения, как в JavaScript, а не исключение проверки.
* `sort` изменяет массив и без компаратора сортирует по строковому представлению (`[10, 9, 1].sort()` даёт `[1, 10, 9]`); в этом курсе компаратор указывается всегда.
* `toSorted`, `toReversed` и `Object.groupBy` недоступны при цели `es2022`; используйте копию через `[...items]` и обычные циклы.
* Группировка по ключу без `Map` делается через `filter` по каждому значению ключа, как в примере 3; `Map` и `Record` разбираются дальше по курсам.
