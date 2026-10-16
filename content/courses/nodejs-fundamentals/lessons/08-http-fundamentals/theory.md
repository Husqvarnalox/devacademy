<!-- section: simple -->
## HTTP: текст, который клиент и сервер шлют друг другу

Когда браузер открывает страницу, он отправляет серверу **HTTP-запрос**, а сервер возвращает **HTTP-ответ**. Оба сообщения - обычный текст в строгом формате:

```text
GET /hello?name=Ann HTTP/1.1      <- стартовая строка: метод, цель, версия
Host: example.com                 <- заголовки: "Имя: значение"
Accept: text/plain
                                  <- пустая строка заканчивает заголовки
(тело, если есть)
```

Ответ устроен так же: стартовая строка `HTTP/1.1 200 OK` (версия, **код статуса**, пояснение), заголовки, пустая строка и тело. Код статуса сообщает итог: `200` успех, `404` ресурса нет, `405` метод не подходит, `500` ошибка сервера.

В Node.js этим занимается модуль **`node:http`**: он принимает соединения, разбирает текст в объекты запроса и ответа и вызывает вашу функцию. Но самое интересное, то есть логика «по запросу построить ответ», - обычный код без сети. Его можно написать и проверить на строках, без единого открытого порта. Именно так устроены практические задания этого урока: **сокеты не открываются**, мы разбираем и формируем HTTP-текст руками, чтобы понять, что за кулисами делают `node:http` и фреймворки.

Строки в HTTP разделяются парой символов `\r\n` (CRLF). Клиенты и серверы рассчитывают на строгий формат, поэтому такие детали важны. Объекты запроса и ответа в `node:http` - потоки, знакомые по прошлым урокам ([потоки и обратное давление](lesson:nodejs.fundamentals.streams-backpressure)), а сам сервер - `EventEmitter` ([события и process](lesson:nodejs.fundamentals.events-and-process)). Для сравнения: HTTP-клиенты в Python ([надёжные HTTP-клиенты](lesson:python.automation.robust-http-clients)).

<!-- section: precise -->
## Сообщения, node:http и маршрутизация

**Запрос.** Стартовая строка `МЕТОД цель ВЕРСИЯ`. Методы: `GET` читает ресурс, `HEAD` - то же, но без тела в ответе, `POST` отправляет данные, `PUT`, `DELETE` и другие. Заголовки: `имя: значение`; имена нечувствительны к регистру (`Host` и `host` - одно и то же), повторяющийся заголовок можно объединять через запятую. В HTTP/1.1 заголовок `Host` обязателен.

**Длина тела.** Получатель должен знать, где тело кончается. Если указан `Content-Length`, то это точное число **байтов** (не символов: `Привет` - 12). Если указан `Transfer-Encoding: chunked`, тело идёт кусками: строка с размером в шестнадцатеричной записи, данные, конец строки; признак конца - кусок размера `0`, после него необязательные заголовки-трейлеры и пустая строка. Без обоих заголовков у запроса тела нет. Благодаря этим правилам по одному соединению можно отправить **несколько запросов подряд** (pipelining, keep-alive): граница между ними определяется длиной тела.

**Ответ.** Стартовая строка с кодом: классы `2xx` (успех), `3xx` (перенаправление), `4xx` (ошибка клиента), `5xx` (ошибка сервера). Важные коды: `400` (запрос некорректен), `404` (нет такого пути), `405` (путь есть, метод не подходит; в ответе обязателен заголовок `Allow`), `411` (нужен `Content-Length`), `501` (метод неизвестен серверу), `505` (версия не поддерживается). Тексты кодов берите из `http.STATUS_CODES`, список методов - `http.METHODS`.

**Сервер на node:http.** `http.createServer((req, res) => { ... })` создаёт сервер; сокет открывается лишь вызовом `server.listen(port)`. В обработчике `req` (`IncomingMessage`) - читаемый поток с полями `method`, `url` (путь с query), `headers` (имена в нижнем регистре); `res` (`ServerResponse`) - записываемый поток: `res.writeHead(status, headers)`, `res.write(...)`, `res.end(body)`. Тело запроса приходит потоком: его читают через `for await (const chunk of req)`. Код ниже показывает форму и не запускается в уроке:

