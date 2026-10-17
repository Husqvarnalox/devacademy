<!-- section: simple -->
## REST API: ресурсы, методы и статусы

HTTP API удобно проектировать вокруг **ресурсов** - «вещей», с которыми работает клиент: задачи, книги, заказы. У каждого ресурса есть адрес (`/tasks/17`), а действие выражается **методом** HTTP, а не словом в адресе:

| Что нужно | Запрос | Успешный ответ |
|---|---|---|
| список | `GET /tasks` | `200` и список |
| одна задача | `GET /tasks/17` | `200` и задача |
| создать | `POST /tasks` | `201`, заголовок `Location: /tasks/17`, созданная задача |
| заменить целиком | `PUT /tasks/17` | `200` и новая задача |
| удалить | `DELETE /tasks/17` | `204` без тела |

Адреса называют **существительными во множественном числе** (`/tasks`), а не `/getTasks` или `/deleteTask`. Вложенность отражает принадлежность: `/authors/7/books`.

Клиент часто не знает, дошёл ли запрос. Сеть оборвалась после отправки, ответа нет - что делать? Повторить. Поэтому важно понятие **идемпотентности**: операция идемпотентна, если повторение даёт то же состояние, что и одно выполнение. `GET`, `PUT` и `DELETE` идемпотентны, `POST` - нет: два повтора создадут две задачи. Об этом подробнее в уроке про [согласованность и идемпотентность](lesson:system-design.fundamentals.consistency-and-idempotency).

Остальное - дисциплина, которая делает API предсказуемым:

* **статусы** по смыслу (`201`, `204`, `400`, `404`, `405`, `409`, `412`, `422`), а не «всегда 200»;
* **единый формат ошибок** (предыдущий урок);
* **пагинация** для списков: клиент получает страницу и сведения о том, сколько всего;
* **фильтры** в строке запроса (`?done=true&q=хлеб`);
* **проверка** входных данных (урок про безопасность).

Express здесь ничего не навязывает: он даёт маршрутизацию и средства для ответа. Контракт API определяете вы, поэтому важно зафиксировать его и проверять тестами.

<!-- section: precise -->
## Контракт подробно

**Методы и безопасность.** `GET` и `HEAD` *безопасны* (не меняют состояние) и кэшируемы. `PUT` заменяет ресурс **целиком** переданным представлением: поля, которых нет в теле, не «остаются прежними» (для частичных изменений используют `PATCH`, он не идемпотентен по определению). `DELETE` удаляет. `POST` создаёт подчинённый ресурс или запускает обработку. Идемпотентность описывает **эффект на состояние**, а не одинаковость ответа: второй `DELETE` обычно возвращает `404`, но состояние то же - задачи нет.

**Статусы.**

* `200 OK` - успех с телом; `201 Created` - ресурс создан (добавьте `Location` с адресом); `204 No Content` - успех без тела (`DELETE`); `304 Not Modified` - клиентский кэш актуален.
* `400 Bad Request` - запрос не разобрать или параметры неверны (синтаксис JSON, `?limit=abc`); `422 Unprocessable Content` - запрос разобран, но данные не годятся (многие API используют для этого и 400: выберите один вариант и придерживайтесь его).
* `404 Not Found` - нет ресурса; `405 Method Not Allowed` - у адреса нет такого метода, ответ **обязан** содержать заголовок `Allow` со списком допустимых; `409 Conflict` - конфликт с текущим состоянием (например, дубликат); `412 Precondition Failed` - не выполнилось условие `If-Match`; `413 Content Too Large`.
* `5xx` - сбой сервера, детали клиенту не показывают.

**PUT и несуществующий ресурс.** Есть два допустимых контракта: «`PUT` создаёт ресурс по заданному клиентом адресу» (`201`) и «`PUT` только заменяет, для несуществующего `404`». Выбранный контракт описывают в документации и проверяют тестами. Для ресурсов с выдаваемыми сервером идентификаторами выбирают второй.

**Express и 405.** Express не различает «пути нет» и «метод не подходит»: оба случая доходят до финального обработчика. Чтобы вернуть `405` с `Allow`, на адрес добавляют `app.route(path).all(handler)` **после** конкретных методов: `all` срабатывает только для методов, которых нет выше. `HEAD` Express направляет в обработчик `GET` автоматически.

**Location.** `res.location(url)` ставит заголовок, `res.status(201).location(...).json(...)` - стандартная запись создания.

**Пагинация списков.**

* *Смещение (offset/limit)*: `?limit=10&offset=20`. Просто и позволяет прыгать на страницу, но при вставке или удалении записей между запросами страницы «съезжают» (запись видна дважды или пропускается), а большие смещения дорогие для базы.
* *Курсор (cursor)*: ответ содержит непрозрачный маркер последней записи (`?after=...`); стабилен при изменениях и быстр, но не позволяет прыгнуть на произвольную страницу.

