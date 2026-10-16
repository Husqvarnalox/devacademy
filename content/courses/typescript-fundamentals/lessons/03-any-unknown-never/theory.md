<!-- section: simple -->
## Данные снаружи нельзя считать своими

Всё, что приходит в программу снаружи, - тело HTTP-ответа, содержимое файла, `JSON.parse`, ввод пользователя, - TypeScript не может проверить: типы существуют только при компиляции. Если сказать компилятору «это `User`», он поверит, а ошибка вылезет позже, в совсем другом месте.

Поэтому у TypeScript есть два «верхних» типа с противоположным характером. **`any`** отключает проверку: с ним можно делать что угодно, и ошибки уходят вместе с типами. **`unknown`** означает «значение есть, но что это, неизвестно»: прямо использовать его нельзя, пока вы не проверили вид значения. Проверка - обычный код (`typeof`, `Array.isArray`, функция-предикат), и после неё компилятор знает точный тип. Для данных на границе программы выбирайте `unknown`.

Третий тип, **`never`**, - «значения не бывает»: у функции, которая всегда бросает исключение, и у ветки, куда программа попасть не может.

```typescript
function parse(text: string): unknown {
  return JSON.parse(text);          // JSON.parse возвращает any, наружу отдаём unknown
}

const data = parse('{"id": 1}');
// data.id;                       // ошибка: data имеет тип unknown
if (typeof data === "object" && data !== null && "id" in data) {
  console.log(data.id);             // теперь можно читать id (его тип - unknown)
}
```

<!-- section: precise -->
## any, unknown и never; безопасная работа с неизвестными данными

**`any`** совместим со всем в обе стороны. Присваивание `const n: number = anyValue` проходит без проверки, и любое обращение `anyValue.foo.bar()` компилируется. Хуже всего, что `any` заразен: результат операции с `any` тоже `any`. Параметр без аннотации, из которого нельзя вывести тип, при `strict` (флаг `noImplicitAny`) - ошибка TS7006, так что `any` приходится писать явно или получать из чужих объявлений: например, `JSON.parse` возвращает `any`, и `Response.json()` тоже.

**`unknown`** - безопасный двойник: присвоить в него можно что угодно, а вот из него без проверки - ничего, кроме `unknown` и `any`. Обращение к члену даёт TS18046 (`'x' is of type 'unknown'`), присваивание в `string` - TS2322. Допустимы только `===`, `typeof`, `instanceof` и другие проверки.

**Сужение `unknown`.** После `typeof x === "string"` тип `x` - `string`. После `Array.isArray(x)` - `any[]` (у `unknown` стандартная библиотека даёт именно такой результат), поэтому элементы массива снова нужно проверять. После `typeof x === "object" && x !== null` вы получаете `object`: свойств у него по-прежнему нет, но оператор `"id" in x` добавляет их как `unknown`.

**Пользовательская функция-предикат** `function isRecord(v: unknown): v is Record<string, unknown>` описывает проверку один раз, а компилятор сужает тип в каждом месте вызова. Компилятор **не проверяет**, что тело предиката честное: `return true` в нём скомпилируется. Предикат - обещание, которое вы обязаны выполнить сами. Версия `asserts v is T` работает так же, но вместо `true`/`false` бросает исключение.

**Блок `catch`.** При `strict` (флаг `useUnknownInCatchVariables`) переменная `e` в `catch (e)` имеет тип `unknown`: бросить можно что угодно, не только `Error`. Чтобы получить сообщение, проверьте `e instanceof Error`.

**Проверка данных на границе.** Типы стираются при компиляции, поэтому описание `interface User` не защищает от ответа сервера с другой формой. Правило: данные снаружи принимаются как `unknown`, проходят проверку, и только результат проверки получает тип `User`. Библиотеки вроде zod (и ей подобные) строят такие проверки из описания схемы и сами выводят тип; в этом курсе мы пишем проверки руками, чтобы понять механику. Приведение `data as User` ничего не проверяет: оно лишь велит компилятору замолчать.

**Результат проверки.** Удобно возвращать дискриминированное объединение `{ ok: true; value: User } | { ok: false; error: string }`: читать `value`, не проверив `ok`, компилятор не даст.

**`never`.** Функция, которая не может вернуться (`throw`, бесконечный цикл), имеет тип `never`, и её результат подходит под любой тип: `return fail("...")` допустимо в функции, возвращающей `User`. Тип параметра `never` принимает только значения `never`, на этом строится проверка исчерпываемости из прошлого урока.

<!-- section: examples -->
## Примеры

**Пример 1. unknown не пропускает без проверки.** Строки с ошибками закомментированы вместе с кодом ошибки (проверено `tsc` 7.0.2).

