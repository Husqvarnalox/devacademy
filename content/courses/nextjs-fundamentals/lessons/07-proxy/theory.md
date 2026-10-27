<!-- section: simple -->
## Код перед маршрутом

Запрос браузера проходит несколько этапов, прежде чем страница отрисуется: Next.js находит подходящий маршрут, собирает компоненты, читает данные. Иногда нужно вмешаться **до** всего этого: перенаправить старый адрес на новый, отправить неавторизованного пользователя на страницу входа, выбрать язык по cookie. Для таких случаев в Next.js 16 есть файл **`proxy.ts`**.

**Proxy** - функция, которая выполняется на сервере для входящего запроса раньше, чем начнётся работа маршрута. Она получает запрос и решает, что сделать с ним: пропустить дальше, перенаправить, переписать адрес или ответить самостоятельно. Название отражает суть: это сетевая граница перед приложением. В прежних версиях файл назывался `middleware.ts`; в Next.js 16 его переименовали в `proxy.ts`, а функцию - в `proxy`.

```ts
// proxy.ts (в корне проекта или в src/, на одном уровне с app)
import { NextResponse, type NextRequest } from "next/server";

export function proxy(request: NextRequest) {
  if (request.nextUrl.pathname.startsWith("/old-blog/")) {
    const target = request.nextUrl.clone();
    target.pathname = target.pathname.replace("/old-blog/", "/blog/");
    return NextResponse.redirect(target, 308);
  }
  return NextResponse.next();
}

export const config = {
  matcher: "/old-blog/:path*",
};
```

Здесь три части. `proxy(request)` - сама функция (можно назвать `proxy` и экспортировать именованно либо отдать как `export default`). `NextResponse.redirect(target, 308)` - ответ-перенаправление с кодом 308, то есть «адрес переехал навсегда». `NextResponse.next()` - «Proxy ничего не меняет, продолжайте». А `config.matcher` ограничивает, для каких адресов функция вообще вызывается.

Идею показывает модель на чистом TypeScript: функция смотрит на запрос и либо отвечает сама, либо пропускает его к маршруту. Настоящий Proxy устроен так же, только запрос и ответ приходят от Next.js.

```typescript run
// Модель Proxy: функция смотрит на запрос до маршрутов и либо отвечает сама, либо пропускает дальше.
type Decision = Response | "pass";

function proxyModel(request: Request): Decision {
  const url = new URL(request.url);
  if (url.pathname.startsWith("/old-blog/")) {
    const target = new URL(url);
    target.pathname = url.pathname.replace("/old-blog/", "/blog/");
    return Response.redirect(target, 308);
  }
  return "pass";
}

function route(request: Request): string {
  const decision = proxyModel(request);
  if (decision === "pass") {
    return `маршрут отрисовал ${new URL(request.url).pathname}`;
  }
  return `Proxy ответил ${decision.status}, Location: ${decision.headers.get("location")}`;
}

console.log(route(new Request("http://site.test/old-blog/hello?utm=mail")));
console.log(route(new Request("http://site.test/blog/hello")));
```
```output
Proxy ответил 308, Location: http://site.test/blog/hello?utm=mail
маршрут отрисовал /blog/hello
```

Разметка `Response.redirect(target, 308)` в модели - стандартный Fetch API, на нём построены и `NextRequest`, и `NextResponse`. Параметры запроса (`?utm=mail`) переехали вместе с адресом, потому что мы скопировали весь `URL`, а не собрали новый.

<!-- section: precise -->
## Что именно умеет Proxy

**Где лежит файл.** Один `proxy.ts` на проект: в корне или в `src/`, рядом с каталогом `app`. Он работает в среде Node.js; параметр `runtime` в этом файле не допускается и вызывает ошибку. Старый `middleware.ts` ещё работает, но объявлен устаревшим и нужен лишь тем, кто хочет среду edge. Для переноса есть кодмод: `npx @next/codemod@latest middleware-to-proxy .`.

