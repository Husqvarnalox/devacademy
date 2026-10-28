<!-- section: simple -->
## Зачем кэш и что делает «use cache»

Каждый показ страницы может запускать одну и ту же работу: запрос к базе, обращение к внешнему API, тяжёлое вычисление. Если результат не изменился, повторять работу расточительно: страница отвечает медленнее, а источник данных получает лишнюю нагрузку. **Кэш** хранит результат работы и отдаёт его, пока результат считается свежим.

В Next.js 16 кэширование включается флагом **Cache Components** в `next.config.ts`:

```ts
// next.config.ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
};

export default nextConfig;
```

Правило при включённом флаге: **всё динамическое по умолчанию**, а кэшировать нужно явно директивой **`"use cache"`**. Её ставят первой строкой тела асинхронной функции или компонента (либо в начале файла - тогда кэшируется каждый экспорт):

```ts
// lib/catalog.ts
import { cacheLife, cacheTag } from "next/cache";

export async function getProducts() {
  "use cache";
  cacheTag("products");
  cacheLife("hours");
  return db.query("SELECT * FROM products");
}
```

Здесь три разных вещи:

* `"use cache"` - объявляет, что результат функции можно сохранить и использовать повторно;
* `cacheLife("hours")` - **срок жизни** записи (профиль `hours`: см. таблицу ниже);
* `cacheTag("products")` - **тег**: ярлык, по которому запись можно сбросить после изменения данных.

Идею кэширования по ключу показывает модель на чистом TypeScript. Ключом служат аргументы функции: у каждого набора аргументов своя запись.

```typescript run
const sourceCalls: string[] = [];

async function loadPrices(currency: string): Promise<string> {
  sourceCalls.push(currency);
  return `цены в ${currency}`;
}

function cached<A extends unknown[], R>(fn: (...args: A) => Promise<R>) {
  const entries = new Map<string, R>();
  return async (...args: A): Promise<R> => {
    const key = JSON.stringify(args);
    if (!entries.has(key)) entries.set(key, await fn(...args));
    return entries.get(key) as R;
  };
}

const getPricesModel = cached(loadPrices);

async function modelMain() {
  await getPricesModel("RUB");
  await getPricesModel("RUB");
  await getPricesModel("USD");
  console.log(sourceCalls.join(", "));
}
modelMain();
```
```output
RUB, USD
```

Источник вызван дважды, а не трижды: второй вызов с `"RUB"` взят из кэша. Настоящий механизм Next.js сложнее (запись живёт ограниченное время и может храниться вне процесса), но ключ строится по тому же принципу.

<!-- section: precise -->
## Точные правила

### Ключ кэша и сериализация

Ключ записи составляют: идентификатор сборки, идентификатор функции (хэш её расположения и сигнатуры), **сериализуемые аргументы** и значения, которые функция замкнула из внешней области видимости. Поэтому разные аргументы дают разные записи, а новая сборка сбрасывает весь кэш.

Аргументы и результат должны быть сериализуемыми. Допустимы примитивы, обычные объекты, массивы, `Date`, `Map`, `Set`. Нельзя передавать экземпляры классов, функции (кроме сквозной передачи, как `children`), символы и `URL`. Результат может быть и JSX.

### Профили cacheLife

`cacheLife` принимает имя профиля или объект. Профиль задаёт три времени: `stale` (сколько клиент может обходиться без обращения к серверу), `revalidate` (после какого срока следующий запрос запустит фоновое обновление) и `expire` (после какого срока без запросов следующий запрос ждёт свежих данных).

| Профиль | `stale` | `revalidate` | `expire` |
| --- | --- | --- | --- |
| `default` | 5 минут | 15 минут | никогда |
| `seconds` | 30 секунд | 1 секунда | 1 минута |
| `minutes` | 5 минут | 1 минута | 1 час |
| `hours` | 5 минут | 1 час | 1 день |
| `days` | 5 минут | 1 день | 1 неделя |
| `weeks` | 5 минут | 1 неделя | 30 дней |
| `max` | 5 минут | 30 дней | 1 год |

