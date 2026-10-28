<!-- section: simple -->
## Компонент, который выполняется на сервере

В обычном React-приложении компонент всегда исполняется в браузере: сначала загружается JavaScript, потом компонент запрашивает данные (чаще всего в эффекте) и только затем показывает результат. В App Router у компонента есть другой режим. По умолчанию страницы и макеты в `app/` - **серверные компоненты** (Server Components): они выполняются на сервере, а в браузер приходит уже готовый результат их работы, без кода самого компонента.

Из этого следует главное удобство: серверный компонент может быть **асинхронной функцией**. Он выполняет `await` прямо в теле, обращается к базе данных или внутреннему API и возвращает разметку. Не нужны ни `useEffect`, ни `useState` для «загрузки», ни промежуточный API-маршрут только ради того, чтобы страница получила данные.

```tsx
// app/products/[id]/page.tsx
import { getProduct } from "../../../lib/products";

export default async function ProductPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const product = await getProduct(id);
  return <h1>{product?.name}</h1>;
}
```

Здесь две вещи, которые нужно запомнить сразу. Во-первых, `params` - это **промис**: значения динамических сегментов (`[id]`) приходят как `Promise`, и перед использованием их нужно дождаться через `await`. В Next.js 16 синхронный доступ удалён. Во-вторых, компонент ничего не знает про браузер: у него нет состояния, обработчиков событий и `window`. Интерактивность выносится в клиентские компоненты, их разберёт [следующий урок](lesson:nextjs.fundamentals.client-components).

Идею можно записать без Next.js: серверный компонент - это функция, которая сначала ждёт данные, а потом возвращает результат.

```typescript run
type Product = { id: string; name: string; price: number };

const db: Product[] = [
  { id: "1", name: "Чайник", price: 1490 },
  { id: "2", name: "Блокнот", price: 350 },
];

async function getProduct(id: string): Promise<Product | undefined> {
  return db.find((product) => product.id === id);
}

async function ProductPage(props: { params: Promise<{ id: string }> }): Promise<string> {
  const { id } = await props.params;
  const product = await getProduct(id);
  if (!product) return "<p>Не найдено</p>";
  return `<h1>${product.name}</h1><p>${product.price} ₽</p>`;
}

async function main() {
  console.log(await ProductPage({ params: Promise.resolve({ id: "1" }) }));
  console.log(await ProductPage({ params: Promise.resolve({ id: "42" }) }));
}
main();
```
```output
<h1>Чайник</h1><p>1490 ₽</p>
<p>Не найдено</p>
```

Настоящий компонент возвращает не строку, а элементы React, но порядок тот же: дождаться параметры, дождаться данные, вернуть результат. Именно так устроено и упражнение: тесты вызывают вашу страницу как обычную функцию, ждут её результат и отрисовывают его.

<!-- section: examples -->
## Примеры

**Страница списка с параметрами запроса.** Часть адреса после `?` (например, `/products?sort=price`) приходит в страницу через `searchParams`. Это тоже промис.

```tsx
// app/products/page.tsx
export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<{ sort?: string }>;
}) {
  const { sort } = await searchParams;
  const products = await getProducts();
  // ...отсортировать по sort и вывести список
}
```

**Макет с общей оболочкой.** Файл `layout.tsx` получает `children` - страницу или вложенный макет - и оборачивает её. Корневой макет `app/layout.tsx` обязан содержать `<html>` и `<body>`.

```tsx
// app/products/layout.tsx
export default function ProductsLayout({ children }: { children: React.ReactNode }) {
  return (
    <section aria-label="Каталог">
      <nav>{/* ссылки сортировки */}</nav>
      {children}
    </section>
  );
}
```

Вложенность макетов можно смоделировать функциями: каждый макет получает готовое содержимое и возвращает разметку вокруг него.

```typescript run
type Layout = (children: string) => string;

const rootLayout: Layout = (children) => `<html><body>${children}</body></html>`;
const catalogLayout: Layout = (children) => `<section>${children}</section>`;

function renderWithLayouts(layouts: Layout[], page: string): string {
  // от корня вглубь: последний макет оборачивает страницу первым
  return layouts.reduceRight((inner, layout) => layout(inner), page);
}

console.log(renderWithLayouts([rootLayout, catalogLayout], "<h1>Каталог</h1>"));
console.log(renderWithLayouts([rootLayout], "<h1>Главная</h1>"));
```
```output
<html><body><section><h1>Каталог</h1></section></body></html>
<html><body><h1>Главная</h1></body></html>
```