```javascript
const http = require("node:http");

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  if (req.method === "GET" && url.pathname === "/hello") {
    res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8" });
    res.end(`Hello, ${url.searchParams.get("name") ?? "world"}!`);
    return;
  }
  res.writeHead(404);
  res.end("404 Not Found");
});

server.listen(3000); // только здесь открывается порт
```

**Маршрутизация.** Это выбор обработчика по паре «метод + путь». Простейшая реализация - таблица `{ "/hello": { GET: handler } }`: нет пути - `404`, нет метода - `405` с `Allow`. Адрес разбирают классом `URL`: `new URL(req.url, "http://localhost")` даёт `pathname` и `searchParams`, где значения уже декодированы из процентной записи.

**Тестируемость.** Чистая функция «запрос → ответ» легко проверяется на строках. Тонкая обёртка вокруг `node:http` только читает запрос и пишет результат. Фреймворки (Express, Fastify) устроены так же, но в этом курсе мы остаёмся на встроенных модулях.

**Безопасность.** Заголовки, путь и тело - недоверенный ввод. Ограничивайте размер тела, проверяйте `Content-Length` как целое число, сравнивайте его с реальным количеством данных, не отдавайте файлы по путям из URL без проверки выхода из корня ([файловая система и пути](lesson:nodejs.fundamentals.filesystem-paths)).

<!-- section: examples -->
## Примеры

Справочные объекты `node:http` и разбор адреса: сети здесь нет.

```javascript run
const http = require("node:http");

console.log(http.STATUS_CODES[200], "|", http.STATUS_CODES[404], "|", http.STATUS_CODES[405]);
console.log(http.METHODS.includes("GET"), http.METHODS.includes("FETCH"), http.METHODS.length > 20);

const url = new URL("/search?q=%D0%BA%D0%BE%D1%82&page=2&page=3#top", "http://localhost");
console.log(url.pathname);
console.log(url.searchParams.get("q"), url.searchParams.getAll("page"));
console.log([...url.searchParams.keys()]);
```
```output
OK | Not Found | Method Not Allowed
true false true
/search
кот [ '2', '3' ]
[ 'q', 'page', 'page' ]
```

Сообщение на проводе: видим `\r\n` и считаем длину в байтах.

```javascript run
const body = "Привет!";
const response = [
  "HTTP/1.1 200 OK",
  "Content-Type: text/plain; charset=utf-8",
  `Content-Length: ${Buffer.byteLength(body)}`,
  "",
  body,
].join("\r\n");

console.log(JSON.stringify(response));
console.log(body.length, Buffer.byteLength(body));
```
```output
"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 13\r\n\r\nПривет!"
7 13
```

Разбор запроса в объект: стартовая строка, заголовки без учёта регистра, тело по длине.

```javascript run
function parseRequest(raw) {
  const raw8 = Buffer.from(raw);
  const m = /\r?\n\r?\n/.exec(raw);
  const head = m ? raw.slice(0, m.index) : raw;
  const body = m ? raw8.subarray(Buffer.byteLength(raw.slice(0, m.index + m[0].length))) : Buffer.alloc(0);
  const [startLine, ...headerLines] = head.split(/\r?\n/);
  const [method, target, version] = startLine.split(" ");
  const headers = {};
  for (const line of headerLines) {
    const i = line.indexOf(":");
    headers[line.slice(0, i).toLowerCase()] = line.slice(i + 1).trim();
  }
  return { method, target, version, headers, body: body.subarray(0, Number(headers["content-length"] ?? 0)) };
}

const req = parseRequest("POST /echo HTTP/1.1\r\nHost: x\r\nCONTENT-LENGTH: 12\r\n\r\nПривет and more");
console.log(req.method, req.target, req.version);
console.log(req.headers);
console.log(req.body.length, req.body.toString("utf8"));
```
```output
POST /echo HTTP/1.1
{ host: 'x', 'content-length': '12' }
12 Привет
```