В ответ обычно кладут `items` и сведения о странице (`total`, `limit`, `offset` либо `nextCursor`). Параметры всегда проверяют: значения целые, `limit` ограничен сверху (иначе клиент попросит миллион записей), неверное значение даёт `400`, а не молчаливую замену. Порядок элементов должен быть определённым (например, по `id`), иначе страницы нестабильны.

**Фильтры и сортировка** задаются строкой запроса. Значения из `req.query` - строки (или массивы строк при повторе параметра): булево значение сравнивают со строками `"true"`/`"false"`, число разбирают явно.

**Кэширование и условные запросы.** Ответ может нести `ETag` - идентификатор версии представления. Клиент повторяет запрос с `If-None-Match: <etag>`, и если ресурс не менялся, сервер отвечает `304` без тела, экономя трафик. `If-None-Match` сравнивает **слабо** (префикс `W/` игнорируется), допускает список тегов и значение `*`. Обратное - `If-Match` для изменяющих запросов: «выполни, только если ресурс всё ещё в той версии, которую я видел», иначе `412`. Это *оптимистичная блокировка*: она защищает от «потерянного обновления», когда два клиента изменяют один ресурс. Сравнение `If-Match` **сильное**: слабые теги не совпадают. Express по умолчанию сам вычисляет **слабый** `ETag` для `res.send`/`res.json` и сам отвечает `304`, если заголовок `If-None-Match` совпал (настройка `etag`: `"weak"` по умолчанию, `false` отключает).

**Версионирование и совместимость.** Изменения, не ломающие клиентов (новое необязательное поле), допустимы всегда; ломающие требуют новой версии (`/v2/tasks` или заголовок). Клиентам советуют игнорировать неизвестные поля ответа.

**Безопасность.** Не доверяйте `id` из тела при `PUT`/`POST`: идентификатор берётся из адреса или выдаётся сервером. Проверяйте, что у пользователя есть права на **конкретный** ресурс, а не только на тип ([контроль доступа](lesson:security.fundamentals.access-control)): иначе подстановка чужого `id` даёт доступ к чужим данным.

<!-- section: examples -->
## Примеры

POST не идемпотентен, PUT и DELETE идемпотентны: считаем состояние после повторов.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const store = new Map();
let nextId = 1;
const app = express();
app.use(express.json());
app.post("/tasks", (req, res) => {
  const task = { id: nextId++, title: req.body.title };
  store.set(task.id, task);
  res.status(201).location(`/tasks/${task.id}`).json(task);
});
app.put("/tasks/:id", (req, res) => {
  const id = Number(req.params.id);
  if (!store.has(id)) return res.status(404).json({ error: "no such task" });
  store.set(id, { id, title: req.body.title });
  res.json(store.get(id));
});
app.delete("/tasks/:id", (req, res) => {
  if (!store.delete(Number(req.params.id))) return res.status(404).json({ error: "no such task" });
  res.status(204).end();
});

(async () => {
  const call = async (method, url, body) => {
    const res = await inject(app, { method, url, headers: { "content-type": "application/json" }, payload: body && JSON.stringify(body) });
    console.log(method.padEnd(7), url.padEnd(9), res.statusCode, res.headers.location ?? "-", "| store:", store.size);
  };
  await call("POST", "/tasks", { title: "a" });
  await call("POST", "/tasks", { title: "a" });
  await call("PUT", "/tasks/1", { title: "b" });
  await call("PUT", "/tasks/1", { title: "b" });
  await call("DELETE", "/tasks/1");
  await call("DELETE", "/tasks/1");
})();
```
```output
POST    /tasks    201 /tasks/1 | store: 1
POST    /tasks    201 /tasks/2 | store: 2
PUT     /tasks/1  200 - | store: 2
PUT     /tasks/1  200 - | store: 2
DELETE  /tasks/1  204 - | store: 1
DELETE  /tasks/1  404 - | store: 1
```

405 с `Allow`: `all` после конкретных методов срабатывает только для остальных.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.route("/tasks/:id")
  .get((req, res) => res.json({ id: req.params.id }))
  .delete((req, res) => res.status(204).end())
  .all((req, res) => res.set("Allow", "GET, DELETE").status(405).json({ error: "method not allowed" }));

(async () => {
  for (const method of ["GET", "HEAD", "DELETE", "PATCH", "POST"]) {
    const res = await inject(app, { method, url: "/tasks/1" });
    console.log(method.padEnd(7), res.statusCode, res.headers.allow ?? "-");
  }
})();
```
```output
GET     200 -
HEAD    200 -
DELETE  204 -
PATCH   405 GET, DELETE
POST    405 GET, DELETE
```

Параметры списка приходят строками; их нужно проверять явно.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.get("/tasks", (req, res) => {
  const { limit = "10", done } = req.query;
  const n = typeof limit === "string" && /^\d+$/.test(limit) ? Number(limit) : NaN;
  res.json({ raw: req.query, limitOk: n >= 1 && n <= 50, doneIsBoolean: done === "true" || done === "false" || done === undefined });
});

