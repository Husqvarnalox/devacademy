<!-- section: simple -->
## Роутеры: мини-приложения внутри приложения

Пока маршрутов пять, их можно держать в одном файле. Когда их пятьдесят, нужна структура. В Express её даёт **роутер** (`express.Router()`): это «мини-приложение» со своим стеком middleware и маршрутов, которое подключается к основному приложению по префиксу:

```javascript
// routes/books.mjs
const router = express.Router();
router.get("/", listBooks);        // GET /api/books
router.get("/:id", getBook);       // GET /api/books/42
export default router;

// app.mjs
app.use("/api/books", booksRouter);
```

Внутри роутера пути пишут **относительно точки подключения**: роутер книг не знает, что он лежит под `/api/books` - завтра его можно смонтировать под `/v2/books`, и код не изменится. Это главное преимущество: модуль отвечает за свою часть, а приложение просто собирает части вместе.

Что обычно выносят в роутер:

* **маршруты одного ресурса** (книги, авторы, заказы);
* **middleware этой области**, например проверку токена только для `/api` или заголовок только для ресурса;
* **обработчики параметров** (`router.param("id", ...)`): общий код «найти книгу по `:id` или ответить 404», который иначе дублировался бы в каждом маршруте.

Структура приложения - не про файлы ради файлов. Полезное правило: **собирайте приложение функцией `createApp(зависимости)`**, а порт открывайте в отдельном файле запуска. Тогда тесты вызывают `createApp` с тестовыми данными, а реальный запуск - с настоящими. Тот же приём работает и для роутеров: `createBooksRouter(store)` получает хранилище как аргумент и не обращается к глобальным переменным.

<!-- section: precise -->
## Router, монтирование и параметры

**Router** - функция-обработчик с методами `get`, `post`, `use`, `route`, `param`, `all` и т. д., как у приложения; создаётся через `express.Router(options)`. Он является middleware: `app.use("/api/books", router)`. Опции: `caseSensitive` (по умолчанию `false`: `/Users` и `/users` одно и то же), `strict` (по умолчанию `false`: `/users` и `/users/` одно и то же), `mergeParams` (см. ниже).

**Монтирование.** При `app.use("/api/books", router)` Express убирает префикс из `req.url`, вызывает роутер и после него восстанавливает адрес. Внутри роутера:

* `req.baseUrl` - точка подключения (`/api/books`);
* `req.path` и `req.url` - относительный остаток (`/42`);
* `req.originalUrl` - полный исходный адрес (`/api/books/42?x=1`);
* `req.route` - сопоставленный маршрут.

Если ни один маршрут роутера не совпал, управление **возвращается** в приложение и идёт к следующему обработчику (например, общему 404). Так же ведёт себя `next("router")`.

**`router.route(path)`** создаёт цепочку для одного пути: `router.route("/:id").get(show).put(replace).delete(remove)`. Путь пишется один раз, а методы перечисляются; `router.route(path).all(fn)` срабатывает для любого метода (удобно для проверок и для ответа `405`).

**Параметры.** `router.param("id", (req, res, next, value) => { ... })` вызывается **один раз на запрос** для каждого маршрута роутера, в пути которого есть `:id`, до самого обработчика. Обычно здесь загружают объект и кладут в `req` (`req.book = ...`) либо отвечают 404 и **не** вызывают `next()`. В Express 5 первым аргументом должна быть строка (массив имён принимает только `app.param`), а сигнатуры `router.param(fn)` больше нет. Обработчик параметров действует только на маршруты **своего** роутера.

**`mergeParams`.** Параметры родительского пути (`/authors/:authorId/books`) по умолчанию вложенному роутеру недоступны: у него свой `req.params`. Опция `express.Router({ mergeParams: true })` объединяет их (при конфликте имён выигрывают параметры дочернего роутера).

**Организация проекта.** Распространённый скелет: `app.mjs` (создаёт и настраивает приложение, экспортирует `createApp`), `server.mjs` (вызывает `createApp` и `listen`), каталог `routes/` (по файлу на ресурс), при необходимости `middleware/` и `services/` (бизнес-логика без `req` и `res`). Тонкие обработчики, которые только читают запрос, зовут сервис и пишут ответ, проще тестировать: сервис проверяется обычными функциями.

**Безопасность.** Обработчик параметра - удобное место для проверки формата идентификатора (`/^\d+$/`), ведь значение приходит от клиента. Важно также не добавлять в роутер публичный маршрут «по пути», если он должен быть защищён: защиту вешают на роутер целиком (`router.use(auth)`) **до** маршрутов, и порядок внутри файла имеет значение.

<!-- section: examples -->
## Примеры

Роутер не знает, где смонтирован: один и тот же роутер подключён в два места, а `baseUrl` показывает отличия.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const router = express.Router();
router.get("/ping", (req, res) => res.json({ base: req.baseUrl, path: req.path, original: req.originalUrl }));

const app = express();
app.use("/api", router);
app.use("/v2/service", router);

