<!-- section: simple -->
## Типы, которые строятся из других типов

Часто новый тип - это вариация старого: «то же самое, но все поля необязательные», «только эти два поля», «тип, который вернёт функция». Описывать такие типы вручную значит дублировать код и забывать обновлять копии. TypeScript умеет **вычислять** типы из других типов.

Базовый набор - **утилитарные типы** из стандартной библиотеки: `Partial<T>` (все поля необязательные), `Required<T>`, `Readonly<T>`, `Pick<T, K>` (выбрать поля), `Omit<T, K>` (убрать поля), `Record<K, V>` (словарь), `ReturnType<F>` (что возвращает функция), `Awaited<T>` (во что превращается промис после `await`). Они не создают кода: это только подсказки компилятору.

Эти типы написаны на двух механизмах, которыми можно пользоваться самим: **отображаемые типы** (mapped types) проходят по ключам и строят новый объект, **условные типы** (`T extends U ? X : Y`) выбирают один из вариантов в зависимости от типа.

```typescript
interface User { id: number; name: string; email?: string }

type UserPatch = Partial<User>;                  // все поля необязательные
type PublicUser = Omit<User, "id">;              // { name: string; email?: string }
type Roles = Record<"admin" | "guest", boolean>; // обязательно оба ключа
```

<!-- section: precise -->
## Утилитарные, отображаемые и условные типы

**Утилитарные типы.** Проверено в `tsc` 7.0.2:

* `Partial<T>` и `Required<T>` добавляют или убирают `?`; `Readonly<T>` добавляет `readonly`. Все три работают **поверхностно**: во вложенном объекте поля остаются прежними (`Partial<U>` с вложенным `addr` всё равно требует `city` внутри `addr`, а `ro.a.b = 2` у `Readonly<{ a: { b: number } }>` допустимо);
* `Pick<T, K>` требует, чтобы `K` был ключом `T`: `Pick<U, "phone">` - ошибка TS2344. `Omit<T, K>` такого требования **не предъявляет**: `Omit<U, "phone">` скомпилируется, и опечатка в имени поля пройдёт незамеченной;
* `Record<K, V>` с объединением литералов в `K` требует все ключи (`{ a: 1 }` для `Record<"a" | "b", number>` - ошибка TS2741);
* `ReturnType<F>` берёт тип результата функции, `Awaited<T>` разворачивает промис, в том числе вложенный: `Awaited<Promise<Promise<boolean>>>` равен `boolean`. Для `async`-функции `Awaited<ReturnType<typeof f>>` - тип значения, которое она вернёт после `await`.

**Отображаемые типы.** Запись `{ [K in keyof T]: T[K] }` повторяет форму `T`. Модификаторы добавляют и убирают свойства: `readonly`, `-readonly`, `?`, `-?`. Так записан `Required<T>`: `{ [K in keyof T]-?: T[K] }`. Ключи можно переименовать и отфильтровать через `as`: `{ [K in keyof T as T[K] extends string ? K : never]: T[K] }` оставит только строковые поля (ключ `never` удаляется), а шаблонные литералы собирают новые имена: `` `get${Capitalize<string & K>}` ``.

**Условные типы.** Запись `T extends U ? X : Y` проверяет совместимость и выбирает ветку. Внутри проверки слово `infer` объявляет «захваченный» тип: `T extends readonly (infer E)[] ? E : never` достаёт тип элемента. Важное свойство - **распределение по объединениям**: если `T` - голый параметр типа, то условный тип применяется к каждому члену объединения отдельно, и результаты объединяются. Для `type Dist<T> = T extends string ? "s" : "o"` значение `Dist<string | number>` равно `"s" | "o"`, а `Dist<never>` - `never` (пустое объединение). Распределение отключают, заключив обе части в кортеж: `[T] extends [string]` даёт `"o"` для `string | number` и `"s"` для `never`.

**Как сравнивать типы.** Утверждения о типах проверяют вспомогательным типом `Equal<A, B>`, который сравнивает точное тождество. Именно так устроены скрытые проверки упражнения; в образцах ниже этот помощник записан в первой строке.

**Когда и сколько.** Вычисления над типами мощны, но читаются хуже обычного кода. Если выражение нужно один раз, проще написать тип руками; выносить стоит повторяющиеся преобразования. Сообщения об ошибках для сложных вычисляемых типов длинные, поэтому сложные типы нужно проверять примерами, как обычный код.

<!-- section: examples -->
## Примеры

**Пример 1. Утилитарные типы в деле.** Тип патча выводится из модели, поэтому при добавлении поля в `User` обновлять второй тип не нужно.

