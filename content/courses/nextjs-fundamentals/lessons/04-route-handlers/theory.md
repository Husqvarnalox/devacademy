<!-- section: simple -->
## Адрес, который отвечает данными

До сих пор каждый адрес в приложении возвращал страницу. Но сайту нужны и адреса другого рода: отдать JSON для мобильного приложения, принять данные от стороннего сервиса, выдать файл. Для этого в App Router есть **обработчики маршрутов** (Route Handlers). Это файл `route.ts` в папке сегмента, который вместо компонента экспортирует **функции с именами HTTP-методов**: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`.

```ts
// app/api/notes/route.ts
export async function GET(request: Request) {
  return Response.json({ notes: [] });
}
```

Запрос по адресу `/api/notes` методом `GET` вызовет эту функцию, а её результат станет HTTP-ответом. Если запрос придёт другим методом, например `DELETE`, а такой функции в файле нет, Next.js сам вернёт ответ `405 Method Not Allowed`.

Обработчик построен на стандартных веб-объектах `Request` и `Response`: те же классы есть в браузере (`fetch`) и в Node.js. Значит, вся логика обработчика - это функция от запроса к ответу, и её можно записать без Next.js:

```typescript run
const notes = [
  { id: "1", title: "Покупки" },
  { id: "2", title: "Идеи для урока" },
];

async function GET(request: Request): Promise<Response> {
  const { searchParams } = new URL(request.url);
  const query = (searchParams.get("q") ?? "").toLowerCase();
  const found = notes.filter((note) => note.title.toLowerCase().includes(query));
  return Response.json({ notes: found, total: found.length });
}

async function main() {
  const all = await GET(new Request("http://localhost/api/notes"));
  console.log(all.status, await all.text());

  const filtered = await GET(new Request("http://localhost/api/notes?q=" + encodeURIComponent("ИДЕИ")));
  console.log(filtered.status, filtered.headers.get("content-type"));
  console.log(JSON.stringify(await filtered.json()));
}
main();
```
```output
200 {"notes":[{"id":"1","title":"Покупки"},{"id":"2","title":"Идеи для урока"}],"total":2}
200 application/json
{"notes":[{"id":"2","title":"Идеи для урока"}],"total":1}
```

Ответ `Response.json(...)` сам выставляет заголовок `Content-Type: application/json`. Эту же функцию вызывают тесты упражнения: обработчик не нужно «запускать на порту», достаточно создать запрос и получить ответ.

<!-- section: examples -->
## Примеры

**Расширенные запрос и ответ.** Next.js дополняет стандартные классы: `NextRequest` и `NextResponse` из `next/server`.

```ts
// app/api/notes/route.ts
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
  const q = request.nextUrl.searchParams.get("q"); // параметры запроса
  const theme = request.cookies.get("theme");      // cookie запроса
  return NextResponse.json({ q, theme: theme?.value ?? null });
}

export async function POST(request: NextRequest) {
  const body = await request.json();               // разбор тела запроса
  return NextResponse.json(body, { status: 201, headers: { Location: "/api/notes/4" } });
}
```

`NextResponse.json(данные, { status, headers })` строит JSON-ответ с нужным статусом и заголовками; `NextResponse.redirect(url)` - ответ-перенаправление.

**Динамический сегмент.** Для `app/api/notes/[id]/route.ts` параметры приходят вторым аргументом, как и в страницах, - в виде промиса.

```ts
// app/api/notes/[id]/route.ts
import { NextRequest, NextResponse } from "next/server";

export async function GET(_request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const note = await findNote(id);
  if (!note) {
    return NextResponse.json({ error: "Заметка не найдена" }, { status: 404 });
  }
  return NextResponse.json(note);
}
```

Тот же тип можно записать вспомогательным типом `RouteContext`: `ctx: RouteContext<'/api/notes/[id]'>`. Он доступен глобально, без импорта, а типы для него генерируются при `next dev`, `next build` или `next typegen`.

**Ответ без тела.** После успешного удаления принято отвечать кодом 204 No Content: тело не нужно.

```ts
export async function DELETE(_request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const removed = await removeNote(id);
  return removed ? new NextResponse(null, { status: 204 }) : NextResponse.json({ error: "Не найдено" }, { status: 404 });
}
```

Выбор обработчика по методу тоже можно смоделировать: экспортированные функции - это таблица «метод - функция», а остальные методы получают 405.

```typescript run
type Handler = (request: Request) => Promise<Response>;

