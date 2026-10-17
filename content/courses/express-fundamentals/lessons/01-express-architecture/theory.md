<!-- section: simple -->
## Express: тонкий слой над node:http

В курсе по Node.js мы писали обработчик `(req, res)` для `http.createServer` и вручную выбирали маршрут по методу и пути, разбирали адрес, формировали заголовки ([основы HTTP в Node.js](lesson:nodejs.fundamentals.http-fundamentals)). **Express** делает ровно то же самое, но избавляет от рутины: он даёт

* **маршрутизацию**: `app.get("/users/:id", handler)` вместо цепочки `if (req.method === "GET" && ...)`;
* **удобные объекты**: у `req` появляются `params`, `query`, `get()`, у `res` - `json()`, `status()`, `redirect()`;
* **цепочку обработчиков** (middleware): каждый шаг может обработать запрос, изменить его или передать дальше;
* **единое место для ошибок**.

Главное, что нужно понять сразу: **приложение Express - это просто функция `(req, res)`**. Её можно отдать в `http.createServer(app)`, а можно (и в этом курсе мы так делаем) вызвать на искусственном запросе без сети. Порт открывает только `app.listen(...)` - это всего лишь короткая запись `http.createServer(app).listen(...)`.

```javascript
const express = require("express");
const app = express();

app.get("/hello/:name", (req, res) => {
  res.json({ greeting: `Hello, ${req.params.name}!` });
});

app.listen(3000); // здесь открывается порт; в упражнениях курса его нет
```

Express **не** навязывает структуру проекта, базу данных или шаблонизатор. Он минималистичен: всё остальное - ваши модули или подключаемые пакеты. Поэтому в курсе мы собираем недостающее (проверку данных, заголовки безопасности, ограничение частоты) своими руками, чтобы понять, что именно делают готовые библиотеки.

Как проверять такое приложение, если порта нет? Курс использует `light-my-request`: он создаёт искусственные `req` и `res`, вызывает приложение и возвращает собранный ответ (`statusCode`, `headers`, `body`, `json()`). Это быстро, детерминированно и работает в песочнице без сети. В седьмом уроке мы построим на этом набор тестов.

<!-- section: precise -->
## Устройство Express 5

**Приложение и запрос.** `express()` возвращает функцию-приложение с методами `get`, `post`, `put`, `delete`, `use`, `route`, `all`, `set`, `listen` и другими. На каждый запрос Express (1) заменяет прототипы `req` и `res` на свои, расширяющие `http.IncomingMessage` и `http.ServerResponse`, и (2) прогоняет запрос по **стеку** зарегистрированных обработчиков в порядке регистрации. Первый обработчик, который завершил ответ (`res.json`, `res.send`, `res.end`), останавливает цепочку. Если ни один не ответил, срабатывает встроенный финальный обработчик: он отдаёт HTML-страницу `404 Cannot GET /путь`.

**Маршрут** - это метод HTTP, шаблон пути и один или несколько обработчиков. `app.get(path, fn)` реагирует на `GET` (и на `HEAD`, если нет отдельного обработчика). Express **не отдаёт 405**: неизвестный метод для известного пути - просто «не совпало», то есть 404. Если нужен `405` с заголовком `Allow`, его строят сами (урок про REST API).

**Параметры пути.** `/users/:id` кладёт значение в `req.params.id` (уже декодированное из `%XX`). **Строка запроса** разбирается в `req.query`: по умолчанию в Express 5 используется простой разбор (`querystring`): `?tag=a&tag=b` даёт массив `["a","b"]`, а `?tag=a` - строку `"a"`; вложенность `a[b]=1` не разворачивается. Значения всегда строки или массивы строк: число из них нужно получать явно.

**Что изменилось в Express 5** (сверено с руководством по миграции):

* **Синтаксис путей** разбирает path-to-regexp 8. Подстановка получает имя: вместо `/*` пишут `/*splat`, а `req.params.splat` - **массив** сегментов; чтобы совпасть ещё и с корнем, `/{*splat}`. Необязательная часть берётся в фигурные скобки: `/:file{.:ext}` вместо `/:file.:ext?`. Регулярные выражения внутри строки пути не поддерживаются, символы `()[]?+!` зарезервированы.
* **`req.params` без совпавших частей их не содержит** (в Express 4 были пустые строки и `undefined`); объект создан без прототипа.
* **Асинхронные обработчики**: если обработчик возвращает промис и тот отклоняется, значение передаётся как `next(err)`. В Express 4 такой запрос просто повисал.
* **`req.body` равен `undefined`**, пока нет парсера тела (в Express 4 был `{}`).
* **`req.query`** стал геттером (его нельзя присвоить), парсер по умолчанию - простой.
* **Удалены** `app.del()`, `req.param(name)`, форма `res.json(obj, status)` и `res.send(status)`: статус задают цепочкой `res.status(201).json(obj)`.

**Ответы.** `res.json(obj)` сериализует и ставит `Content-Type: application/json; charset=utf-8`; `res.send(x)` выбирает тип по значению (строка - `text/html`, объект - JSON, `Buffer` - `application/octet-stream`); `res.status(code)` принимает целое число 100-999; `res.sendStatus(code)` отправляет код и его текст; `res.set(name, value)` ставит заголовок. Ответ нужно **завершить ровно один раз**: вторая попытка после отправки даёт ошибку `ERR_HTTP_HEADERS_SENT`.

