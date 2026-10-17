<!-- section: simple -->
## Всё, что пришло из сети, - недоверенные данные

Сервер Express стоит на границе: с одной стороны интернет, с другой - ваша логика и данные. Всё, что пришло в запросе (тело, строка запроса, заголовки, cookie, части пути), составлено клиентом, а клиентом может быть кто угодно. Поэтому на границе нужны четыре слоя защиты, и все они - обычные middleware:

1. **Проверка входных данных.** Принимаем только то, что ожидаем: нужные поля, нужные типы, разумные размеры. Остальное отвергаем с понятным списком ошибок.
2. **Защитные заголовки.** Несколько заголовков ответа подсказывают браузеру, как себя вести: не угадывать тип содержимого, не встраивать страницу в чужой фрейм, не слать лишнего в `Referer`, ходить только по HTTPS.
3. **CORS.** Браузер по умолчанию запрещает странице с одного сайта читать ответы другого. CORS - способ сервера явно сказать: «этому источнику можно». Заголовки CORS - не защита сервера (запрос всё равно дойдёт), а разрешение, которое проверяет **браузер**.
4. **Ограничение частоты.** Один клиент не должен занимать весь сервер или перебирать пароли тысячами попыток в секунду ([ограничение частоты в проектировании систем](lesson:system-design.fundamentals.rate-limiting) разбирает алгоритмы подробнее).

Готовые пакеты (`helmet`, `cors`, `express-rate-limit`, `zod`) делают это за вас. В курсе мы пишем упрощённые версии вручную, чтобы увидеть, **что именно** они делают: после этого любую библиотеку можно читать и настраивать осознанно. В production используйте проверенные пакеты, а не самописный код.

Пятая тема - **cookie сессии**: они хранят идентификатор вошедшего пользователя, поэтому их флаги (`HttpOnly`, `Secure`, `SameSite`) решают, сможет ли чужой скрипт или чужой сайт ими воспользоваться ([сессии, CSRF и cookie](lesson:security.fundamentals.sessions-csrf-and-cookies)).

Главный принцип: **белый список, а не чёрный**. Не «запретить плохое», а «разрешить только известное»: разрешённые поля, разрешённые источники CORS, разрешённая частота.

<!-- section: precise -->
## Что именно делает каждый слой

**Проверка данных.** Проверка выполняется на сервере всегда, даже если форма в браузере уже проверила то же самое: клиентскую проверку обходят `curl`-ом. Проверяют: наличие обязательных полей; **тип** (`typeof`, `Number.isInteger`, `Array.isArray`: из JSON приходят строки, числа, булевы значения, `null`, массивы, объекты); **границы** (длина строки, диапазон числа, размер массива: без верхней границы запрос может быть сколь угодно большим); **набор значений** (`enum`); **формат** (регулярное выражение); **неизвестные поля** (лишние ключи отвергают или отбрасывают явно). Проверяют **все** поля и возвращают **список** ошибок, а не первую: клиенту удобнее исправить всё за раз. Статус для «тело разобрано, но не годится» - `422 Unprocessable Content` (или `400`, если так принято в вашем API; важно выбрать один и придерживаться его).

Нюансы JavaScript: `JSON.parse('{"__proto__":{"admin":true}}')` создаёт **собственное** свойство с именем `__proto__`; при слиянии в другой объект через `Object.assign` или циклом оно изменяет прототип результата. Поэтому наличие поля проверяют через `Object.hasOwn(obj, key)`, а не `key in obj`, а неизвестные ключи отвергают. Значения из `req.query` - строки или массивы строк (`?id=1&id=2`), а `Number("")` равно `0`; число получают явным разбором и проверкой формата.

**Защитные заголовки** (то, что делает `helmet`):

* `X-Content-Type-Options: nosniff` - браузер не угадывает тип содержимого (иначе загруженный «текст» может исполниться как скрипт).
* `Content-Security-Policy` - список допустимых источников скриптов, стилей и т. д.; для API достаточно `default-src 'none'`. `frame-ancestors 'none'` запрещает встраивание в фреймы (защита от кликджекинга; заменяет устаревший `X-Frame-Options`).
* `Referrer-Policy: no-referrer` - не отправлять адрес страницы в заголовке `Referer`.
* `Strict-Transport-Security: max-age=...` - браузер, увидев его по HTTPS, дальше сам заходит на сайт только по HTTPS (игнорируется на обычном HTTP).
* `Cross-Origin-Resource-Policy: same-origin` - другие сайты не могут встраивать ваши ресурсы.
* Убрать `X-Powered-By: Express` (`app.disable("x-powered-by")` или `res.removeHeader`): не облегчайте поиск уязвимостей известной версии.