```typescript run
interface User { id: number; name: string; email?: string }

type UserPatch = Partial<Omit<User, "id">>;

function applyPatch(user: User, patch: UserPatch): User {
  return { ...user, ...patch };
}

const u: User = { id: 1, name: "Ада" };
console.log(applyPatch(u, { email: "ada@example.org" }));
console.log(applyPatch(u, { name: "Ада Лавлейс" }));
// applyPatch(u, { id: 2 });   // TS2353: 'id' не существует в типе UserPatch
```
```output
{ id: 1, name: 'Ада', email: 'ada@example.org' }
{ id: 1, name: 'Ада Лавлейс' }
```

**Пример 2. Свой отображаемый тип.**

```typescript run
type Mutable<T> = { -readonly [K in keyof T]: T[K] };
type Nullable<T> = { [K in keyof T]: T[K] | null };

interface Point { readonly x: number; readonly y: number }

const p: Mutable<Point> = { x: 1, y: 2 };
p.x = 10;                                  // без Mutable: TS2540

const empty: Nullable<Point> = { x: null, y: 5 };
console.log(p, empty);
```
```output
{ x: 10, y: 2 } { x: null, y: 5 }
```

**Пример 3. Фильтрация ключей и условный тип.** Образец проверяет тождество типов на этапе компиляции: если бы `Equal` вернул `false`, присваивание `true` не скомпилировалось бы.

```typescript run
type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;

type StringKeys<T> = { [K in keyof T as T[K] extends string ? K : never]: T[K] };
type ElementOf<T> = T extends readonly (infer E)[] ? E : never;

interface Row { id: number; title: string; author: string }

const t1: Equal<StringKeys<Row>, { title: string; author: string }> = true;
const t2: Equal<ElementOf<readonly number[]>, number> = true;
const t3: Equal<ElementOf<string[] | number[]>, string | number> = true;
const t4: Equal<ElementOf<number>, never> = true;

console.log(t1, t2, t3, t4);
```
```output
true true true true
```

**Пример 4. Распределение по объединению.**

```typescript run
type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;

type Dist<T> = T extends string ? "s" : "o";
type NoDist<T> = [T] extends [string] ? "s" : "o";

const a: Equal<Dist<string | number>, "s" | "o"> = true;
const b: Equal<NoDist<string | number>, "o"> = true;
const c: Equal<Dist<never>, never> = true;
const d: Equal<NoDist<never>, "s"> = true;

console.log(a, b, c, d);
```
```output
true true true true
```

**Пример 5. ReturnType и Awaited.**

```typescript run
async function loadCount(): Promise<number> {
  return 42;
}

type Loaded = Awaited<ReturnType<typeof loadCount>>;   // number

async function main(): Promise<void> {
  const n: Loaded = await loadCount();
  console.log(n + 1);
}

main();
```
```output
43
```

**Пример 6. Pick против Omit.** `Omit` не проверяет имена ключей.

```typescript run
interface U { id: number; name: string }

type A = Omit<U, "nme">;   // опечатка: компилируется, но ничего не убирает
const a: A = { id: 1, name: "Ада" };
// type B = Pick<U, "nme">; // TS2344: Type '"nme"' does not satisfy the constraint 'keyof U'

console.log(a);
```
```output
{ id: 1, name: 'Ада' }
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать `Partial`, `Readonly` и `Required` глубокими.** Они работают только на верхнем уровне.
* **Опечатка в ключе `Omit`.** `Omit<User, "idd">` ничего не вырежет и не даст ошибки. Для проверяемого варианта пишите `Omit<T, Extract<keyof T, "id">>` или используйте `Pick`.
* **Ожидать, что условный тип не распределяется.** `T extends U ? X : Y` для голого параметра применяется к каждому члену объединения; чтобы получить один результат, оберните обе стороны в `[ ]`.
* **Приводить результат вместо построения типа.** `{} as Pick<T, K>` внутри реализации иногда неизбежно (компилятор не знает, что вы заполните все поля), но сигнатура должна оставаться точной: именно от неё зависят вызывающие.
* **Слишком хитрые типы.** Если тип занимает три строки с вложенными `infer`, подумайте, нет ли более простого решения (перегрузки, обычный интерфейс).
* **Смешивать типы и значения.** `ReturnType<f>` не скомпилируется: нужен `typeof f`.

<!-- section: summary -->
## Коротко

* `Partial`, `Required`, `Readonly`, `Pick`, `Omit`, `Record`, `ReturnType`, `Awaited` строят типы из других типов; первые три поверхностные.
* Отображаемый тип `{ [K in keyof T]: ... }` проходит по ключам; `-readonly`, `-?` и `as` меняют модификаторы и имена.
* Условный тип `T extends U ? X : Y` с `infer` достаёт части типов и распределяется по объединениям.
* `Pick` проверяет ключи, `Omit` - нет.
* Сложные типы проверяйте утверждениями вида `Equal<A, B>`, как это делает упражнение.
