<!-- section: simple -->
## Как назвать форму данных

Форму объекта в TypeScript можно описать двумя способами: **`interface`** и **псевдонимом `type`**. Для обычных объектов они почти взаимозаменяемы, и выбор часто вопрос договорённости в команде. Различия появляются на краях: `type` умеет называть любые типы (объединения, кортежи, литералы), `interface` умеет «дописываться» в другом месте программы (слияние объявлений) и расширяться через `extends`.

Для отдельных свойств есть два модификатора. **`?`** делает свойство необязательным (его тип - `T | undefined`), **`readonly`** запрещает присваивание свойству после создания объекта. Для словарей с заранее неизвестными ключами используют **сигнатуру индекса** `{ [key: string]: number }` или `Record<string, number>`.

Наконец, оператор **`satisfies`** проверяет, что значение подходит под тип, но не заменяет им тип значения. Аннотация `const x: Record<string, string> = {...}` забывает имена ключей, а `{...} satisfies Record<string, string>` ловит ошибки и при этом помнит, какие ключи есть.

```typescript
interface User { readonly id: number; name: string; email?: string }

const routes = { home: "/", user: "/users/:id" } satisfies Record<string, string>;
type RouteName = keyof typeof routes;   // "home" | "user"
```

<!-- section: precise -->
## interface и type, readonly, индексы и satisfies

**interface против type.** Для объектов оба варианта описывают одно и то же: структура определяет совместимость, а не название. Отличия, проверенные в `tsc` 7.0.2:

* два `interface` с одним именем **сливаются** в один тип, а два одноимённых `type` - ошибка TS2300 (`Duplicate identifier`). Слияние нужно, чтобы дополнять чужие типы (например, расширять глобальные объявления), а в прикладном коде чаще вредит: опечатка в имени молча создаёт новый тип;
* `type` может назвать объединение, кортеж, примитив и результат вычислений над типами (`type Id = string | number`), а `interface` описывает только форму объекта (или функции);
* `interface B extends A` расширяет тип и сразу проверяет, что новые свойства совместимы со старыми; пересечение `A & B` просто объединяет требования, и конфликтующее свойство превратится в `never` без ошибки в объявлении.

Практическое правило: объекты описывайте `interface`, остальное - `type`, и придерживайтесь одного стиля в проекте.

**`readonly`** запрещает присваивание (TS2540: `Cannot assign to 'n' because it is a read-only property`) и **не глубокий**: поле `readonly arr: number[]` нельзя заменить, но в массив можно добавлять элементы. Для массива есть `readonly number[]` (у него нет `push`, TS2339), для объекта - `Readonly<T>`. Важная особенность: `readonly` не влияет на совместимость, поэтому объект с `readonly`-полем можно присвоить переменной с изменяемым типом и изменить через неё. Это ограничение, а не защита; во время работы `readonly` не существует.

**Необязательные свойства.** `theme?: Theme` означает, что свойство можно не указывать, а при чтении оно `Theme | undefined`. Флаг `exactOptionalPropertyTypes` (в `strict` он не входит) разводит «свойства нет» и «свойство равно `undefined`»: тогда `{ a: undefined }` для `a?: number` - ошибка TS2375. Значение по умолчанию удобно задавать оператором `??`.

**Сигнатура индекса.** `Record<string, number>` обещает число для любого ключа, но на деле ключа может не быть. Флаг `noUncheckedIndexedAccess` (тоже вне `strict`, его включают отдельно) честнее: `idx["a"]` получает тип `number | undefined`, и присвоить его в `number` нельзя (TS2322). Он же защищает `arr[i]`. Словарь из обычного объекта к тому же знает «служебные» имена: `counts["constructor"]` найдёт `Object.prototype.constructor`, поэтому для произвольных ключей надёжнее `Map`.

**`satisfies`.** Аннотация присваивает значению *другой* тип, а `satisfies` лишь проверяет совместимость и оставляет выведенный тип. В `const ROUTES = {...} satisfies Record<string, string>` ошибочное значение (`user: 5`) даст TS2322, лишний ключ при проверке под конкретную форму - TS2353, а `keyof typeof ROUTES` останется `"home" | "user"`. С аннотацией `Record<string, string>` то же выражение дало бы `keyof` равный `string`, и опечатка в имени маршрута перестала бы ловиться.

<!-- section: examples -->
## Примеры

**Пример 1. Расширение интерфейса и пересечение.**