**Что получает функция.** `NextRequest` - расширение стандартного `Request`: у него есть `request.nextUrl` (адрес с удобными полями `pathname`, `search`, `searchParams`, метод `clone()`), `request.cookies` (методы `get`, `has`, `set`) и обычные `request.headers`. Результат - `NextResponse` или обычный `Response`. Если функция ничего не вернула, запрос идёт дальше.

**Четыре исхода:**

| Вызов | Что происходит |
| --- | --- |
| `NextResponse.next()` | запрос продолжает путь к маршруту |
| `NextResponse.redirect(url, status?)` | браузер получает перенаправление и идёт по новому адресу; адрес в строке меняется |
| `NextResponse.rewrite(url)` | страница берётся с другого адреса, а в строке браузера остаётся прежний |
| `Response.json(...)` или другой `Response` | Proxy отвечает сам, маршрут не запускается |

`NextResponse.redirect` без второго аргумента отвечает кодом 307 (временное перенаправление, метод запроса сохраняется); для переезда навсегда передают 308.

**Когда Proxy вызывается.** Порядок такой: заголовки и перенаправления из `next.config`, затем Proxy, затем `rewrites` типа `beforeFiles`, затем файловые маршруты (`public/`, `app/`), затем остальные `rewrites` и динамические маршруты. Если Proxy перенаправил запрос, дальнейшие этапы не выполняются.

**Matcher** - список шаблонов адресов в `config.matcher`. Правила шаблона:

* начинается с `/`;
* `:name` - один сегмент: `/about/:path` подходит для `/about/a`, но не для `/about/a/c`;
* модификаторы: `*` - ноль или больше, `?` - ноль или один, `+` - один или больше; поэтому `/dashboard/:path*` подходит и для `/dashboard`, и для `/dashboard/team/7`;
* шаблон привязан к началу адреса и не цепляет соседей: `/dashboard/:path*` не совпадёт с `/dashboards`;
* значения должны быть константами: их читают при сборке, а переменные игнорируются.

```typescript run
// Модель matcher: "/dashboard/:path*" значит "/dashboard" и всё, что глубже.
function toRegExp(pattern: string): RegExp {
  // "/dashboard/:path*" -> "^/dashboard(?:/.*)?$": шаблон привязан к началу и концу пути
  return new RegExp(`^${pattern.replace("/:path*", "(?:/.*)?")}$`);
}

function doesMatch(patterns: string[], path: string): boolean {
  return patterns.some((pattern) => toRegExp(pattern).test(path));
}

const matcher = ["/dashboard/:path*", "/docs/:path*"];
for (const path of ["/dashboard", "/dashboard/team/7", "/dashboards", "/docs/ru/intro", "/api/dashboard", "/"]) {
  console.log(`${path.padEnd(18)} ${doesMatch(matcher, path)}`);
}
```
```output
/dashboard         true
/dashboard/team/7  true
/dashboards        false
/docs/ru/intro     true
/api/dashboard     false
/                  false
```

Без `matcher` Proxy вызывается для **каждого** запроса, включая `/_next/static/...` и картинки. Поэтому его принято сужать: либо перечислять нужные адреса, как в нашем упражнении, либо исключать служебные регулярным выражением вида `'/((?!api|_next/static|_next/image|favicon.ico).*)'`.

**Заголовки запроса и ответа - разные.** Чтобы страница увидела новый заголовок, передают его как заголовок **запроса**: `NextResponse.next({ request: { headers } })`, где `headers` - копия `request.headers` с вашими изменениями. Запись `NextResponse.next({ headers })` без `request` отправит заголовки браузеру, а не маршруту. Заголовок ответа ставят так: `response.headers.set(name, value)`.