Если `cacheLife` не вызвать, действует `default`; документация советует всегда указывать срок явно. `cacheLife` и `cacheTag` работают только внутри области кэша; на уровне модуля вызов бросает ошибку.

### Теги и сброс

`cacheTag` принимает одну или несколько строк: `cacheTag("products", "product-" + id)` помечает запись обоими тегами. Сбросить записи по тегу можно тремя способами:

| | `revalidateTag(tag, "max")` | `updateTag(tag)` | `revalidatePath(path)` |
| --- | --- | --- | --- |
| Где вызывается | серверное действие, обработчик маршрута | **только** серверное действие | серверная функция, обработчик маршрута |
| Поведение | пометить устаревшим: пока идёт обновление, отдаётся прежнее значение (stale-while-revalidate) | сразу списать запись: следующее чтение ждёт свежих данных | сбросить кэш для адреса |
| Когда нужно | фоновое обновление, небольшая задержка допустима | пользователь только что изменил данные и должен их увидеть (read-your-own-writes) | неизвестно, какие теги связаны со страницей |

Второй аргумент `revalidateTag` - профиль, который задаёт, как долго можно отдавать устаревшее значение; рекомендуемый - `"max"`. Форма `revalidateTag(tag)` с одним аргументом устарела, в Next.js 16 её заменяют `updateTag` (в действии) или `revalidateTag(tag, "max")`. Теги предпочтительнее путей: они точнее и не сбрасывают лишнего.

Фоновая логика видна в модели. `revalidateTagModel` лишь помечает запись, а `updateTagModel` удаляет её:

```typescript run
let version = 0;
async function load(): Promise<string> {
  version += 1;
  return `версия ${version}`;
}

type Entry = { value: string; tags: string[]; stale: boolean };
const entries = new Map<string, Entry>();
const background: Promise<void>[] = [];

async function read(key: string, tags: string[]): Promise<string> {
  const entry = entries.get(key);
  if (!entry) {
    const value = await load();
    entries.set(key, { value, tags, stale: false });
    return value;
  }
  if (entry.stale) {
    entry.stale = false; // обновление уже запущено
    background.push(load().then((value) => void entries.set(key, { value, tags, stale: false })));
  }
  return entry.value;
}

function revalidateTagModel(tag: string): void {
  for (const entry of entries.values()) if (entry.tags.includes(tag)) entry.stale = true;
}
function updateTagModel(tag: string): void {
  for (const [key, entry] of entries) if (entry.tags.includes(tag)) entries.delete(key);
}

async function scenario() {
  console.log(await read("list", ["products"]));
  revalidateTagModel("products");
  console.log(await read("list", ["products"])); // отдано устаревшее, обновление пошло в фоне
  await Promise.all(background);
  console.log(await read("list", ["products"]));
  updateTagModel("products");
  console.log(await read("list", ["products"])); // записи нет: чтение ждёт загрузки
}
scenario();
```
```output
версия 1
версия 1
версия 2
версия 3
```

### Динамические API нельзя в кэше

`cookies()`, `headers()` и `searchParams` зависят от запроса, а кэш общий для запросов. Поэтому внутри области кэша их читать нельзя (ошибка `next-request-in-use-cache`; запрет действует и на вспомогательные функции, которые кэшируемая функция вызывает). Значение читают **снаружи** и передают **аргументом**: оно попадает в ключ кэша. Компонент, который читает такие данные, оборачивают в `<Suspense>`: во время сборки в статическую оболочку попадёт запасной вариант (`fallback`), а содержимое придёт потоком при запросе.

Для редких случаев есть варианты `"use cache: private"` и `"use cache: remote"`, этот урок их не разбирает.

### Статическая оболочка и сборка