Заголовки нужно ставить на **каждый** ответ, в том числе на ошибки и 404, поэтому middleware ставится первым.

**CORS.** Источник (origin) - тройка «схема, хост, порт». Для «простых» запросов браузер отправляет запрос и проверяет в ответе `Access-Control-Allow-Origin`: если значения нет или оно не совпало, страница не получит ответ. Для остальных (метод `PUT`/`DELETE`, заголовки `Authorization` или `Content-Type: application/json`) браузер сначала шлёт **preflight** - `OPTIONS` с заголовками `Origin` и `Access-Control-Request-Method` - и только после разрешения отправляет сам запрос. Ответ на preflight: `204` и `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers`, `Access-Control-Max-Age`. Правила: возвращайте **тот** `Origin`, который есть в белом списке, а не `*` вместе с учётными данными (браузер такое запретит); добавляйте `Vary: Origin`, чтобы кэш не отдал ответ с чужим разрешением; не «отражайте» любой Origin без проверки: это равносильно отказу от защиты.

**Ограничение частоты (fixed window).** Для ключа (обычно IP-адрес клиента, `req.ip`; за прокси включают `app.set("trust proxy", ...)`, иначе все клиенты выглядят как один адрес прокси) считают запросы в окне фиксированной длины. Лишний запрос получает `429 Too Many Requests` и `Retry-After` (секунды до сброса). Заголовки `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` сообщают клиенту остаток. Фиксированное окно просто, но допускает всплеск вдвое больше лимита на стыке окон; скользящее окно и токен-бакет сглаживают это. Счётчики в памяти процесса действуют только в нём: при нескольких экземплярах сервера нужно общее хранилище (например, Redis). Время подаётся в middleware как зависимость (`now`), поэтому поведение проверяется в тестах без ожиданий.

**Cookie.** `res.cookie(name, value, options)` ставит заголовок `Set-Cookie`. Флаги сессионной cookie: `HttpOnly` (скрипт на странице не может прочитать её через `document.cookie`, значит, XSS не украдёт сессию), `Secure` (отправляется только по HTTPS), `SameSite=Lax` (не отправляется при межсайтовых запросах, кроме перехода по ссылке: основная защита от CSRF), `Path`, `Max-Age` (время жизни; в Express опция `maxAge` задаётся в **миллисекундах**, а в заголовок попадает в секундах). Имена с префиксом `__Host-` дополнительно требуют `Secure`, `Path=/` и отсутствия `Domain`. Значение сессии - случайный идентификатор, а не данные пользователя.

**Что не заменяет проверку.** Параметризованные запросы к базе, экранирование вывода и проверка прав - отдельные уровни ([защита от инъекций](lesson:security.fundamentals.injection-defences), [контроль доступа](lesson:security.fundamentals.access-control)). Проверка формата не делает данные безопасными для SQL или HTML.

<!-- section: examples -->
## Примеры

Заголовки по умолчанию и после middleware. Обратите внимание: `X-Powered-By` Express ставит сам, до ваших middleware, а `removeHeader` его снимает.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const plain = express();
plain.get("/", (req, res) => res.json({ ok: true }));

