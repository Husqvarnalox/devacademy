<!-- section: simple -->
## Тесты Express-приложения без сети

Приложение Express - это функция `(req, res)`. Значит, проверять его можно без запуска сервера: подать искусственный запрос и посмотреть на ответ. В курсе для этого используется `light-my-request` (функция `inject`), а запускает тесты **Vitest**. Такая связка быстрая (миллисекунды на тест), надёжная (нет портов, сети и ожиданий) и работает в песочнице.

Тест состоит из трёх частей:

1. **Подготовка** - создаём приложение и данные (`createApp()`);
2. **Действие** - отправляем запрос (`inject(app, { method: "POST", url: "/bookmarks", payload })`);
3. **Проверка** - сравниваем ответ с ожидаемым (`expect(res.statusCode).toBe(201)`).

```javascript
import { describe, it, expect } from "vitest";
import inject from "light-my-request";
import { createApp } from "../src/app.mjs";

describe("API закладок", () => {
  it("неизвестная закладка - 404", async () => {
    const app = createApp();
    const res = await inject(app, { method: "GET", url: "/bookmarks/42", headers: { "x-api-key": "secret" } });
    expect(res.statusCode).toBe(404);
  });
});
```

Что проверять в API: **статус**, **важные заголовки** (`Location`, `Allow`, `Content-Type`), **тело** (форма и значения) и **побочные эффекты** (после неудачного запроса данные не изменились). Не проверяйте внутренности: какие функции вызваны, как названы переменные. Хороший тест не ломается при рефакторинге, который не меняет поведения.

Второй вид тестов - **юнит-тесты отдельного middleware**: вызываем функцию с поддельными `req`, `res`, `next` и смотрим, ответила она или передала управление. Они быстрые и точно указывают, где ошибка (например, потерян `return` после ответа 401).

Главные правила:

* каждый тест получает **своё** приложение (фабрика `createApp()`), чтобы тесты не влияли друг на друга;
* зависимости, делающие результат непредсказуемым (время, случайные идентификаторы), **подставляются** снаружи;
* тест, который никогда не падает, бесполезен: проверяйте, что он действительно краснеет, если сломать код.

В этом уроке вы работаете «с другой стороны»: дано приложение с ошибками и набор тестов, а ваша задача - найти причины падений и исправить приложение.

<!-- section: precise -->
## Инструменты и приёмы

**Vitest.** Файлы `*.test.mjs` запускаются командой `vitest`. Функции `describe` (группа), `it`/`test` (тест), `expect` (проверка), `beforeEach`/`afterEach` (подготовка и очистка), `vi.fn()` (шпион/заглушка). Тест может быть `async`: возвращённый промис ожидается. Упавшая проверка выбрасывает исключение, поэтому тест прекращается на первой же неудачной проверке. Основные проверки: `toBe` (строгое равенство примитивов), `toEqual` (глубокое сравнение структур), `toMatch` (строка по шаблону), `toBeUndefined`, `toHaveBeenCalledTimes`, `toThrow`.

**light-my-request.** `inject(app, { method, url, headers, payload, remoteAddress })` возвращает промис с ответом: `statusCode`, `headers` (имена в нижнем регистре), `body` (строка), `json()` (разобранное тело), `cookies`. `payload` - строка или объект (объект сериализуется в JSON и получает нужный `Content-Type`); для проверки ошибок разбора передавайте строку. Порт не открывается, приложение вызывается как функция.

**Фабрика приложения.** `createApp(зависимости)` возвращает новое приложение с новым состоянием. Если состояние лежит в переменных **модуля**, оно живёт на протяжении всего прогона тестов, и результат зависит от порядка: первый тест видит пустое хранилище, второй - данные первого. Фабрика решает это.

**Фикстуры.** Повторяющуюся подготовку выносят в функцию-помощник (`setup()`), возвращающую приложение и удобную обёртку `call(method, url, options)`; так тесты остаются короткими и читаются как описания поведения. Для данных, общих для нескольких тестов, используют `beforeEach`. Лучше создавать данные внутри теста запросами через API (`POST`), чем заполнять хранилище в обход: тест проверяет и сам путь создания.

**Юнит-тест middleware.** Достаточно минимального поддельного `req` (с теми методами, которыми пользуется middleware), `res` с шпионами и `next = vi.fn()`. Шпионы `res.status` и `res.json` должны возвращать `res` (`mockReturnThis()`), иначе цепочка `res.status(401).json(...)` упадёт. Проверяют: был ли ответ (`res.status` вызван с 401), вызван ли `next` и сколько раз. Подделки хрупки: они знают, какие методы `req` использует middleware. Для логики, зависящей от нескольких свойств `req`, надёжнее интеграционный тест через `inject`.

