<!-- section: simple -->
## Что остаётся от TypeScript во время работы

TypeScript - это JavaScript плюс система типов, которая **целиком исчезает** до запуска. Компилятор (`tsc`) проверяет типы и стирает их; Node или браузер запускают обычный JavaScript. Из этого следуют три практических правила.

1. Типы ничего не делают во время работы. Приведение `as number` не превращает строку в число, `interface` не существует в запущенной программе, а описание формы данных не защищает от неверных данных (об этом был третий урок).
2. Строгость задаётся настройками в `tsconfig.json`. Флаг `strict` включает основной набор проверок, но не все: `noUncheckedIndexedAccess` и `exactOptionalPropertyTypes` включают отдельно.
3. Современные способы запуска (Node с удалением типов, быстрые сборщики) не умеют превращать «синтаксис с поведением» вроде `enum` или свойств-параметров конструктора. Поэтому кроме проверки типов важно писать код на **стираемом подмножестве** TypeScript: в нём каждый кусок типов можно просто удалить, оставив рабочий JavaScript.

```typescript
const raw = "5" as unknown as number;   // тип изменён, значение нет
console.log(typeof raw);                // "string"
```

<!-- section: precise -->
## tsconfig, стирание типов, enum и файлы объявлений

**`strict` и отдельные флаги.** `strict` объединяет `noImplicitAny`, `strictNullChecks`, `strictFunctionTypes`, `useUnknownInCatchVariables` и ещё несколько. Не входят в него (проверено `tsc` 7.0.2: код с `const a: number = xs[0]` и `{ r: undefined }` для `r?: number` проходит при одном `--strict`):

* `noUncheckedIndexedAccess` - чтение по индексу (`xs[i]`, `dict[key]`) получает тип `T | undefined`. Лечится проверкой, `??` или сигнатурой, где `undefined` предусмотрен. Это самая полезная опция после `strict`;
* `exactOptionalPropertyTypes` - различает «поля нет» и «поле равно `undefined`»: `{ retries: undefined }` для `retries?: number` даёт TS2375. Чтобы убрать поле, его не присваивают, а исключают из объекта (деструктуризацией `const { retries, ...rest } = o`);
* `verbatimModuleSyntax` - требует `import type` для импорта только типов и сохраняет остальные импорты как есть;
* `erasableSyntaxOnly` (с TypeScript 5.8) - запрещает синтаксис, у которого есть поведение при выполнении (TS1294).

**Стираемое подмножество.** Node 22 умеет запускать `.ts`-файлы, просто вырезая типы (`--experimental-strip-types`; в более новых версиях Node запуск включён по умолчанию, в этом курсе проверен Node 22.15 с флагом). Он выполняет только то, что остаётся после удаления аннотаций. Поэтому не работают (проверено на Node 22.15): `enum` (`TypeScript enum is not supported in strip-only mode`), свойства-параметры конструктора (`constructor(public x: number)`), `namespace` с кодом. Остальные аннотации, `as`, `!`, `import type` и `interface` вырезаются безопасно. Файлы нужно импортировать с расширением (`./a.ts`), а типы - через `import type`: обычный `import { Foo }` для интерфейса упадёт с ошибкой «does not provide an export named 'Foo'».

**`enum` и его замена.** `enum` создаёт настоящий объект во время работы, поэтому требует преобразования кода. Эквивалент на стираемом синтаксисе - объект `as const` и объединение его значений:

```typescript
const Status = { Active: "active", Blocked: "blocked" } as const;
type Status = (typeof Status)[keyof typeof Status];   // "active" | "blocked"
```

Имя `Status` здесь обозначает и значение, и тип (они живут в разных пространствах имён). Строка `"active"` подходит под `Status` напрямую, а у `enum` строковый литерал под его тип не подходит.

**Стирание и обобщения.** После компиляции нет ни `T`, ни интерфейсов, поэтому нельзя спросить во время работы «это `User`?». Информацию о виде значения хранят явно: поле-дискриминант, функция-предикат, схема проверки.

**Файлы объявлений (`.d.ts`).** Это файлы, где есть только типы: они описывают код на JavaScript, который TypeScript не видит. Если импортировать пакет или файл `.js` без объявлений, при `strict` вы получите TS7016 (`Could not find a declaration file for module`): модуль неявно `any`. Для пакетов из npm объявления часто лежат в пакете `@types/имя`. Для своего кода рядом с `lib.js` создают `lib.d.ts`:

```typescript
// vendor/legacy-math.d.ts
export const VERSION: string;
export function clamp(n: number, lo: number, hi: number): number;
export function mean(xs: readonly number[]): number;
```