**Заголовок `X-Powered-By: Express`** добавляется по умолчанию и раскрывает используемый фреймворк; его отключают через `app.disable("x-powered-by")` (подробнее в уроке про безопасность).

<!-- section: examples -->
## Примеры

Приложение - функция, а запрос можно подать без сети. Здесь Express получает искусственный запрос от `light-my-request`:

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.get("/hello/:name", (req, res) => {
  res.json({ greeting: `Hello, ${req.params.name}!`, query: req.query });
});

console.log(typeof app, app.length);

(async () => {
  const res = await inject(app, { method: "GET", url: "/hello/%D0%90%D0%BD%D0%BD%D0%B0?tag=a&tag=b&n=1" });
  console.log(res.statusCode, res.headers["content-type"]);
  console.log(res.headers["x-powered-by"]);
  console.log(res.json());
  const missing = await inject(app, { method: "GET", url: "/nowhere" });
  console.log(missing.statusCode, missing.headers["content-type"]);
})();
```
```output
function 3
200 application/json; charset=utf-8
Express
{ greeting: 'Hello, Анна!', query: { tag: [ 'a', 'b' ], n: '1' } }
404 text/html; charset=utf-8
```

Синтаксис путей Express 5: необязательная часть в скобках, именованная подстановка, несовпавшие части отсутствуют в `req.params`:

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.get("/files/:name{.:ext}", (req, res) => res.json(req.params));
app.get("/static/*splat", (req, res) => res.json(req.params));
app.get("/users/:id", (req, res) => res.json({ id: req.params.id, type: typeof req.params.id }));

(async () => {
  for (const url of ["/files/report.pdf", "/files/readme", "/static/css/app.css", "/static", "/users/42", "/users/a%2Fb"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(url.padEnd(20), res.statusCode, res.statusCode === 200 ? res.body : "-");
  }
})();
```
```output
/files/report.pdf    200 {"name":"report","ext":"pdf"}
/files/readme        200 {"name":"readme"}
/static/css/app.css  200 {"splat":["css","app.css"]}
/static              404 -
/users/42            200 {"id":"42","type":"string"}
/users/a%2Fb         200 {"id":"a/b","type":"string"}
```

Ошибка в асинхронном обработчике теперь не теряется. Здесь её перехватывает обычное middleware ошибок (подробно в уроке про обработку ошибок):

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.get("/async", async (req, res) => {
  await Promise.resolve();
  throw new Error("boom");
});
app.use((err, req, res, next) => {
  res.status(500).json({ caught: err.message });
});

inject(app, { method: "GET", url: "/async" }).then((res) => console.log(res.statusCode, res.body));
```
```output
500 {"caught":"boom"}
```

Приложение Express и `http.createServer` - одно и то же устройство: сервер просто вызывает функцию, порт не открыт, пока нет `listen`:

```javascript run
const http = require("node:http");
const express = require("express");

const app = express();
const server = http.createServer(app);
console.log(server.listening, typeof server.listen, app.enabled("x-powered-by"));
```
```output
false function true
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать 405.** Express не различает «пути нет» и «метод не подходит»: оба случая дают 404. Если контракту нужен 405 с `Allow`, пишите его явно.
* **Старый синтаксис путей.** `app.get("/*", ...)` и `"/:id?"` из Express 4 в Express 5 выбрасывают ошибку при регистрации: нужны `/*splat` и `/:id{...}`.
* **Считать, что `req.query.x` - строка.** При `?x=1&x=2` это массив, при `?x[a]=1` - ключ `"x[a]"`. Проверяйте тип, прежде чем использовать значение (в упражнении это нужно для `lang`).
* **Брать число из `req.query` без проверки.** `Number("")` равно `0`, `Number(undefined)` - `NaN`, `Number(["1","2"])` - `NaN`: пустая строка выглядит как корректный ноль.
* **Обращаться к `obj[req.query.key]` напрямую.** Ключ `constructor` или `__proto__` вернёт служебное значение. Сверяйте через `Object.hasOwn` или `Map`.
* **Два ответа на один запрос.** `res.json(...)` без `return` после раннего ответа приводит к `ERR_HTTP_HEADERS_SENT`. Пишите `return res.status(400).json(...)`.
* **`res.json(obj, 201)` и `res.send(404)`** из Express 4 в Express 5 не работают: `res.status(201).json(obj)`, `res.sendStatus(404)`.
* **Открывать порт в тесте.** Приложение удобнее проверять вызовом как функции: так тесты не зависят от свободного порта и не «подвисают».

<!-- section: summary -->
## Итоги

* Express 5 - тонкий слой над `node:http`: приложение - функция `(req, res)`, порт открывает только `listen`.
* Запрос проходит по стеку обработчиков в порядке регистрации; первый ответивший останавливает цепочку, без ответа - 404 от встроенного обработчика.
* `req.params` (путь), `req.query` (строка запроса) всегда содержат строки или массивы строк; их нужно проверять и приводить к нужным типам.
* Express 5: `/*splat`, `{...}` для необязательных частей, async-обработчики с автоматической передачей ошибок, `req.body === undefined` без парсера.
* Ответ завершают ровно один раз: `return res.status(...).json(...)`.
* Express не различает 404 и 405; приложение удобно проверять без сети через `light-my-request`.