**Тесты ошибок.** Важнее счастливого пути: отсутствие ключа, неверный JSON, неверный тип содержимого, несуществующий ресурс, повторное удаление. Для каждого проверяют статус, код ошибки и то, что **состояние не изменилось**.

**Детерминизм.** Время (`now()`), случайность и идентификаторы передают зависимостями: `createApp({ now, generateId })`. Тогда тест подставляет фиксированные значения. Нельзя полагаться на `setTimeout` и реальное время: такие тесты медленные и нестабильные.

**Пирамида тестов.** Много быстрых юнит-тестов чистых функций и middleware, меньше интеграционных (приложение целиком через `inject`), совсем мало сквозных тестов с реальным сервером и сетью. В большинстве приложений Express основную ценность дают интеграционные тесты через `inject`.

**Безопасность тестов.** Не кладите в тесты реальные ключи и пароли: используйте тестовые значения. Тест, проверяющий запрет доступа, должен убедиться, что запрещённое действие **не выполнилось**, а не только что вернулся `401`.

<!-- section: examples -->
## Примеры

Самодельный мини-тест на `node:assert`: подготовка, запрос и проверка. Те же шаги вы будете писать в Vitest.

```javascript run
const assert = require("node:assert/strict");
const express = require("express");
const inject = require("light-my-request");

function createApp() {
  const app = express();
  const items = [];
  app.use(express.json());
  app.post("/items", (req, res) => {
    items.push(req.body.name);
    res.status(201).location(`/items/${items.length}`).json({ id: items.length, name: req.body.name });
  });
  app.get("/items", (req, res) => res.json({ total: items.length }));
  return app;
}

(async () => {
  const app = createApp();
  const created = await inject(app, { method: "POST", url: "/items", payload: { name: "a" } });
  assert.equal(created.statusCode, 201);
  assert.equal(created.headers.location, "/items/1");
  assert.deepEqual(created.json(), { id: 1, name: "a" });
  assert.deepEqual((await inject(app, { method: "GET", url: "/items" })).json(), { total: 1 });
  console.log("все проверки прошли:", created.headers["content-type"]);
})();
```
```output
все проверки прошли: application/json; charset=utf-8
```

Юнит-тест middleware с поддельными `req`, `res`, `next`. Версия без `return` отвечает 401 и всё равно передаёт управление дальше - тест это ловит.

```javascript run
const assert = require("node:assert/strict");

const withBug = (key) => (req, res, next) => {
  if (req.headers["x-api-key"] !== key) res.status(401).json({ error: "unauthorized" });
  next();
};
const fixed = (key) => (req, res, next) => {
  if (req.headers["x-api-key"] !== key) return res.status(401).json({ error: "unauthorized" });
  next();
};

function run(middleware, headers) {
  const calls = [];
  const res = {
    status(code) { calls.push(`status ${code}`); return this; },
    json() { calls.push("json"); return this; },
  };
  middleware({ headers }, res, () => calls.push("next"));
  return calls;
}

for (const [name, factory] of [["с ошибкой", withBug], ["исправлено", fixed]]) {
  const denied = run(factory("k1"), {});
  const allowed = run(factory("k1"), { "x-api-key": "k1" });
  console.log(name.padEnd(11), JSON.stringify(denied), JSON.stringify(allowed));
}
assert.deepEqual(run(fixed("k1"), {}), ["status 401", "json"]);
```
```output
с ошибкой   ["status 401","json","next"] ["next"]
исправлено  ["status 401","json"] ["next"]
```

Состояние в модуле «протекает» между тестами, состояние внутри фабрики - нет.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const shared = [];
function leakyApp() {
  const app = express();
  app.post("/", (req, res) => { shared.push(1); res.json({ count: shared.length }); });
  return app;
}
function isolatedApp() {
  const own = [];
  const app = express();
  app.post("/", (req, res) => { own.push(1); res.json({ count: own.length }); });
  return app;
}

(async () => {
  for (const [name, make] of [["leaky", leakyApp], ["isolated", isolatedApp]]) {
    const first = await inject(make(), { method: "POST", url: "/" });
    const second = await inject(make(), { method: "POST", url: "/" });
    console.log(name.padEnd(9), first.json().count, second.json().count);
  }
})();
```
```output
leaky     1 2
isolated  1 1
```

Тип содержимого с параметрами: строгое сравнение заголовка и `req.is` ведут себя по-разному.

```javascript run
const express = require("express");
const inject = require("light-my-request");