**Метаданные.** Заголовок вкладки и описание страницы задаёт экспорт из `page.tsx` или `layout.tsx`. Если значения известны заранее, экспортируют объект `metadata`. Если зависят от данных - функцию `generateMetadata`.

```tsx
// app/products/[id]/page.tsx
import type { Metadata } from "next";

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }): Promise<Metadata> {
  const { id } = await params;
  const product = await getProduct(id);
  return product ? { title: product.name, description: product.description } : { title: "Товар не найден" };
}
```

**Отсутствующий товар.** Для неизвестного `id` страница вызывает `notFound()` из `next/navigation`. Функция прерывает отрисовку сегмента и показывает ближайший файл `not-found.tsx` (или стандартную страницу 404).

```tsx
const product = await getProduct(id);
if (!product) {
  notFound();
}
// ниже TypeScript знает, что product определён
```

**Заранее известные адреса.** Функция `generateStaticParams` перечисляет значения динамического сегмента, для которых страницы можно подготовить заранее, на этапе сборки: `[{ id: "1" }, { id: "2" }]`.

<!-- section: precise -->
## Как это работает точно

**Что делает сервер.** Next.js отрисовывает дерево по частям, по сегментам маршрута (макеты и страницы). Серверные компоненты превращаются в особый формат - **RSC Payload** (React Server Component Payload): компактное описание результата. В нём лежит отрисованный результат серверных компонентов, места для клиентских компонентов со ссылками на их JavaScript и props, которые серверный компонент передал клиентскому. Из этого же описания на сервере собирается начальный HTML. В браузер код серверных компонентов не попадает.

**Что серверному компоненту недоступно.** Состояние (`useState`), эффекты (`useEffect`), обработчики событий (`onClick`) и браузерные API (`window`, `localStorage`) работают только в клиентских компонентах. Взамен серверный компонент свободно обращается к данным и секретам: ключи и строки подключения остаются на сервере.

**Параметры.** `params` содержит значения динамических сегментов (для `app/shop/[slug]/page.tsx` и адреса `/shop/1` это `{ slug: "1" }`, обёрнутый в `Promise`). `searchParams` содержит пары из строки запроса; значение может быть строкой, массивом строк (для `?a=1&a=2`) или отсутствовать. Это обычный объект, а не `URLSearchParams`. Обращение к `searchParams` делает страницу динамической: её нельзя целиком подготовить заранее, потому что значения известны только при запросе.

**Макеты не видят `searchParams`.** Макет не перерисовывается при переходах между его страницами, поэтому значения запроса в нём устарели бы. Параметры запроса читают в странице или, на стороне клиента, хуком `useSearchParams`.

**Метаданные.** Экспорты `metadata` и `generateMetadata` работают только в серверных компонентах, и из одного сегмента нельзя экспортировать оба. Метаданные должны быть известны до отрисовки страницы, чтобы попасть в первый HTML-ответ. Для заголовка есть шаблон: `title.template` в макете (например, `"%s | Магазин"`) добавляет префикс или суффикс к заголовкам вложенных сегментов; при этом для шаблона нужно задать и `default`.

**Одинаковые запросы не дублируются.** Запросы `fetch` с одинаковыми аргументами внутри `generateMetadata`, `generateStaticParams`, макетов и страниц автоматически объединяются. Если данные берутся не через `fetch` (например, из базы), для того же эффекта оборачивают функцию доступа в React `cache`. Идея видна в модели: результат запоминается на время запроса, и второй вызов с тем же аргументом не обращается к источнику.

```typescript run
function memoizePerRequest<A, R>(load: (arg: A) => Promise<R>): (arg: A) => Promise<R> {
  const results = new Map<A, Promise<R>>();
  return (arg) => {
    const known = results.get(arg);
    if (known) return known;
    const started = load(arg);
    results.set(arg, started);
    return started;
  };
}

let queries = 0;
async function loadTitle(id: string): Promise<string> {
  queries += 1;
  return `Товар №${id}`;
}

async function main() {
  const getTitle = memoizePerRequest(loadTitle);
  const forMetadata = await getTitle("7");
  const forPage = await getTitle("7");
  console.log(forMetadata, "|", forPage);
  console.log("запросов к источнику:", queries);
  await getTitle("8");
  console.log("после другого id:", queries);
}
main();
```
```output
Товар №7 | Товар №7
запросов к источнику: 1
после другого id: 2
```

**`notFound()`.** Вызов бросает специальную ошибку и завершает отрисовку сегмента, где он произошёл; код ниже не выполняется. Поэтому `notFound()` вызывают в самом пути отрисовки (в компоненте или в функции, которую компонент ждёт через `await`). Next.js добавляет на такую страницу тег `<meta name="robots" content="noindex" />`. Пока потоковая передача не началась, ответ получает статус 404; если она уже началась, статус остаётся 200.