```typescript run
// Заголовки запроса и ответа - два разных набора. Proxy может менять оба.
const incoming = new Headers({ "x-locale": "evil", accept: "text/html" });

// Заголовки запроса: их увидит страница, клиент не увидит.
const forwarded = new Headers(incoming);
forwarded.set("x-locale", "ru");

// Заголовки ответа: их получит браузер.
const outgoing = new Headers();
outgoing.set("x-request-path", "/docs/intro");

console.log("пришло от клиента:", incoming.get("x-locale"));
console.log("увидит страница: ", forwarded.get("x-locale"));
console.log("accept сохранён:  ", forwarded.get("accept"));
console.log("увидит браузер:   ", outgoing.get("x-locale"), outgoing.get("x-request-path"));
```
```output
пришло от клиента: evil
увидит страница:  ru
accept сохранён:   text/html
увидит браузер:    null /docs/intro
```

<!-- section: examples -->
## Три типичные задачи

**1. Защита раздела по cookie.** Proxy видит каждый запрос и читает только cookie, без обращения к базе данных: он вызывается и для предзагрузки ссылок, поэтому медленная проверка замедлит весь сайт. Такая проверка называется **оптимистичной**: она отсекает запросы без сессии и экономит работу, но не заменяет настоящую проверку прав.

```ts
// proxy.ts
export function proxy(request: NextRequest) {
  const session = request.cookies.get("session")?.value;
  if (!session) {
    const login = new URL("/login", request.url);
    login.searchParams.set("from", request.nextUrl.pathname + request.nextUrl.search);
    return NextResponse.redirect(login);
  }
  return NextResponse.next();
}

export const config = { matcher: "/dashboard/:path*" };
```

Адрес возврата кладут через `searchParams.set`: он сам закодирует символы `/`, `?` и `&`, и значение не сломает остальную строку запроса.

**2. Язык документации.** Переписывание нужно, когда адрес для пользователя остаётся коротким, а страницы лежат в каталогах по языкам:

```ts
// proxy.ts
const locale = request.cookies.get("NEXT_LOCALE")?.value === "ru" ? "ru" : "en";
const target = request.nextUrl.clone();
target.pathname = `/docs/${locale}${request.nextUrl.pathname.slice("/docs".length)}`;
return NextResponse.rewrite(target);
```

Значение cookie приходит от клиента, поэтому его сравнивают со списком допустимых языков, а не подставляют в путь как есть.

**3. Передача данных странице.** Proxy не вызывает страницу напрямую, поэтому данные передают через заголовки запроса, cookie, адрес, перенаправление или переписывание. Страница читает заголовок через `headers()`:

```tsx
// app/docs/[locale]/page.tsx
import { headers } from "next/headers";

export default async function Page() {
  const locale = (await headers()).get("x-locale");
  return <p>Язык: {locale}</p>;
}
```

Сравним три исхода на модели: что видит пользователь после перенаправления и после переписывания.

```typescript run
// Три исхода Proxy и то, что при каждом видит пользователь.
type Outcome =
  | { kind: "next" }
  | { kind: "redirect"; to: string }
  | { kind: "rewrite"; to: string };

function decide(path: string, hasSession: boolean): Outcome {
  if (path.startsWith("/dashboard") && !hasSession) {
    return { kind: "redirect", to: `/login?from=${encodeURIComponent(path)}` };
  }
  if (path === "/docs") {
    return { kind: "rewrite", to: "/docs/ru" };
  }
  return { kind: "next" };
}

function describeOutcome(path: string, hasSession: boolean): string {
  const outcome = decide(path, hasSession);
  switch (outcome.kind) {
    case "redirect":
      return `${path}: браузер идёт на ${outcome.to}, адрес в строке меняется`;
    case "rewrite":
      return `${path}: в строке остаётся ${path}, страницу берут из ${outcome.to}`;
    case "next":
      return `${path}: запрос идёт в маршрут без изменений`;
  }
}

console.log(describeOutcome("/dashboard/settings", false));
console.log(describeOutcome("/docs", false));
console.log(describeOutcome("/dashboard/settings", true));
```
```output
/dashboard/settings: браузер идёт на /login?from=%2Fdashboard%2Fsettings, адрес в строке меняется
/docs: в строке остаётся /docs, страницу берут из /docs/ru
/dashboard/settings: запрос идёт в маршрут без изменений
```

