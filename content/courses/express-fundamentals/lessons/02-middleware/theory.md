<!-- section: simple -->
## Middleware: конвейер из функций

Запрос в Express не попадает сразу в «свой» обработчик. Он проходит через **цепочку функций** - **middleware**. Каждая функция получает `(req, res, next)` и выбирает одно из трёх:

1. **ответить** (`res.json(...)`, `res.send(...)`, `res.end()`): цепочка на этом заканчивается;
2. **передать дальше**, вызвав `next()`: управление переходит к следующей функции;
3. **сообщить об ошибке**, вызвав `next(err)`: запрос перескакивает к обработчикам ошибок (отдельный урок).

Обычный маршрут `app.get("/x", handler)` - тот же middleware, просто привязанный к методу и пути. Так что вся программа на Express - это стек функций, которые вызываются **в порядке регистрации**:

```javascript
app.use(requestId);        // 1. пометить запрос
app.use(logger);           // 2. записать в журнал
app.use(express.json());   // 3. разобрать тело
app.get("/users", list);   // 4. ответить
```

Поэтому порядок - часть логики. Если поставить `logger` после маршрута, который отвечает, журнал не увидит этот запрос: до него дело просто не дойдёт. Если поставить проверку авторизации после маршрута, маршрут будет открыт всем.

Middleware бывают трёх видов: **свои** (пишем сами - журнал, идентификатор запроса, проверка заголовка), **встроенные** (`express.json()`, `express.urlencoded()`, `express.static()`) и **сторонние** (cors, helmet, и т. д.). В этом уроке мы пишем свои, а встроенные разбираем по смыслу.

Типичная ловушка новичка: забыть `next()`. Тогда запрос «зависает»: ответа нет, а следующая функция не вызвана. Обратная ловушка - вызвать `next()` **после** того, как уже ответили: следующий обработчик тоже попытается ответить, и получится ошибка «заголовки уже отправлены». Поэтому пишут `return res.status(401).json(...)`.

<!-- section: precise -->
## Как Express вызывает цепочку

**Сигнатуры.** Обычное middleware - `(req, res, next)`. Обработчик ошибок отличается ровно тем, что принимает **четыре** аргумента `(err, req, res, next)`; Express определяет вид по числу параметров функции (`fn.length`). Поэтому в обработчике ошибок `next` нужно указывать, даже если он не используется.

**Регистрация.** `app.use(fn)` вызывает `fn` для любого пути; `app.use("/api", fn)` - для путей, **начинающихся** с `/api` (внутри `req.url` обрезан до остатка, а полный адрес остаётся в `req.originalUrl`, префикс - в `req.baseUrl`). `app.get(path, ...fns)`, `app.post(...)` - только для метода и пути целиком; в них можно передать несколько функций через запятую или массив. `app.all(path, fn)` - для любого метода.

**Передача управления.** `next()` - к следующей функции стека. `next("route")` - пропустить оставшиеся функции этого маршрута и перейти к следующему маршруту. `next("router")` - выйти из текущего роутера. `next(err)` с любым другим значением - ошибка. **Асинхронные** функции в Express 5: если вернуть промис и он отклонится, это равносильно `next(err)`; но `next()` внутри колбэков Node.js по-прежнему вызывают вручную.

**Общие данные.** Middleware передают данные дальше, записывая их в `req` (например, `req.user`, `req.id`) или в `res.locals` (область, предназначенную для данных ответа и шаблонов). Имена нужно выбирать так, чтобы не затереть существующие свойства `req`.

**Что происходит после ответа.** Объект `res` - `EventEmitter`: событие `"finish"` наступает, когда ответ полностью передан операционной системе, `"close"` - когда соединение закрыто (в том числе обрыв клиентом). Журналы пишут в `"finish"`, чтобы знать итоговый статус. Заголовок и статус можно прочитать на этом этапе, а изменить уже нельзя.