**`loading.tsx`.** Файл рядом со страницей задаёт запасной интерфейс: пока серверный компонент ждёт данные, браузер видит его, а готовое содержимое подставляется следом (**потоковая передача**, streaming). Технически `loading.tsx` оборачивает страницу в границу Suspense.

**`generateStaticParams`.** Возвращает массив объектов, где ключ - имя динамического сегмента, а значение - строка (для `[...slug]` - массив строк). Для значений, которых в списке нет, поведение задаёт настройка `dynamicParams`.

<!-- section: mistakes -->
## Типичные ошибки

* **`params.id` без `await`.** Параметры - промис, поэтому `params.id` даст `undefined`. Нужно `const { id } = await params;`. Синхронный доступ в Next.js 16 удалён.
* **`useState` или `onClick` в серверном компоненте.** Next.js сообщит об ошибке: хуки состояния и обработчики событий доступны только в клиентских компонентах. Выносите интерактивную часть в отдельный компонент с `"use client"`.
* **Обращение к товару до проверки.** Строку `if (!product) notFound();` ставят сразу после загрузки: функция прерывает отрисовку, а TypeScript знает, что ниже `product` определён. Если проверку пропустить, страница упадёт на `undefined`.
* **Параметры запроса в макете.** `searchParams` есть только у страницы. Макет, которому они нужны для содержимого, не получит свежих значений: переносите логику в страницу или клиентский компонент.
* **Оба экспорта метаданных.** `metadata` и `generateMetadata` нельзя объявлять в одном сегменте одновременно.
* **Дублирование загрузки в цепочке.** Если макет, страница и `generateMetadata` по отдельности читают один и тот же ресурс, не через `fetch`, источник опрашивается несколько раз. Оберните доступ к данным в `cache` из React.
* **Предположение, что `searchParams.sort` всегда строка.** При `?sort=a&sort=b` придёт массив. В упражнении значение, отличное от `"price"`, считается неизвестным и даёт сортировку по названию.

<!-- section: summary -->
## Итоги

* `page` и `layout` в `app/` по умолчанию - серверные компоненты; они могут быть `async` и выполнять `await` прямо в теле.
* `params` и `searchParams` - промисы; их ждут через `await`. Макет `searchParams` не получает.
* Метаданные задаются объектом `metadata` или функцией `generateMetadata` (только один из двух вариантов на сегмент).
* Для отсутствующих данных вызывают `notFound()`: отрисовка сегмента прерывается, показывается `not-found`.
* `generateStaticParams` перечисляет значения динамического сегмента, которые можно подготовить заранее.
* Интерактивность (состояние, события, браузерные API) требует клиентского компонента - тема [урока про границу `"use client"`](lesson:nextjs.fundamentals.client-components).

<!-- section: programmer -->
## Если вы приходите из другого фреймворка

* В Next.js Pages Router данные загружали в `getServerSideProps` и `getStaticProps`, а страница получала их через props. В App Router эти функции не используются: страница сама `async`, и данные берутся там, где нужны.
* Аналог серверной загрузки данных в SPA - запрос в эффекте после загрузки JS. Здесь запрос идёт до отправки HTML, поэтому страница приходит с данными.
* Отличие от классических шаблонизаторов (PHP, Rails, Django): компонент остаётся функцией React, и результат можно смешивать с интерактивными клиентскими компонентами в одном дереве.
* Аналог `generateStaticParams` в Pages Router - `getStaticPaths`; аналог `notFound()` - `return { notFound: true }`.

<!-- section: deep-dive -->
## Подробнее

**Рендеринг по сегментам.** Макеты и страницы отрисовываются как отдельные части, поэтому пока страница ждёт данные, браузер может показывать содержимое `loading.tsx`, а готовый результат подставляется позже. Если на странице несколько независимых `await`, выполняйте их параллельно (`Promise.all`), иначе второй запрос ждёт первый без необходимости.

**Почему упражнение вызывает страницу как функцию.** Серверный компонент - это функция от пропсов, которая возвращает элементы. Ни браузера, ни сервера Next.js для проверки такой функции не нужно: тест передаёт `params` и `searchParams` в виде готовых промисов, ждёт результат и отдаёт его Testing Library. Настоящий `notFound()` бросает внутреннюю ошибку Next.js, поэтому в тесте он заменён макетом, который бросает узнаваемую `Error`.