(async () => {
  for (const url of ["/api/ping?x=1", "/v2/service/ping", "/ping"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(url.padEnd(18), res.statusCode, res.statusCode === 200 ? res.body : "-");
  }
})();
```
```output
/api/ping?x=1      200 {"base":"/api","path":"/ping","original":"/api/ping?x=1"}
/v2/service/ping   200 {"base":"/v2/service","path":"/ping","original":"/v2/service/ping"}
/ping              404 -
```

`router.param` и `router.route`: один обработчик параметра на все маршруты ресурса, два метода на одном пути.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const calls = [];
const items = new Map([["1", { id: "1", title: "Node" }]]);
const router = express.Router();

router.param("id", (req, res, next, value) => {
  calls.push(`param ${value}`);
  const item = items.get(value);
  if (!item) return res.status(404).json({ error: "no such item" });
  req.item = item;
  next();
});

router.route("/:id")
  .get((req, res) => res.json(req.item))
  .delete((req, res) => { items.delete(req.item.id); res.sendStatus(204); });

const app = express();
app.use("/items", router);

(async () => {
  for (const [method, url] of [["GET", "/items/1"], ["GET", "/items/2"], ["DELETE", "/items/1"], ["GET", "/items/1"]]) {
    const res = await inject(app, { method, url });
    console.log(method.padEnd(7), url.padEnd(10), res.statusCode, res.body || "-");
  }
  console.log(calls);
})();
```
```output
GET     /items/1   200 {"id":"1","title":"Node"}
GET     /items/2   404 {"error":"no such item"}
DELETE  /items/1   204 -
GET     /items/1   404 {"error":"no such item"}
[ 'param 1', 'param 2', 'param 1', 'param 1' ]
```

`mergeParams`: без него вложенный роутер не видит параметр родителя.

```javascript run
const express = require("express");
const inject = require("light-my-request");

function nested(options) {
  const child = express.Router(options);
  child.get("/", (req, res) => res.json({ authorId: req.params.authorId ?? null }));
  const parent = express.Router();
  parent.use("/:authorId/books", child);
  const app = express();
  app.use("/authors", parent);
  return app;
}

(async () => {
  console.log((await inject(nested({}), { method: "GET", url: "/authors/7/books" })).body);
  console.log((await inject(nested({ mergeParams: true }), { method: "GET", url: "/authors/7/books" })).body);
})();
```
```output
{"authorId":null}
{"authorId":"7"}
```

Маршрутизация по умолчанию нечувствительна к регистру и к косой черте в конце (для роутера те же опции `caseSensitive` и `strict`):

```javascript run
const express = require("express");
const inject = require("light-my-request");

const loose = express();
loose.get("/users", (req, res) => res.send("users"));
const strict = express();
strict.set("case sensitive routing", true);
strict.set("strict routing", true);
strict.get("/users", (req, res) => res.send("users"));

(async () => {
  for (const url of ["/users", "/Users", "/users/"]) {
    const a = await inject(loose, { method: "GET", url });
    const b = await inject(strict, { method: "GET", url });
    console.log(url.padEnd(8), a.statusCode, b.statusCode);
  }
})();
```
```output
/users   200 200
/Users   200 404
/users/  200 404
```

<!-- section: mistakes -->
## Типичные ошибки

* **Вложенный роутер без `mergeParams`.** `req.params.authorId` внутри `/authors/:authorId/books` будет `undefined`. Включите `mergeParams: true`.
* **Писать в роутере полный путь.** `router.get("/api/books/:id")` под `app.use("/api/books", router)` даст путь `/api/books/api/books/:id`. Внутри роутера пути относительные.
* **Глобальное состояние в модуле роутера.** Массив данных на уровне файла общий для всех приложений и всех тестов. Передавайте хранилище в фабрику `createBooksRouter(store)`.
* **Один `express()` на весь проект с `listen` в `app.mjs`.** Тесты начнут занимать порт, а второй `import` приложения откроет второй сервер. Разделяйте `createApp` и `listen`.
* **Вызвать `next()` после ответа в `router.param`.** Если параметр неверен, ответьте 404 и выйдите (`return res.status(404)...`).
* **Забыть, что защита на `router.use` действует на то, что зарегистрировано после неё.** Публичные маршруты регистрируйте до `router.use(auth)` или в другом роутере.
* **Не проверять формат идентификатора.** `Number("")` и `Number("1e3")` дают числа; проверяйте строку шаблоном `^\d+$`, прежде чем преобразовывать.
* **Дублировать загрузку объекта в каждом обработчике.** Это задача `router.param`; обработчики остаются короткими.

<!-- section: summary -->
## Итоги

* `express.Router()` - мини-приложение; подключается через `app.use(префикс, router)` и пишет пути относительно точки монтирования.
* Внутри роутера: `req.baseUrl` - префикс, `req.path` - остаток, `req.originalUrl` - полный адрес.
* `router.use` задаёт middleware для всего роутера, `router.param` - загрузку объекта по `:id`, `router.route` - цепочку методов на один путь.
* Вложенным роутерам нужен `mergeParams: true`, чтобы видеть параметры родителя.
* Собирайте приложение функцией `createApp(зависимости)`, а `listen` вызывайте в отдельном файле; роутеры получают хранилище через фабрику.
* Формат идентификаторов проверяйте в обработчике параметра: значение приходит от клиента.