(async () => {
  for (const url of ["/tasks", "/tasks?limit=5&done=true", "/tasks?limit=abc", "/tasks?limit=1&limit=2", "/tasks?limit=&done=1"]) {
    const res = await inject(app, { method: "GET", url });
    console.log(url.padEnd(26), JSON.stringify(res.json()));
  }
})();
```
```output
/tasks                     {"raw":{},"limitOk":true,"doneIsBoolean":true}
/tasks?limit=5&done=true   {"raw":{"limit":"5","done":"true"},"limitOk":true,"doneIsBoolean":true}
/tasks?limit=abc           {"raw":{"limit":"abc"},"limitOk":false,"doneIsBoolean":true}
/tasks?limit=1&limit=2     {"raw":{"limit":["1","2"]},"limitOk":false,"doneIsBoolean":true}
/tasks?limit=&done=1       {"raw":{"limit":"","done":"1"},"limitOk":false,"doneIsBoolean":false}
```

Пагинация смещением «съезжает», когда данные меняются между запросами:

```javascript run
const items = ["a", "b", "c", "d", "e", "f"];
const page = (offset, limit) => items.slice(offset, offset + limit);

console.log("page 1:", page(0, 3));
items.unshift("NEW"); // между запросами добавили запись в начало
console.log("page 2:", page(3, 3), "<- 'c' показан повторно");
```
```output
page 1: [ 'a', 'b', 'c' ]
page 2: [ 'c', 'd', 'e' ] <- 'c' показан повторно
```

Express сам считает слабый `ETag` и отвечает `304`; отключение настройки `etag` убирает это поведение.

```javascript run
const express = require("express");
const inject = require("light-my-request");

function build(etag) {
  const app = express();
  app.set("etag", etag);
  app.get("/doc", (req, res) => res.json({ v: 1 }));
  return app;
}

(async () => {
  for (const mode of ["weak", false]) {
    const app = build(mode);
    const first = await inject(app, { method: "GET", url: "/doc" });
    const tag = first.headers.etag;
    const second = tag ? await inject(app, { method: "GET", url: "/doc", headers: { "if-none-match": tag } }) : first;
    console.log(String(mode).padEnd(6), tag ?? "-", first.statusCode, second.statusCode, JSON.stringify(second.body));
  }
})();
```
```output
weak   W/"7-BThvKNFhT+yxx+MpvYJBf7SN1FI" 200 304 ""
false  - 200 200 "{\"v\":1}"
```

<!-- section: mistakes -->
## Типичные ошибки

* **Глаголы в адресах.** `/createTask`, `/tasks/delete?id=3`: действие выражает метод HTTP.
* **Всегда 200.** Ошибка с кодом 200 и `{ "success": false }` ломает кэши, мониторинг и клиентские библиотеки. Статус - часть контракта.
* **`POST` без `Location` и без 201.** Клиент не узнает адрес созданного ресурса.
* **`PUT` как частичное обновление.** Он заменяет ресурс целиком; для частичных изменений используйте `PATCH`.
* **`PUT` по несуществующему `id`, создающий запись.** Для идентификаторов, выдаваемых сервером, это даёт дыры и коллизии: отвечайте `404`.
* **Идентификатор из тела.** Клиент пришлёт `id: 1` и перепишет чужую запись; берите `id` из адреса.
* **405 как 404.** Клиенту нужно знать, что адрес есть, но метод не подходит, и какие методы разрешены (`Allow`).
* **`limit` без верхней границы** и тихая замена неверных значений вместо `400`: клиент не узнает о своей ошибке.
* **Список без определённого порядка.** При пагинации записи повторяются или теряются.
* **Считать `offset` защитой от изменения данных.** При вставках между запросами страницы смещаются; для живых лент нужен курсор.
* **Проверка только типа ресурса, но не владельца.** Пользователь запрашивает `/tasks/42` и получает чужую задачу.
* **Слабый `ETag` в `If-Match`.** Для изменений сравнение сильное: слабый тег не должен разрешать запись.
* **`DELETE`, который падает при повторе с `500`.** Повтор должен быть безопасным: `404` или `204`, но не сбой.

<!-- section: summary -->
## Итоги

* Ресурсы - существительные в адресах, действия - методы HTTP: `GET`, `POST`, `PUT`, `DELETE`.
* `201` + `Location` при создании, `204` для `DELETE`, `404` для неизвестного ресурса, `405` + `Allow` для неподдерживаемого метода, `400`/`422` для неверных данных.
* `GET`, `PUT`, `DELETE` идемпотентны (повтор не меняет итоговое состояние), `POST` - нет; повторные запросы после сбоев безопасны только для идемпотентных операций.
* Списки: определённый порядок, `limit` с верхней границей, проверка параметров, ответ `{ items, total, limit, offset }`; для изменчивых данных - курсоры.
* `ETag` и `If-None-Match` экономят трафик (`304`); `If-Match` защищает от потерянных обновлений (`412`, сильное сравнение).
* Express по умолчанию сам ставит слабый `ETag`; контракт API (методы, статусы, форматы) фиксируют тестами.