**Фабрики.** Конфигурируемое middleware пишут как функцию, которая **возвращает** `(req, res, next)`: `requestId({ generate })`. Так настройки (генератор, список токенов, приёмник журнала) подставляются при создании приложения, а не берутся из глобальных переменных. Это же упрощает тесты: можно подставить детерминированный генератор.

**Встроенные middleware Express 5.**

* `express.json()` разбирает тело с `Content-Type: application/json` в `req.body` (по умолчанию лимит 100kb; неверный JSON - ошибка 400; превышение лимита - 413). Если тело не разобрано, `req.body` равен `undefined`.
* `express.urlencoded()` разбирает формы `application/x-www-form-urlencoded`; в Express 5 по умолчанию `extended: false` (простой разбор через `querystring`, без вложенных объектов).
* `express.text()` и `express.raw()` дают строку и `Buffer`. Все четыре поддерживают в запросах `gzip`, `deflate` и `br`.
* `express.static(root)` отдаёт файлы из каталога: подбирает `Content-Type`, ставит `ETag`, `Last-Modified`, `Cache-Control`, понимает `Range` и `If-None-Match`. В Express 5 по умолчанию **не отдаёт** файлы и каталоги, имя которых начинается с точки (`dotfiles: "ignore"`).

**Уровни.** Middleware на уровне приложения (`app.use`), на уровне **роутера** (`router.use` - действует на все запросы, попавшие в этот роутер; тема следующего урока) и на уровне **маршрута** (`app.get("/x", auth, handler)`) отличаются областью действия. Узкая область - надёжнее: авторизацию вешают на роутер защищённой части, а не проверяют вручную в каждом обработчике.

**Безопасность.** Заголовок `X-Request-Id` приходит от клиента: ему нельзя доверять. Его проверяют по шаблону (иначе в журнал попадёт произвольный текст, в том числе переводы строк - «подделка журнала») и заменяют своим. Отказы авторизации различают: `401` - данные не предъявлены или неверны по форме (нужен заголовок `WWW-Authenticate`), `403` - предъявлены, но доступа нет ([контроль доступа](lesson:security.fundamentals.access-control)).

<!-- section: examples -->
## Примеры

Порядок вызовов: «внешнее» middleware видит запрос до обработчика и ответ после него, если дождаться `"finish"`.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const trace = [];
const app = express();
app.use((req, res, next) => { trace.push("A: до"); res.on("finish", () => trace.push("A: ответ отправлен")); next(); });
app.use((req, res, next) => { trace.push("B: до"); next(); });
app.get("/x", (req, res) => { trace.push("handler"); res.send("ok"); });
app.use((req, res) => { trace.push("не выполнится для /x"); res.status(404).end(); });

inject(app, { method: "GET", url: "/x" }).then((res) => {
  console.log(res.statusCode, res.body);
  console.log(trace);
});
```
```output
200 ok
[ 'A: до', 'B: до', 'handler', 'A: ответ отправлен' ]
```

`next()`, `next("route")` и область `app.use("/api", ...)`: одна и та же функция по-разному видит адрес.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.use("/api", (req, res, next) => {
  req.seen = { url: req.url, baseUrl: req.baseUrl, originalUrl: req.originalUrl };
  next();
});
app.get("/api/items/:id", (req, res, next) => {
  if (req.params.id === "skip") return next("route"); // пропустить остальное в этом маршруте
  res.json({ route: "first", seen: req.seen });
});
app.get("/api/items/:id", (req, res) => res.json({ route: "second", id: req.params.id }));

(async () => {
  for (const url of ["/api/items/7?x=1", "/api/items/skip"]) {
    console.log(JSON.stringify((await inject(app, { method: "GET", url })).json()));
  }
})();
```
```output
{"route":"first","seen":{"url":"/items/7?x=1","baseUrl":"/api","originalUrl":"/api/items/7?x=1"}}
{"route":"second","id":"skip"}
```

Разбор тела: без парсера `req.body` равен `undefined`, а `urlencoded` в Express 5 по умолчанию простой.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const withoutParser = express();
withoutParser.post("/", (req, res) => res.json({ body: req.body === undefined ? "undefined" : req.body }));