const hardened = express();
hardened.use((req, res, next) => {
  res.set("X-Content-Type-Options", "nosniff");
  res.set("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
  res.removeHeader("X-Powered-By");
  next();
});
hardened.get("/", (req, res) => res.json({ ok: true }));

(async () => {
  for (const [name, app] of [["plain", plain], ["hardened", hardened]]) {
    const res = await inject(app, { method: "GET", url: "/" });
    console.log(name.padEnd(9), res.headers["x-powered-by"] ?? "-", "|", res.headers["x-content-type-options"] ?? "-", "|", res.headers["content-security-policy"] ?? "-");
  }
})();
```
```output
plain     Express | - | -
hardened  - | nosniff | default-src 'none'; frame-ancestors 'none'
```

CORS: разрешённый источник, чужой источник и preflight. `Vary: Origin` ставится всегда.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const allowed = new Set(["https://app.example.com"]);
const app = express();
app.use((req, res, next) => {
  res.vary("Origin");
  const origin = req.get("Origin");
  if (origin === undefined) return next();
  const preflight = req.method === "OPTIONS" && req.get("Access-Control-Request-Method") !== undefined;
  if (!allowed.has(origin)) {
    return preflight ? res.status(403).json({ error: "origin not allowed" }) : next();
  }
  res.set("Access-Control-Allow-Origin", origin);
  if (!preflight) return next();
  res.set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE");
  res.status(204).end();
});
app.get("/data", (req, res) => res.json({ secret: 1 }));

(async () => {
  const cases = [
    ["GET", {}],
    ["GET", { origin: "https://app.example.com" }],
    ["GET", { origin: "https://evil.example" }],
    ["OPTIONS", { origin: "https://app.example.com", "access-control-request-method": "PUT" }],
    ["OPTIONS", { origin: "https://evil.example", "access-control-request-method": "PUT" }],
  ];
  for (const [method, headers] of cases) {
    const res = await inject(app, { method, url: "/data", headers });
    console.log(method.padEnd(8), (headers.origin ?? "-").padEnd(26), res.statusCode, res.headers["access-control-allow-origin"] ?? "-", "|", res.headers.vary);
  }
})();
```
```output
GET      -                          200 - | Origin
GET      https://app.example.com    200 https://app.example.com | Origin
GET      https://evil.example       200 - | Origin
OPTIONS  https://app.example.com    204 https://app.example.com | Origin
OPTIONS  https://evil.example       403 - | Origin
```

Фиксированное окно: часы подаются снаружи, поэтому пример детерминирован.

```javascript run
const express = require("express");
const inject = require("light-my-request");

function rateLimit({ limit, windowMs, now }) {
  const buckets = new Map();
  return (req, res, next) => {
    const t = now();
    let b = buckets.get(req.ip);
    if (!b || t >= b.resetAt) { b = { count: 0, resetAt: t + windowMs }; buckets.set(req.ip, b); }
    b.count += 1;
    const reset = Math.ceil((b.resetAt - t) / 1000);
    res.set("RateLimit-Remaining", String(Math.max(0, limit - b.count)));
    if (b.count > limit) return res.status(429).set("Retry-After", String(reset)).json({ error: "slow down" });
    next();
  };
}

let time = 0;
const app = express();
app.use(rateLimit({ limit: 2, windowMs: 10_000, now: () => time }));
app.get("/", (req, res) => res.send("ok"));

(async () => {
  const hit = async (label, ip) => {
    const res = await inject(app, { method: "GET", url: "/", remoteAddress: ip });
    console.log(label.padEnd(12), res.statusCode, "remaining=" + res.headers["ratelimit-remaining"], "retry-after=" + (res.headers["retry-after"] ?? "-"));
  };
  await hit("t=0 a", "10.0.0.1");
  await hit("t=0 a", "10.0.0.1");
  await hit("t=0 a", "10.0.0.1");
  await hit("t=0 b", "10.0.0.2");
  time = 4000;
  await hit("t=4s a", "10.0.0.1");
  time = 10_000;
  await hit("t=10s a", "10.0.0.1");
})();
```
```output
t=0 a        200 remaining=1 retry-after=-
t=0 a        200 remaining=0 retry-after=-
t=0 a        429 remaining=0 retry-after=10
t=0 b        200 remaining=1 retry-after=-
t=4s a       429 remaining=0 retry-after=6
t=10s a      200 remaining=1 retry-after=-
```

Cookie сессии: флаги в заголовке `Set-Cookie`; `maxAge` в Express - миллисекунды.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.post("/weak", (req, res) => { res.cookie("sid", "abc123"); res.end(); });
app.post("/strong", (req, res) => {
  res.cookie("sid", "abc123", { httpOnly: true, secure: true, sameSite: "lax", path: "/", maxAge: 3600 * 1000 });
  res.end();
});

(async () => {
  for (const url of ["/weak", "/strong"]) {
    const res = await inject(app, { method: "POST", url });
    console.log(url.padEnd(8), res.headers["set-cookie"].replace(/Expires=[^;]+/, "Expires=..."));
  }
})();
```
```output
/weak    sid=abc123; Path=/
/strong  sid=abc123; Max-Age=3600; Path=/; Expires=...; HttpOnly; Secure; SameSite=Lax
```

Данные из JSON нельзя сливать вслепую: поле `__proto__` меняет прототип результата. Проверка через `Object.hasOwn` и отказ от неизвестных ключей это исключают.

```javascript run
const body = JSON.parse('{"name":"Anna","__proto__":{"isAdmin":true}}');
const merged = Object.assign({}, body);

console.log(Object.keys(body));
console.log(merged.isAdmin, Object.hasOwn(merged, "isAdmin"));

const allowed = ["name"];
const unknown = Object.keys(body).filter((key) => !allowed.includes(key));
console.log(unknown);
console.log(Number(""), Number(" "), Number("1e3"), Number(["7"]));
```
```output
[ 'name', '__proto__' ]
true false
[ '__proto__' ]
0 0 1000 7
```

<!-- section: mistakes -->
## Типичные ошибки

* **Проверка только на клиенте.** Браузерную форму обходят `curl`-ом; сервер проверяет всё сам.
* **Только `typeof x === "object"` или «поле есть».** `null`, массивы, строки вместо чисел проходят такую проверку. Проверяйте тип точно: `Number.isInteger`, `Array.isArray`.
* **Нет верхних границ.** Строка в мегабайт, массив из миллиона элементов - это нагрузка на сервер. Задавайте `max` для строк и массивов и `limit` для `express.json()`.
* **Отвергать только первую ошибку.** Клиент исправляет по одной и отправляет снова; отдавайте список всех ошибок с полем и кодом.
* **Принимать неизвестные поля.** Лишний `isAdmin: true` может попасть в базу при слиянии объектов. Отвергайте или явно отбрасывайте лишнее.
* **`Access-Control-Allow-Origin: *` вместе с авторизацией и «отражение» любого Origin.** Это отмена защиты. Белый список точных значений.
* **Считать CORS защитой API.** CORS ограничивает только браузеры; `curl` и серверы его игнорируют. Доступ ограничивает авторизация.
* **Забытый `Vary: Origin`.** Общий кэш выдаст ответ с `Allow-Origin` одного сайта другому.
* **Лимит по `req.ip` за прокси без `trust proxy`.** Все клиенты выглядят как адрес прокси и блокируют друг друга; обратное - слепое доверие `X-Forwarded-For` позволяет подделать адрес.
* **Счётчик лимита в глобальной переменной.** Он общий для всех экземпляров приложения в процессе (и тестов) и теряется при перезапуске; выносите его в фабрику и, для нескольких серверов, в общее хранилище.
* **Cookie сессии без `HttpOnly`, `Secure`, `SameSite`.** Любой XSS читает её, любой HTTP-запрос раскрывает, любой чужой сайт может отправить её вместе с запросом.
* **Путать `maxAge` в миллисекундах с секундами.** `maxAge: 3600` в Express - это 3,6 секунды.
* **Заголовки безопасности только на успешных ответах.** Ставьте их первым middleware, чтобы они были и у 404, и у ошибок.

<!-- section: summary -->
## Итоги

* Всё из запроса недоверенно: проверяйте тип, границы, набор значений, формат и неизвестные поля; возвращайте список ошибок (`422`).
* Защитные заголовки (`nosniff`, CSP, `Referrer-Policy`, HSTS, CORP) ставят первым middleware на каждый ответ; `X-Powered-By` убирают.
* CORS - разрешение для браузера: белый список источников, точное значение `Allow-Origin`, `Vary: Origin`, preflight с `204`.
* Ограничение частоты: окно на ключ, `429` с `Retry-After`, заголовки `RateLimit-*`; время и ключ подаются зависимостями, счётчики - в фабрике.
* Cookie сессии: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path`, `Max-Age` (`maxAge` в Express - миллисекунды).
* В production используйте проверенные пакеты, а принципы из этого урока помогут их настроить; проверка данных не заменяет параметризованные запросы, экранирование вывода и авторизацию.
