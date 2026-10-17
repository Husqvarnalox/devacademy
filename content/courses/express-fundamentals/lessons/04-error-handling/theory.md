<!-- section: simple -->
## Ошибки: один путь вместо россыпи try/catch

В любом сервере что-то идёт не так: клиент прислал сломанный JSON, записи нет в базе, внешний сервис недоступен, в коде обнаружилась ошибка. Важно не то, что ошибки бывают, а то, **как о них узнаёт клиент**:

* ответ должен иметь верный **статус** (4xx - виноват клиент, 5xx - сервер);
* тело должно быть **одинакового формата** для всех ошибок, чтобы клиентский код разбирал его одним способом;
* внутренности (текст исключения, трассировка, SQL) **нельзя показывать** клиенту;
* серверные ошибки нужно **записать в журнал**, чтобы их можно было найти.

В Express для этого есть единый механизм. Любая ошибка (`throw` в обработчике, отклонённый промис или вызов `next(err)`) пропускает все обычные middleware и попадает в **middleware ошибок** - функцию с **четырьмя** параметрами:

```javascript
app.use((err, req, res, next) => {
  res.status(500).json({ error: { code: "internal_error", message: "Internal Server Error" } });
});
```

Это единственное место, где формируется ответ об ошибке. Обработчики маршрутов не отвечают на ошибки сами: они просто **бросают** осмысленную ошибку (`throw new HttpError(404, "item_not_found", "...")`), а обработчик превращает её в ответ. Код маршрутов становится короче и читается как «счастливый путь».

В Express 5 это работает и для `async`-функций: отклонённый промис автоматически превращается в `next(err)`. Дополнительные обёртки и `try/catch` ради этого не нужны.

Ещё один обработчик нужен для **404**: если ни один маршрут не ответил, запрос доходит до конца стека. Мы ловим его последним обычным middleware и превращаем в ту же ошибку `404`, чтобы ответ имел тот же формат.

<!-- section: precise -->
## Как Express ведёт ошибку

**Как возникает ошибка.** Express считает ошибкой: (1) исключение, брошенное синхронно в middleware или обработчике; (2) отклонённый промис, **возвращённый** из обработчика (Express 5); (3) вызов `next(значение)`, где значение - не `undefined`, не `"route"` и не `"router"`. Если исключение брошено в **колбэке** (`setTimeout`, событие, колбэк старого API), Express его не видит: процесс упадёт с необработанным исключением. В таких местах вызывайте `next(err)` вручную или оберните в промис.

**Поиск обработчика.** После ошибки Express пропускает все обычные middleware и ищет ближайшие **следующие** по порядку регистрации функции с четырьмя параметрами. Поэтому обработчик ошибок подключают **последним**, после всех маршрутов и middleware. Несколько обработчиков образуют цепочку: каждый может ответить или передать ошибку дальше вызовом `next(err)`. Параметр `next` в обработчике ошибок указывают всегда, даже если он не используется: иначе функция получит три параметра и Express посчитает её обычным middleware.

**Встроенный обработчик.** Если ни один пользовательский не ответил, ошибку обрабатывает встроенный (`finalhandler`): статус берётся из `err.status` или `err.statusCode` (если это число 4xx-5xx), иначе 500; тело - HTML. В режиме, отличном от `test`, стек ошибки печатается в `stderr`; в ответ клиенту стек попадает, только если `NODE_ENV` не `production`. Поэтому в production задавайте `NODE_ENV=production` и пишите собственный обработчик.

**`res.headersSent`.** Если часть ответа уже передана (заголовки ушли), поменять статус и тело нельзя. В таком случае обработчик ошибок обязан вызвать `next(err)`: встроенный обработчик закроет соединение и клиент поймёт, что ответ оборван. Попытка вызвать `res.status(500).json(...)` приведёт к новой ошибке `ERR_HTTP_HEADERS_SENT`.

**Единый формат.** Распространённый вариант:

```json
{ "error": { "code": "item_not_found", "message": "Item 42 does not exist", "details": { "id": "42" } } }
```