При сборке (`next build`) Next.js отрисовывает дерево маршрута заранее. Результаты `"use cache"` с достаточно долгим сроком жизни, предсказуемые вычисления и запасные варианты `<Suspense>` образуют **статическую оболочку** (static shell): HTML и сериализованные данные для быстрой начальной загрузки. Остальное досылается потоком при запросе. Такой подход документация называет частичным предрендером (Partial Prerendering). Слишком короткий срок жизни (например, профиль `seconds`: его `expire` меньше 5 минут) исключает запись из предрендера: получается «динамическая дыра», которая решается при запросе.

В выводе `next build` у маршрутов есть значки с легендой: `○ (Static)` - полностью статический маршрут, `◐ (Partial Prerender)` - статический HTML с динамическим содержимым, досылаемым потоком. Сборка в этом курсе не запускается, а значит, и значки вы не увидите: упражнение проверяет код, а не вывод `next build`.

### Что проверяет тест в упражнении

Vitest не содержит компилятора Next.js, поэтому `"use cache"` для него - обычная строка, а кэш не работает. Проверку такого кода строят из того, что доступно: тест читает исходный файл и убеждается, что директива стоит первым оператором тела; подменяет `next/cache` макетами, которые записывают вызовы `cacheTag`, `cacheLife`, `updateTag`, `revalidateTag`. Настоящее кэширование появляется только при `next build` и `next start`.