const withParsers = express();
withParsers.use(express.json(), express.urlencoded());
withParsers.post("/", (req, res) => res.json({ body: req.body }));

(async () => {
  const send = (app, headers, payload) => inject(app, { method: "POST", url: "/", headers, payload }).then((r) => r.body);
  console.log(await send(withoutParser, { "content-type": "application/json" }, '{"a":1}'));
  console.log(await send(withParsers, { "content-type": "application/json" }, '{"a":1}'));
  console.log(await send(withParsers, { "content-type": "application/x-www-form-urlencoded" }, "a=1&a=2&b[c]=3"));
})();
```
```output
{"body":"undefined"}
{"body":{"a":1}}
{"body":{"a":["1","2"],"b[c]":"3"}}
```

`express.static` отдаёт файлы, но не файлы с точкой в имени (поведение Express 5 по умолчанию):

```javascript run
const fs = require("node:fs");
const path = require("node:path");
const express = require("express");
const inject = require("light-my-request");

const root = fs.mkdtempSync(path.join(process.cwd(), "pub-"));
fs.writeFileSync(path.join(root, "hello.txt"), "hi");
fs.writeFileSync(path.join(root, ".secret"), "hidden");

const app = express();
app.use(express.static(root));

(async () => {
  for (const url of ["/hello.txt", "/.secret", "/missing.txt"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(url.padEnd(14), res.statusCode, res.headers["content-type"] ?? "-");
  }
  fs.rmSync(root, { recursive: true });
})();
```
```output
/hello.txt     200 text/plain; charset=utf-8
/.secret       404 text/html; charset=utf-8
/missing.txt   404 text/html; charset=utf-8
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забытый `next()`.** Запрос зависает без ответа. Каждая ветка функции должна либо ответить, либо вызвать `next`.
* **`next()` после ответа.** Следующий обработчик попытается ответить ещё раз. Пишите `return res.status(401).json(...)`.
* **Неверный порядок.** Журнал после маршрута не видит запрос; `express.json()` после маршрута оставляет `req.body === undefined`; авторизация после защищаемого маршрута не защищает.
* **Доверие к `X-Request-Id`, `Authorization` и другим заголовкам клиента.** Всё, что пришло в запросе, нужно проверять: по шаблону, по списку, по длине.
* **Путать 401 и 403.** `401` - клиент не представился (или токен не разобрать), и нужен заголовок `WWW-Authenticate`; `403` - личность известна (или токен разобран), но доступа нет.
* **Использовать `req.url` в журнале внутри `app.use("/prefix", ...)`.** Префикс отрезан. Полный адрес - `req.originalUrl`.
* **Глобальное состояние в middleware.** Счётчики и списки токенов в переменных модуля делают приложение зависимым от порядка запусков и затрудняют тесты. Передавайте зависимости через фабрику.
* **Удивляться 401 на несуществующих путях.** Защита на уровне роутера срабатывает до выбора маршрута, поэтому `/api/unknown` без токена получает `401`, а не `404`. Это нормально (так не раскрывается, какие пути существуют), но об этом нужно помнить при чтении журнала.

<!-- section: summary -->
## Итоги

* Middleware - функция `(req, res, next)`: ответить, передать дальше через `next()` или сообщить об ошибке через `next(err)`.
* Цепочка выполняется в порядке регистрации; порядок - часть логики (идентификатор, журнал, парсер тела, маршруты).
* Конфигурируемое middleware - фабрика, возвращающая функцию; зависимости передают снаружи.
* `app.use("/p", fn)` обрезает префикс в `req.url`; полный адрес в `req.originalUrl`, префикс в `req.baseUrl`.
* Итоговый статус читают в событии `"finish"` у `res`.
* Встроенные `express.json()`, `express.urlencoded()`, `express.static()` работают как обычные middleware; в Express 5 `req.body` без парсера равен `undefined`, а статические файлы с точкой по умолчанию не отдаются.
* Защиту вешают на роутер защищаемой части; `401` и `403` различают.