`code` - стабильная машинная строка (по ней клиент выбирает поведение), `message` - человекочитаемое сообщение (может меняться), `details` - необязательные структурированные подробности (например, список ошибок валидации). Тип `application/problem+json` (RFC 9457) - стандартизованная форма такого же замысла; курс использует простой собственный формат, но идея та же.

**Классы ошибок.** Прикладную ошибку выражают собственным классом (`HttpError` со `status`, `code`, `message`, `details`), наследующим `Error`: так остаётся трассировка, а `instanceof` отличает «ожидаемые» ошибки от неожиданных. **Ожидаемые** (4xx) - часть контракта: их сообщения безопасны для клиента. **Неожиданные** (всё остальное) отдаются как `500 Internal Server Error` без подробностей, а исходная ошибка пишется в журнал.

**Ошибки парсера.** `express.json()` передаёт в обработчик ошибки с полем `type`: `entity.parse.failed` (неверный JSON, статус 400), `entity.too.large` (превышен `limit`, статус 413), `encoding.unsupported`, `charset.unsupported`. По ним обработчик формирует ответ в общем формате.

**Что нельзя в Express 5:** `res.status()` принимает только целые числа от 100 до 999, иначе бросает ошибку; бросать можно что угодно (даже строку), но обработчик должен быть готов к значению, которое не является `Error`.

**Безопасность.** Сообщения исключений часто содержат пути к файлам, строки SQL, имена хостов. Их показ - утечка информации ([инъекции и защита](lesson:security.fundamentals.injection-defences) показывают, во что она превращается). Отличайте то, что можно сказать клиенту, от того, что пишется только в журнал. Не пишите в журнал секреты из тела запроса ([секреты и зависимости](lesson:security.fundamentals.secrets-and-dependencies)).

<!-- section: examples -->
## Примеры

Асинхронная ошибка, синхронная ошибка и вызов `next(err)` - все приходят в один обработчик. Сначала смотрим, во что превращает ошибку **встроенный** обработчик, потом подключаем свой.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.set("env", "test"); // в этом режиме встроенный обработчик не печатает стек в stderr
app.get("/sync", () => { throw new Error("sync"); });
app.get("/status", () => { throw Object.assign(new Error("teapot"), { status: 418 }); });