```typescript run
const raw: unknown = JSON.parse('{"name": "Ада"}');

// const s: string = raw;           // TS2322: Type 'unknown' is not assignable to type 'string'
// raw.name;                        // TS18046: 'raw' is of type 'unknown'

if (typeof raw === "object" && raw !== null && "name" in raw) {
  console.log(typeof raw.name);     // имя найдено, но его тип - unknown
}

function describe(x: unknown): string {
  if (typeof x === "string") return `строка длины ${x.length}`;
  if (typeof x === "number") return `число ${x.toFixed(1)}`;
  if (Array.isArray(x)) return `массив из ${x.length}`;
  return "что-то другое";
}

console.log(describe("abc"), "|", describe(2), "|", describe([1, 2]), "|", describe(null));
```
```output
string
строка длины 3 | число 2.0 | массив из 2 | что-то другое
```

**Пример 2. Предикат и catch.**

```typescript run
function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}

function messageOf(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}

try {
  const data: unknown = JSON.parse("{oops");
  console.log(isRecord(data));
} catch (e) {
  // e здесь unknown: бросить можно что угодно
  console.log("ошибка:", e instanceof SyntaxError);
}

try {
  throw "просто строка";
} catch (e) {
  console.log(messageOf(e), "|", messageOf(new Error("boom")));
}

console.log(isRecord({}), isRecord([]), isRecord(null));
```
```output
ошибка: true
просто строка | boom
true false false
```

**Пример 3. Проверка на границе и результат-объединение.**

```typescript run
type Point = { x: number; y: number };
type Parsed = { ok: true; value: Point } | { ok: false; error: string };

function parsePoint(input: unknown): Parsed {
  if (typeof input !== "object" || input === null) return { ok: false, error: "ожидался объект" };
  const { x, y } = input as Record<string, unknown>;
  if (typeof x !== "number") return { ok: false, error: "x не число" };
  if (typeof y !== "number") return { ok: false, error: "y не число" };
  return { ok: true, value: { x, y } };
}

for (const text of ['{"x": 1, "y": 2}', '{"x": "1", "y": 2}', "42"]) {
  const r = parsePoint(JSON.parse(text));
  console.log(r.ok ? `точка ${r.value.x},${r.value.y}` : `ошибка: ${r.error}`);
}
```
```output
точка 1,2
ошибка: x не число
ошибка: ожидался объект
```

Здесь `as Record<string, unknown>` допустимо: приведение сужает `object` до записи со значениями `unknown`, то есть не обещает ничего лишнего, а каждое поле дальше проверяется. Приведение `input as Point` было бы тем самым «враньём», от которого проверка должна защищать.

**Пример 4. never и вызов, который не возвращается.**

```typescript run
function fail(message: string): never {
  throw new Error(message);
}

function port(env: Record<string, string | undefined>): number {
  const raw = env["PORT"];
  if (raw === undefined) return fail("PORT не задан");
  const n = Number(raw);
  return Number.isInteger(n) ? n : fail(`PORT не число: ${raw}`);
}

console.log(port({ PORT: "8080" }));
for (const env of [{}, { PORT: "abc" }]) {
  try {
    port(env);
  } catch (e) {
    console.log(e instanceof Error ? e.message : e);
  }
}
```
```output
8080
PORT не задан
PORT не число: abc
```

<!-- section: mistakes -->
## Типичные ошибки

* **`as User` вместо проверки.** Приведение стирается при компиляции и ничего не проверяет. Если сервер пришлёт другую форму, ошибка проявится далеко от места получения данных.
* **Тип `any` для «пока не знаю».** Он отключает проверку не только в этой переменной, но и во всём, что от неё производно. Пишите `unknown` и сужайте.
* **Дырявый предикат.** `v is User` с телом `return typeof v === "object"` скомпилируется, но принимает любой объект. Тестируйте предикаты так же, как обычные функции.
* **Проверка только верхнего уровня.** `Array.isArray(x)` не говорит, что внутри строки; проверьте и элементы (`every`).
* **`catch (e) { e.message }`.** При `strict` это ошибка, и справедливо: бросить можно строку или число. Проверяйте `e instanceof Error`.
* **Ловить всё и молчать.** Сообщение об ошибке проверки должно называть поле, иначе его не найти.

<!-- section: summary -->
## Коротко

* `any` отключает проверку, `unknown` требует проверку, `never` - значения не бывает.
* Данные из `JSON.parse`, сети и файлов принимайте как `unknown` и проверяйте до присваивания типа.
* Функция-предикат `v is T` сужает тип, но компилятор не проверяет её честность.
* В `catch` переменная имеет тип `unknown`; для сообщения проверяйте `instanceof Error`.
* Результат проверки удобно возвращать объединением `ok: true | false`.
* Библиотеки схем (zod и подобные) делают то же самое из описания схемы; идея та же: типы существуют только до запуска, поэтому границу программы защищает код.