```typescript run
const source = `
export async function getProducts() {
  "use cache";
  return [];
}
export async function getBad() {
  const x = 1;
  "use cache";
  return x;
}`;

function startsWithUseCache(src: string, name: string): boolean {
  const pattern = new RegExp(`function\\s+${name}\\s*\\([^)]*\\)\\s*\\{\\s*"use cache"`);
  return pattern.test(src);
}

console.log(startsWithUseCache(source, "getProducts"), startsWithUseCache(source, "getBad"));
```
```output
true false
```

Директива, стоящая не первой, не действует как директива: это обычное выражение-строка.

<!-- section: examples -->
## Примеры

**Пример 1. Данные с тегом и сроком жизни.**

```ts
// lib/catalog.ts
import { cacheLife, cacheTag } from "next/cache";

export async function getProduct(id: string) {
  "use cache";
  cacheTag("products", "product-" + id);
  cacheLife("hours");
  return db.product.findUnique({ where: { id } });
}
```

Запись помечена общим тегом списка и своим тегом товара. Аргумент `id` входит в ключ: у каждого товара своя запись.

**Пример 2. Динамическое значение снаружи, кэш внутри.**

```tsx
// app/products/page.tsx
import { Suspense } from "react";
import { cookies } from "next/headers";
import { getPrices } from "@/lib/catalog";

export default function ProductsPage() {
  return (
    <>
      <h1>Каталог</h1>
      <Suspense fallback={<p>Загрузка цен...</p>}>
        <Prices />
      </Suspense>
    </>
  );
}

async function Prices() {
  const currency = (await cookies()).get("currency")?.value ?? "RUB";
  const prices = await getPrices(currency); // валюта - аргумент, а значит часть ключа
  return <pre>{JSON.stringify(prices)}</pre>;
}
```

`Prices` читает cookie, потому что она не кэшируется. `getPrices` кэшируется и получает значение как обычный аргумент. Заголовок и запасной вариант попадают в статическую оболочку, цены приходят потоком.

**Пример 3. Мутация: свой товар сразу, списки в фоне.**

```ts
"use server";

import { revalidateTag, updateTag } from "next/cache";

export async function updatePrice(id: string, price: number) {
  await db.product.update({ where: { id }, data: { price } });
  updateTag("product-" + id); // администратор сразу видит новую цену
  revalidateTag("products", "max"); // общие списки обновятся в фоне
}
```

**Пример 4. Кэш на уровне компонента.**

```tsx
export async function Banner() {
  "use cache";
  cacheLife("days");
  const banner = await getBanner();
  return <aside>{banner.text}</aside>;
}
```

Кэшируется результат отрисовки целиком, включая JSX.

**Пример 5. Прежняя модель.** Если `cacheComponents` не включён, действует прежний подход: `fetch` с параметром `cache: "force-cache"`, `unstable_cache` и настройки сегмента маршрута. Курс использует только Cache Components; о прежней модели рассказывает руководство «Caching and Revalidating (Previous Model)».

<!-- section: mistakes -->
## Типичные ошибки

* **`cookies()` или `headers()` внутри `"use cache"`.** Кэш общий для запросов, поэтому чтение данных запроса там запрещено. Прочитайте значение снаружи и передайте аргументом.
* **Директива не первая.** Строка `"use cache"` после другого оператора - обычное выражение и кэша не создаёт. Она должна открывать тело функции.
* **`revalidateTag(tag)` с одним аргументом.** Форма устарела. Используйте `revalidateTag(tag, "max")` или `updateTag(tag)` в серверном действии.
* **`updateTag` вне серверного действия.** В обработчике маршрута или клиентском компоненте он недоступен; для вебхуков берут `revalidateTag(tag, "max")` (или `{ expire: 0 }`, если нужно сбросить немедленно).
* **Забытый тег.** Если запись не помечена `cacheTag`, сбросить её по тегу нельзя, и данные устареют только по сроку жизни.
* **Несериализуемый аргумент.** Экземпляр класса или `URL` в аргументах не пройдёт. Передавайте примитивы и простые объекты.
* **Слишком короткий срок.** Профиль `seconds` исключает запись из статической оболочки: выигрыша от предрендера не будет.
* **Ожидание постоянного кэша в serverless.** Кэш по умолчанию хранится в памяти процесса; в среде, где каждый запрос может выполнять другой экземпляр, записи могут не переживать запрос.
* **Уверенность, что тест проверил кэширование.** Vitest не выполняет `"use cache"`: он подтверждает объявление и вызовы, а не работу кэша. Поведение проверяют сборкой и запуском приложения.

<!-- section: summary -->
## Итоги

* С флагом `cacheComponents: true` всё динамическое по умолчанию, кэшируют явно: `"use cache"` первой строкой функции, компонента или файла.
* Ключ записи состоит из сериализуемых аргументов и замкнутых значений; каждая комбинация - отдельная запись.
* `cacheLife(профиль)` задаёт срок (`hours`, `days`, `max` и другие), `cacheTag(...теги)` - ярлыки для сброса.
* `revalidateTag(tag, "max")` помечает запись устаревшей (stale-while-revalidate), `updateTag(tag)` списывает её сразу (только серверные действия), `revalidatePath` сбрасывает по адресу.
* `cookies()`, `headers()`, `searchParams` читают вне кэша и передают аргументами; динамическую часть оборачивают в `<Suspense>`.
* Статическая оболочка собирается при `next build`; тесты Vitest проверяют объявление кэша и сброс, а не само кэширование.

<!-- section: programmer -->
## Если вы знаете другой веб-фреймворк

* `"use cache"` с `cacheTag` и `cacheLife` напоминает мемоизацию функции с ключом по аргументам, TTL и тегами инвалидации, как у кэшей Redis с наборами тегов; разница в том, что хранилище и распространение записей по экземплярам определяет платформа (по умолчанию - память процесса).
* `revalidateTag(tag, "max")` соответствует схеме stale-while-revalidate из HTTP-кэширования, `updateTag` - немедленной инвалидации записи.
* Без флага `cacheComponents` действует прежняя модель (`fetch` с `cache: "force-cache"`, `unstable_cache`, настройки сегмента); с флагом то же самое делают директива и функции выше.
* Статическая оболочка с потоковыми «дырами» - это частичный предрендер: часть страницы готова при сборке, часть строится по запросу.