```typescript run
interface Named { name: string }
interface Employee extends Named { salary: number }

type Tagged = Named & { tags: string[] };

const e: Employee = { name: "Ада", salary: 100 };
const t: Tagged = { name: "Билл", tags: ["x"] };

console.log(e.name, e.salary, "|", t.name, t.tags);
```
```output
Ада 100 | Билл [ 'x' ]
```

**Пример 2. Слияние интерфейсов.** Второе объявление дописывает поле в первое.

```typescript run
interface Config { host: string }
interface Config { port: number }

const c: Config = { host: "localhost", port: 8080 };
console.log(`${c.host}:${c.port}`);
```
```output
localhost:8080
```

**Пример 3. readonly не глубокий.**

```typescript run
interface Box {
  readonly id: number;
  readonly items: string[];
}

const b: Box = { id: 1, items: [] };
// b.id = 2;            // TS2540: Cannot assign to 'id' because it is a read-only property
// b.items = [];        // TS2540
b.items.push("можно");  // сам массив изменяем: readonly поверхностный

const frozen: readonly string[] = ["a", "b"];
// frozen.push("c");    // TS2339: Property 'push' does not exist on type 'readonly string[]'
console.log(b.items, frozen.length);
```
```output
[ 'можно' ] 2
```

**Пример 4. Необязательное поле и noUncheckedIndexedAccess.** Образец проверяется в строгом режиме приложения; ошибки с флагом `noUncheckedIndexedAccess` показаны в комментариях.

```typescript run
interface Options { retries?: number }

function retriesOf(o: Options): number {
  return o.retries ?? 3;
}

const stock: Record<string, number> = { apple: 5 };
// const n: number = stock["pear"];   // с noUncheckedIndexedAccess: TS2322, тип number | undefined
const pear = stock["pear"] ?? 0;

console.log(retriesOf({}), retriesOf({ retries: 0 }), pear);
```
```output
3 0 0
```

**Пример 5. satisfies против аннотации.** С аннотацией имена ключей теряются, с `satisfies` остаются.

```typescript run
const annotated: Record<string, string> = { home: "/", user: "/users/:id" };
const checked = { home: "/", user: "/users/:id" } satisfies Record<string, string>;

type Annotated = keyof typeof annotated;   // string
type Checked = keyof typeof checked;       // "home" | "user"

function go(name: Checked): string {
  return checked[name];
}

const any1: Annotated = "опечатка";        // компилируется: проверка потеряна
// const any2: Checked = "опечатка";       // TS2322
console.log(go("user"), any1);
```
```output
/users/:id опечатка
```

**Пример 6. Словарь на Map и служебные ключи.**

```typescript run
function countWords(words: readonly string[]): Record<string, number> {
  const counts = new Map<string, number>();
  for (const w of words) counts.set(w, (counts.get(w) ?? 0) + 1);
  return Object.fromEntries(counts);
}

const plain: Record<string, number> = {};
// плохой способ: plain["constructor"] ?? 0 найдёт функцию Object, а не число
console.log(typeof plain["constructor"]);
console.log(countWords(["to", "be", "or", "not", "to", "be"]));
```
```output
function
{ to: 2, be: 2, or: 1, not: 1 }
```

<!-- section: mistakes -->
## Типичные ошибки

* **Аннотация вместо `satisfies` для таблиц и конфигураций.** `const X: Record<string, T>` стирает ключи; `keyof typeof X` превращается в `string`.
* **Полагаться на `readonly` как на защиту.** Он поверхностный и существует только при компиляции. Для настоящей неизменяемости копируйте значения.
* **Путать `?` и `| undefined`.** `a?: T` разрешает пропустить поле; `a: T | undefined` требует указать его явно.
* **Читать `arr[i]` и `dict[key]` как гарантированные значения.** Включите `noUncheckedIndexedAccess` и обрабатывайте `undefined` (`??`, `if`).
* **Случайное слияние интерфейсов.** Два `interface` с одним именем в одной области видимости объединяются без предупреждения.
* **Строить словарь из `{}` для произвольных ключей.** Слова вроде `constructor` и `toString` конфликтуют с прототипом; используйте `Map`.

<!-- section: summary -->
## Коротко

* Объекты описывайте `interface`, объединения и вычисляемые типы - `type`; интерфейсы с одинаковым именем сливаются, одноимённые `type` - ошибка.
* `readonly` запрещает присваивание поля, но поверхностный и не влияет на совместимость; `?` делает поле необязательным.
* Сигнатуры индекса честнее с `noUncheckedIndexedAccess`: чтение даёт `T | undefined`.
* `satisfies` проверяет значение и сохраняет его точный тип, а аннотация этот тип заменяет.