(async () => {
  for (const url of ["/sync", "/status", "/missing"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(url.padEnd(9), res.statusCode, res.headers["content-type"], res.body.includes("<pre>"));
  }
})();
```
```output
/sync     500 text/html; charset=utf-8 true
/status   418 text/html; charset=utf-8 true
/missing  404 text/html; charset=utf-8 true
```

Единый формат: свой обработчик ошибок и 404 через ту же ошибку.

```javascript run
const express = require("express");
const inject = require("light-my-request");

class HttpError extends Error {
  constructor(status, code, message) { super(message); this.status = status; this.code = code; }
}

const app = express();
app.get("/async", async () => { await null; throw new HttpError(409, "conflict", "Already exists"); });
app.get("/crash", () => { throw new Error("password=hunter2"); });
app.get("/cb", (req, res, next) => setTimeout(() => next(new HttpError(503, "busy", "Try later")), 1));
app.use((req, res, next) => next(new HttpError(404, "not_found", `Cannot ${req.method} ${req.path}`)));
app.use((err, req, res, next) => {
  const known = err instanceof HttpError;
  res.status(known ? err.status : 500).json({
    error: { code: known ? err.code : "internal_error", message: known ? err.message : "Internal Server Error" },
  });
});

(async () => {
  for (const url of ["/async", "/crash", "/cb", "/nope"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(res.statusCode, res.body);
  }
})();
```
```output
409 {"error":{"code":"conflict","message":"Already exists"}}
500 {"error":{"code":"internal_error","message":"Internal Server Error"}}
503 {"error":{"code":"busy","message":"Try later"}}
404 {"error":{"code":"not_found","message":"Cannot GET /nope"}}
```

Ловушка с количеством параметров: функция с тремя параметрами не считается обработчиком ошибок.

```javascript run
const express = require("express");
const inject = require("light-my-request");

function build(handler) {
  const app = express();
  app.set("env", "test");
  app.get("/", () => { throw new Error("boom"); });
  app.use(handler);
  return app;
}

const three = (err, req, res) => res.status(500).json({ custom: true });          // 3 параметра
const four = (err, req, res, next) => res.status(500).json({ custom: true });     // 4 параметра

(async () => {
  for (const handler of [three, four]) {
    const res = await inject(build(handler), { method: "GET", url: "/" });
    console.log(handler.length, res.headers["content-type"].split(";")[0], res.body.slice(0, 20).replace(/\s+/g, " "));
  }
})();
```
```output
3 text/html <!DOCTYPE html> <htm
4 application/json {"custom":true}
```

Ошибки парсера тела несут поле `type`:

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.use(express.json({ limit: "50b" }));
app.post("/", (req, res) => res.json(req.body));
app.use((err, req, res, next) => res.status(err.status).json({ type: err.type, status: err.status }));

(async () => {
  const send = (payload) => inject(app, { method: "POST", url: "/", headers: { "content-type": "application/json" }, payload }).then((r) => r.body);
  console.log(await send("{oops"));
  console.log(await send(JSON.stringify({ text: "x".repeat(80) })));
  console.log(await send('{"ok":1}'));
})();
```
```output
{"type":"entity.parse.failed","status":400}
{"type":"entity.too.large","status":413}
{"ok":1}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Три параметра вместо четырёх.** `(err, req, res)` не обработчик ошибок: Express вызовет его как обычное middleware, и ошибка пройдёт мимо. Всегда пишите `(err, req, res, next)`.
* **Обработчик ошибок подключён раньше маршрутов.** Он увидит только ошибки из того, что стоит до него. Размещайте его последним.
* **Показывать `err.message` и `err.stack` клиенту.** Для неожиданных ошибок отдавайте общее сообщение, а подробности пишите в журнал.
* **Разные форматы ответов.** Часть ответов HTML, часть `{ message }`, часть `{ error: "..." }`: клиентам приходится угадывать. Один формат, один обработчик.
* **Статус 200 с описанием ошибки в теле.** Статус - часть контракта: кэши, прокси и клиентские библиотеки опираются на него.
* **Бросать исключения в колбэках.** `setTimeout(() => { throw ... })` Express не поймает; используйте `next(err)` или промисы.
* **Ловить всё в `catch` и отвечать 500.** Тогда ожидаемые ошибки (404, 409) становятся неотличимыми от поломок. Пусть `catch` ловит только то, что умеет обработать, остальное пробрасывается.
* **Игнорировать `res.headersSent`.** При частично отправленном ответе нужно вызвать `next(err)`, а не пытаться записать новый ответ.
* **Писать в журнал каждый 404.** Ошибки клиента - не повод для тревоги; журнал серверных ошибок (5xx) должен оставаться читаемым.
* **Бросать `throw "строка"`.** Трассировки нет; обработчик не отличит это значение от ожидаемой ошибки. Бросайте экземпляры `Error`.

<!-- section: summary -->
## Итоги

* Ошибку Express получает из `throw`, из отклонённого промиса (Express 5) и из `next(err)`; в колбэках `throw` не ловится.
* Обработчик ошибок - функция из **четырёх** параметров `(err, req, res, next)`, подключённая **последней**.
* 404 - это тоже ошибка: последнее обычное middleware передаёт `HttpError(404, ...)` общему обработчику.
* Ожидаемые ошибки (4xx) выражают классом с `status`, `code`, `message`, `details`; неожиданные становятся `500 Internal Server Error` без подробностей и пишутся в журнал.
* Если `res.headersSent`, обработчик вызывает `next(err)`.
* Формат ошибок один на всё приложение: `{ error: { code, message, details? } }`.
* `express.json()` сообщает о проблемах через `err.type` (`entity.parse.failed`, `entity.too.large`).