Маршрутизация таблицей: 404 и 405 с `Allow`.

```javascript run
const http = require("node:http");

const routes = {
  "/": { GET: () => "Welcome" },
  "/hello": { GET: (q) => `Hello, ${q.get("name") ?? "world"}!`, POST: () => "posted" },
};

function route(method, target) {
  const url = new URL(target, "http://localhost");
  const table = routes[url.pathname];
  if (!table) return `404 ${http.STATUS_CODES[404]}`;
  const handler = table[method];
  if (!handler) return `405 ${http.STATUS_CODES[405]}, Allow: ${Object.keys(table).join(", ")}`;
  return `200 ${handler(url.searchParams)}`;
}

console.log(route("GET", "/"));
console.log(route("GET", "/hello?name=Ann"));
console.log(route("DELETE", "/hello"));
console.log(route("GET", "/missing"));
```
```output
200 Welcome
200 Hello, Ann!
405 Method Not Allowed, Allow: GET, POST
404 Not Found
```

Chunked: тело кусками.

```javascript run
function decodeChunked(raw) {
  const parts = [];
  let pos = 0;
  for (;;) {
    const eol = raw.indexOf("\r\n", pos);
    const size = parseInt(raw.slice(pos, eol), 16);
    pos = eol + 2;
    if (size === 0) break;
    parts.push(raw.subarray(pos, pos + size));
    pos += size + 2;
  }
  return Buffer.concat(parts);
}

const wire = Buffer.from("5\r\nHello\r\n7\r\n, world\r\n0\r\n\r\n");
console.log(decodeChunked(wire).toString());
console.log(JSON.stringify(wire.toString()));
```
```output
Hello, world
"5\r\nHello\r\n7\r\n, world\r\n0\r\n\r\n"
```

<!-- section: mistakes -->
## Типичные ошибки

* **Content-Length в символах.** `body.length` для строки с кириллицей меньше числа байтов. Используйте `Buffer.byteLength(body)`.
* **Сравнение заголовков с учётом регистра.** `req.headers["Content-Type"]` не найдёт ничего: в `node:http` имена приходят в нижнем регистре, и в собственном разборе их нужно приводить к нему.
* **404 вместо 405.** Если путь известен, а метод нет, правильный ответ - `405` с `Allow`, иначе клиенту непонятно, что именно не так.
* **Разбор тела до проверки длины.** Сначала убедитесь, что получено ровно `Content-Length` байтов, затем разбирайте JSON; неверный JSON - это `400`, а не падение процесса.
* **Забытый `res.end()`.** Без него клиент ждёт ответ до таймаута.
* **Склеивать путь из URL с путём на диске без проверки.** Это даёт выход за корень (`..`).
* **Смешивать логику и сетевой код.** Обработчик, который сам читает сокет, невозможно проверить без запуска сервера. Выносите логику в чистые функции.
* **Считать, что запрос приходит одним куском.** Тело `req` - поток; границы кусков произвольны, а несколько запросов могут прийти слитно.

<!-- section: summary -->
## Итоги

* HTTP-сообщение - текст: стартовая строка, заголовки, пустая строка, тело; строки разделены `\r\n`.
* Длину тела задаёт `Content-Length` (в байтах) или `Transfer-Encoding: chunked` (куски, завершаются нулевым).
* `http.createServer` создаёт обработчик `(req, res)`; порт открывает только `listen`. `req` и `res` - потоки.
* Маршрутизация - выбор обработчика по методу и пути; `404` для неизвестного пути, `405` с `Allow` для неподходящего метода.
* Имена заголовков нечувствительны к регистру, в HTTP/1.1 нужен `Host`, а тексты кодов берут из `http.STATUS_CODES`.
* Логику ответа выносите в чистые функции: их проверяют строками, без сети и без запуска сервера.