const routeModule: Record<string, Handler> = {
  GET: async () => Response.json({ ok: true }),
  POST: async (request) => {
    const type = request.headers.get("content-type") ?? "";
    if (!type.includes("application/json")) {
      return Response.json({ error: "Ожидается JSON" }, { status: 415 });
    }
    return Response.json(await request.json(), { status: 201 });
  },
};

async function dispatch(request: Request): Promise<Response> {
  const handler = routeModule[request.method];
  return handler ? handler(request) : new Response(null, { status: 405 });
}

async function main() {
  const base = "http://localhost/api/items";
  const json = { "content-type": "application/json" };
  const requests = [
    new Request(base),
    new Request(base, { method: "POST", headers: json, body: JSON.stringify({ name: "чай" }) }),
    new Request(base, { method: "POST", body: "name=чай" }),
    new Request(base, { method: "DELETE" }),
  ];
  for (const request of requests) {
    const response = await dispatch(request);
    console.log(request.method, response.status);
  }
}
main();
```
```output
GET 200
POST 201
POST 415
DELETE 405
```

В настоящем Next.js этот выбор выполняет сам фреймворк. Для `OPTIONS` без вашего обработчика Next.js тоже отвечает сам и выставляет заголовок `Allow` по тем методам, которые вы определили.

<!-- section: precise -->
## Как это работает точно

**Соглашение.** Обработчики лежат в файле `route.js` или `route.ts` внутри `app/`. Файл можно вкладывать на любую глубину, как `page` и `layout`. Файл `route` и `page` в одном сегменте не уживаются: оба забирают все HTTP-методы этого адреса, поэтому `app/page.js` вместе с `app/route.js` - конфликт, а `app/page.js` вместе с `app/api/route.js` - допустимая пара. Маршруты не участвуют в макетах и клиентской навигации.

**Методы.** Поддерживаются `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD` и `OPTIONS`. Если метод не поддерживается, Next.js возвращает 405.

**Кэширование.** Обработчики по умолчанию не кэшируются. Для `GET` кэширование включают явно: например, `export const dynamic = 'force-static'`. Остальные методы не кэшируются никогда, даже если лежат в одном файле с кэшируемым `GET`. Если в проекте включены Cache Components, `GET`-обработчики работают по тем же правилам, что и обычные страницы: выполняются при запросе по умолчанию, могут быть подготовлены заранее, если не обращаются к данным запроса или к некэшированным данным, а для включения таких данных в статический ответ используют `use cache`. Директиву нельзя поставить прямо в тело обработчика: её выносят во вспомогательную функцию. Подробно кэширование разбирается в [уроке про кэширование](lesson:nextjs.fundamentals.caching).

**Статусы ответов.** Выбор кода - часть контракта API. Для упражнения важны такие коды:

| Код | Когда |
| --- | --- |
| 200 | успешный `GET` или `PATCH` с телом |
| 201 | запись создана; в заголовке `Location` адрес новой записи |
| 204 | успех без тела (обычно `DELETE`) |
| 400 | запрос разобран, но данные неверны: нет поля, неверный тип, число вне диапазона |
| 404 | запись с таким идентификатором не найдена |
| 415 | тип содержимого (`Content-Type`) не поддерживается |

**Порядок проверок.** Типичный порядок: сначала существует ли ресурс (404), затем допустим ли формат запроса (415), затем разобрать тело (400 при неверном JSON), затем проверить значения (400). Порядок важен клиенту: тот, кто обращается к несуществующей записи, получает 404 независимо от тела запроса. Он закреплён в условии упражнения.

**Тело запроса.** `await request.json()` бросает исключение, если тело не является корректным JSON, поэтому вызов оборачивают в `try/catch` и отвечают 400. Результатом `json()` может оказаться что угодно: массив, `null`, строка или число. Поэтому после разбора проверяют, что это обычный объект, а поля - нужных типов.

**Публичная поверхность.** Обработчик маршрута - публичный адрес: любой клиент может отправить любой запрос и любое тело. Никакие проверки на стороне интерфейса его не защищают, поэтому валидацию и проверку прав выполняют внутри обработчика.

<!-- section: mistakes -->
## Типичные ошибки

* **`route.ts` и `page.tsx` в одной папке.** Оба файла хотят обслуживать один адрес, и Next.js сообщит о конфликте. API выносят в отдельный сегмент, например `app/api/...`.
* **Параметры без `await`.** `params` во втором аргументе - промис, как и в страницах: `params.id` без `await` даст `undefined`.
* **Доверие к телу запроса.** `const { title } = await request.json()` без проверок упадёт на `null` или массиве и пропустит число вместо строки. Проверяйте, что тело - объект, а поля имеют нужный тип.
* **Статус 200 для всего.** Ответ `{ error: "..." }` со статусом 200 заставляет клиента разбирать тело, чтобы понять, что случилось. Ошибка клиента - 4xx, и тело только поясняет её.
* **Тело в ответе 204.** Код 204 означает «содержимого нет»: тело ему не передают. Для JSON-ответа используйте 200.
* **Сравнение с учётом регистра при поиске.** Для поиска по тексту приводите обе стороны к одному регистру. Для кириллицы `toLowerCase()` работает.
* **Ожидание, что `GET` кэшируется.** Без явной настройки обработчик выполняется на каждый запрос.
* **Забытый `await` перед `request.json()`.** Без него в переменной оказывается промис, а не данные.

<!-- section: summary -->
## Итоги

* `route.ts` экспортирует функции с именами HTTP-методов; для остальных методов Next.js отвечает 405.
* Обработчики работают с `Request` и `Response`; `NextRequest` и `NextResponse` добавляют `nextUrl.searchParams`, `cookies` и удобные методы `json`, `redirect`.
* Параметры динамического сегмента приходят вторым аргументом промисом: `{ params }: { params: Promise<{ id: string }> }`.
* `route` и `page` не могут находиться в одном сегменте.
* `GET` по умолчанию не кэшируется; остальные методы не кэшируются никогда.
* Корректные коды ответов (200, 201, 204, 400, 404, 415) и проверка входных данных - часть контракта API.
* Обработчик - публичный адрес, поэтому данные из запроса проверяют внутри него.

<!-- section: programmer -->
## Если вы приходите из другого фреймворка

* Route Handlers заменяют API Routes из Pages Router (`pages/api`). Вместо одной функции `handler(req, res)` с проверкой `req.method` здесь отдельная функция на каждый метод, а результат возвращается значением, а не записывается в `res`.
* По устройству это знакомые контроллеры Express или Fastify, но на стандартных `Request` и `Response`.
* Отдельная серверная часть нужна не всегда. Для изменения данных из форм своего приложения удобнее серверные действия ([следующий урок](lesson:nextjs.fundamentals.server-actions)); обработчики маршрутов выбирают, когда нужен именно HTTP-адрес: публичный API, webhook, выдача файла, доступ из других клиентов.

<!-- section: deep-dive -->
## Подробнее

**Как тестируются обработчики.** Обработчик - обычная функция, поэтому тест создаёт `new NextRequest("http://localhost:3000/api/notes?q=...")`, передаёт его в `GET` и читает ответ: статус, заголовки, `await response.json()`. Для динамических сегментов вторым аргументом передают `{ params: Promise.resolve({ id: "1" }) }`. Сервер и порты не нужны, поэтому такие проверки быстры и воспроизводимы. Тесты этого урока выполняются в среде Node.js, а не jsdom: серверному коду браузерное окружение не нужно.

**Cookies и заголовки.** У `NextRequest` есть `request.cookies` (методы `get`, `getAll`, `has`, `set`, `delete`). В ответе заголовки и cookie выставляют через опции `NextResponse.json(данные, { headers })` и свойство `response.cookies`.

**Когда выбирать обработчик, а когда серверное действие.** Серверное действие вызывается из интерфейса вашего приложения и удобно для форм. Обработчик маршрута вызывается любым HTTP-клиентом и подходит, когда у адреса есть потребители вне вашего приложения.