Объявление - обещание, как и предикат типа: компилятор не проверяет, что JavaScript действительно так работает. Описывайте то, что код делает на самом деле, и не используйте `any` там, где можно написать точный тип.

**Наблюдение о TypeScript 7.** Версия 7.0.2 - нативный компилятор; в проверенных примерах диагностика и коды ошибок совпадают с известными по 5.x. Заметное отличие в запуске: если в каталоге есть `tsconfig.json`, а файлы указаны в командной строке, `tsc` отвечает ошибкой TS5112, пока не добавить `--ignoreConfig`.

<!-- section: examples -->
## Примеры

**Пример 1. Типы не меняют значение.**

```typescript run
const raw = "5" as unknown as number;
console.log(typeof raw, raw + 1);

const checked = Number("5");
console.log(typeof checked, checked + 1);
```
```output
string 51
number 6
```

**Пример 2. Объект as const вместо enum.**

```typescript run
const Status = {
  Active: "active",
  Blocked: "blocked",
  Pending: "pending",
} as const;

type Status = (typeof Status)[keyof typeof Status];

function isStatus(v: unknown): v is Status {
  return Object.values(Status).some((s) => s === v);
}

const s: Status = "active";         // строковый литерал подходит напрямую
// const bad: Status = "deleted";   // TS2322
console.log(s, Status.Blocked, Object.values(Status), isStatus("pending"), isStatus("x"));
```
```output
active blocked [ 'active', 'blocked', 'pending' ] true false
```

**Пример 3. Класс без свойства-параметра.**

```typescript run
class Counter {
  value: number;

  constructor(value: number = 0) {
    this.value = value;
  }

  increment(): Counter {
    return new Counter(this.value + 1);
  }
}

console.log(new Counter().increment().increment().value);
```
```output
2
```

**Пример 4. Чтение по индексу с учётом undefined.** Образец проверяется приложением в режиме `strict`, поэтому `undefined` здесь предусмотрен в сигнатуре и без флага `noUncheckedIndexedAccess`: так код остаётся корректным при любых настройках.

```typescript run
function nth<T>(xs: readonly T[], i: number): T | undefined {
  return xs[i];
}

function head<T>(xs: readonly [T, ...T[]]): T {
  return xs[0];                    // в непустом кортеже первый элемент гарантирован
}

const words = ["a", "b"];
console.log(nth(words, 1) ?? "нет", nth(words, 9) ?? "нет");
console.log(head(words as [string, ...string[]]));
// head([]);   // TS2345: Source has 0 element(s) but target requires 1
```
```output
b нет
a
```

**Пример 5. Необязательное поле без undefined.**

```typescript run
interface Options { retries?: number }

function withRetries(o: Options, n: number | undefined): Options {
  const { retries: _old, ...rest } = o;
  return n === undefined ? rest : { ...rest, retries: n };
}

console.log(withRetries({ retries: 2 }, 5));
console.log(withRetries({ retries: 2 }, undefined));
console.log("retries" in withRetries({ retries: 2 }, undefined));
```
```output
{ retries: 5 }
{}
false
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `as` что-то преобразует.** Он только меняет тип для компилятора. Для преобразования нужен код: `Number(x)`, разбор, проверка.
* **Использовать `enum` в проекте, который запускается удалением типов.** Код упадёт при запуске. Используйте объект `as const` и объединение значений либо включите `erasableSyntaxOnly`, чтобы ошибка появилась сразу.
* **Писать `constructor(public x: number)`.** Это сокращение имеет поведение при выполнении и не стирается. Объявите поле и присвойте его в конструкторе.
* **Импортировать типы обычным `import`.** Для удаления типов нужно `import type`, иначе Node попробует найти значение с таким именем.
* **Заглушать TS7016 объявлением `declare module "x"` без типов.** Модуль станет `any` целиком. Опишите хотя бы используемые функции.
* **Не включать `noUncheckedIndexedAccess`.** Без него `xs[0]` и `dict[key]` считаются гарантированными значениями, а ошибки приходят на выполнении.

<!-- section: summary -->
## Коротко

* Типы стираются до запуска; приведение типа не изменяет значение.
* К `strict` стоит добавить `noUncheckedIndexedAccess`; `exactOptionalPropertyTypes` и `erasableSyntaxOnly` - по необходимости.
* Для запуска удалением типов пишите на стираемом подмножестве: без `enum`, `namespace` и свойств-параметров, с `import type`.
* Вместо `enum` используйте объект `as const` и объединение значений.
* `.d.ts` описывает типы для JavaScript-кода; такое объявление - обещание, его честность на вас.