Подробнее о чтении `headers()` и `cookies()` - в уроке про [серверные компоненты](lesson:nextjs.fundamentals.server-components), а о том, как данные из запроса влияют на кэш, - в уроке про [кэширование](lesson:nextjs.fundamentals.caching).

<!-- section: mistakes -->
## Типичные ошибки

* **Считать Proxy единственной защитой.** Он делает оптимистичную проверку по cookie. Права на чтение данных и выполнение действий проверяют там, где эти данные берут. Особенно важно для [серверных действий](lesson:nextjs.fundamentals.server-actions): это POST-запросы к маршруту, где они используются, и если matcher исключает этот путь, Proxy на них не сработает. Проверка внутри действия работает всегда.
* **Ходить в базу данных или во внешний API из Proxy.** Он вызывается на каждый запрос, включая предзагрузку ссылок. Тяжёлая работа здесь замедляет весь сайт. Читайте cookie и заголовки, остальное оставьте маршрутам.
* **Забыть `matcher`.** Без него Proxy обрабатывает статические файлы и картинки. Сузьте его до нужных адресов.
* **Перепутать redirect и rewrite.** Перенаправление меняет адрес в строке браузера и подходит для переезда страниц и входа. Переписывание скрывает внутренний адрес. Для старых ссылок нужен redirect с кодом 308, иначе поисковики и закладки не обновятся.
* **Передать заголовок через `NextResponse.next({ headers })`.** Он уйдёт браузеру, а страница его не увидит. Для страницы нужен `request: { headers }`.
* **Доверять входящему заголовку.** Клиент может прислать любой `x-locale`. Устанавливайте заголовок через `set`: он заменяет присланное значение, а не добавляет рядом.
* **Построить matcher из переменной.** Значения должны быть константами, иначе они игнорируются, и Proxy не сработает там, где вы ожидали.
* **Завести рядом `proxy.ts` и `middleware.ts`.** Остаётся один файл; для переноса используйте кодмод.

<!-- section: summary -->
## Итоги

* Proxy - функция в `proxy.ts`, которая выполняется на сервере до маршрутов и видит запрос целиком; в Next.js 16 она заменила `middleware.ts` и работает в среде Node.js.
* Исходы: `next()` пропускает, `redirect()` меняет адрес в браузере, `rewrite()` подставляет страницу с другого адреса, а обычный `Response` отвечает без маршрута.
* `config.matcher` сужает вызовы: шаблон `/dashboard/:path*` подходит для `/dashboard` и любой вложенности, но не для `/dashboards`.
* Данные странице передают заголовками запроса: `NextResponse.next({ request: { headers } })`; заголовки ответа идут браузеру.
* Proxy делает быстрые оптимистичные проверки по cookie. Настоящую авторизацию проверяют в маршрутах и серверных действиях.

<!-- section: programmer -->
## Если вы знаете другие фреймворки

Proxy - то же самое, что middleware в Express или Koa, с двумя отличиями. Во-первых, цепочки из нескольких функций нет: файл один, а ветвление по адресам вы делаете сами или через `matcher`. Во-вторых, вместо вызова `next()` вы возвращаете объект: `NextResponse.next()`, `redirect()` или `rewrite()`. Если вы работали с Pages Router или Next.js 15, меняется только имя: `middleware.ts` стал `proxy.ts`, а функция `middleware` - `proxy`; поведение, `NextRequest` и `NextResponse` те же. От дополнительных `rewrites` в `next.config` Proxy отличается тем, что может выполнять произвольный код и читать cookie и заголовки.