const app = express();
app.post("/strict", (req, res) => res.json({ ok: req.get("content-type") === "application/json" }));
app.post("/is", (req, res) => res.json({ is: req.is("application/json"), json: req.is("json"), text: req.is("text/*") }));

(async () => {
  for (const type of ["application/json", "application/json; charset=utf-8", "text/plain"]) {
    const a = await inject(app, { method: "POST", url: "/strict", headers: { "content-type": type }, payload: "{}" });
    const b = await inject(app, { method: "POST", url: "/is", headers: { "content-type": type }, payload: "{}" });
    console.log(type.padEnd(34), a.body, b.body);
  }
})();
```
```output
application/json                   {"ok":true} {"is":"application/json","json":"json","text":false}
application/json; charset=utf-8    {"ok":false} {"is":"application/json","json":"json","text":false}
text/plain                         {"ok":false} {"is":false,"json":false,"text":"text/plain"}
```

Время как зависимость делает проверку мгновенной и точной:

```javascript run
const express = require("express");
const inject = require("light-my-request");

function createApp({ now = () => new Date() } = {}) {
  const app = express();
  app.get("/stamp", (req, res) => res.json({ at: now().toISOString() }));
  return app;
}

(async () => {
  const fixed = createApp({ now: () => new Date("2030-01-02T03:04:05.000Z") });
  const first = await inject(fixed, { method: "GET", url: "/stamp" });
  const second = await inject(fixed, { method: "GET", url: "/stamp" });
  console.log(first.body === second.body, first.body);
})();
```
```output
true {"at":"2030-01-02T03:04:05.000Z"}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Один общий экземпляр приложения на все тесты** (или данные в переменных модуля): тесты зависят от порядка и от запусков друг друга. Создавайте приложение в каждом тесте.
* **`app.listen` в тестах.** Нужны порт и сеть, тесты зависают и конфликтуют. Вызывайте приложение через `inject`.
* **Проверять только статус.** `201` без проверки `Location` и тела пропустит половину ошибок; `401` без проверки, что данные не изменились, пропустит потерянный `return`.
* **Тест, который не может упасть.** Проверка `expect(res.statusCode).toBeDefined()` пройдёт всегда. Проверяйте конкретные значения и убеждайтесь, что тест краснеет на сломанном коде.
* **Забытый `await`.** Без `await` перед `inject(...)` тест завершается до ответа и проверка не выполняется; в Vitest такой тест может «пройти» без единой проверки.
* **Шпионы без `mockReturnThis()`.** `res.status = vi.fn()` вернёт `undefined`, и `res.status(401).json(...)` упадёт с `TypeError` внутри middleware.
* **Привязка к реализации.** Проверка «вызван `res.json` один раз» ломается при безвредном рефакторинге; проверяйте видимое поведение (статус, заголовки, тело).
* **Тесты зависят от реального времени и случайных значений.** Подставляйте `now`, `generateId` через зависимости.
* **Сравнивать заголовок `Content-Type` строгим равенством.** Клиенты добавляют `; charset=utf-8`. В коде используйте `req.is(...)`, а в тестах `toMatch(/application\/json/)`.
* **Не проверять сообщения об ошибках на утечки.** Тест для 500 должен убедиться, что в теле нет текста исключения и трассировки.
* **Править тест, чтобы он прошёл.** Если тест падает, сначала выясните, что верно: контракт или тест. Менять ожидание ради зелёного результата - значит терять защиту.

<!-- section: summary -->
## Итоги

* Приложение Express - функция: тестируйте через `inject` без портов, а `createApp(зависимости)` даёт каждому тесту своё состояние.
* Тест = подготовка, действие, проверка. Проверяйте статус, заголовки, тело и отсутствие побочных эффектов при ошибках.
* Юнит-тесты middleware используют поддельные `req`, `res`, `next`; шпионы `res.status` и `res.json` возвращают `res`.
* Ошибочные пути (нет ключа, неверный JSON, неверный тип содержимого, повторное удаление) важнее счастливого.
* Время, случайность и идентификаторы передают зависимостями, чтобы результат был детерминированным.
* При падении теста сначала определите, что неверно: код или ожидание; не подгоняйте тест под результат.
