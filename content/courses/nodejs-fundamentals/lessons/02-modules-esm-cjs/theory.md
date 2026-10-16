<!-- section: simple -->
## Модуль - это файл, а формат файла выбирает Node.js

Модуль - единица кода со своей областью видимости: переменные файла не попадают в общую область, наружу выходит только то, что файл явно **экспортирует**. В Node.js сосуществуют две модульные системы:

* **CommonJS** (CJS): `require("./util")` и `module.exports = ...`. Появилась вместе с Node.js.
* **ES-модули** (ESM): `import ... from "./util.js"` и `export`. Стандарт языка; в актуальных версиях Node.js работает без флагов.

Вводные отличия разобраны в курсе «Node.js: первые шаги» ([модули ESM и CommonJS](lesson:nodejs.first-steps.modules-and-event-loop)), а ES-модули как часть языка - в [JavaScript: модули и тесты](lesson:javascript.fundamentals.modules-and-testing). Здесь мы идём глубже: как именно выбирается формат, как Node.js ищет файл по строке из `require` или `import` и как автор пакета управляет тем, что видят пользователи (`package.json`, `exports`).

Для сравнения: в Go единица - пакет и модуль с `go.mod` ([пакеты и модули Go](lesson:go.fundamentals.packages-modules-testing)), в Python - файл `.py` и каталог-пакет. В Node.js у пакета есть манифест `package.json`, а формат каждого файла вычисляется.

```javascript
// CommonJS
const { join } = require("node:path");
module.exports = { join };

// ESM
import { join } from "node:path";
export { join };
```

<!-- section: precise -->
## Формат, разрешение и exports

**Формат файла** (в порядке проверки):

1. `.mjs` - всегда ESM; `.cjs` - всегда CommonJS.
2. `.js` - по полю `type` ближайшего `package.json` вверх по каталогам: `"module"` - ESM, `"commonjs"` - CommonJS.
3. Нет `type` - Node.js начиная с 22.7.0 (в ветке 20 - с 20.19.0) по умолчанию исследует синтаксис: `import`/`export` верхнего уровня или `import.meta` делают файл ESM, иначе CommonJS. В более ранних версиях этой ветки обнаружение синтаксиса включается флагом `--experimental-detect-module`. Это запасной вариант: явный `type` быстрее и понятнее.
4. Ввод через `--eval` или stdin подчиняется ключу `--input-type`.

**Что отличается.**

| | CommonJS | ESM |
|---|---|---|
| Загрузка | `require()` выполняет файл в месте вызова, синхронно | статический `import` разбирается до выполнения; граф загружается асинхронно; динамический `import()` возвращает промис |
| Значение экспорта | копия ссылки на момент `require` | **живая привязка** к переменной экспортирующего модуля |
| Расширение в пути | необязательно (`./util` найдёт `util.js`, `util.json`, `util/index.js`) | обязательно (`./util.js`) |
| Переменные модуля | `require`, `module`, `exports`, `__filename`, `__dirname` (параметры функции-обёртки) | нет; есть `import.meta` |
| `this` на верхнем уровне | `module.exports` | `undefined` |
| Режим | нестрогий по умолчанию | всегда строгий |
| `await` на верхнем уровне | нет | есть |
| JSON | `require("./a.json")` | `import data from "./a.json" with { type: "json" }` |

**Кеш.** Каждый модуль выполняется один раз; повторные `require` и `import` того же файла возвращают то же значение. В CommonJS кеш виден как `require.cache`. В ESM кеша для правки нет.

**Алгоритм разрешения строки (`specifier`).**

* Встроенные модули: `node:fs`, `fs` (префикс `node:` предпочтителен; у некоторых модулей без него импортировать нельзя).
* Относительные и абсолютные пути (`./a`, `../b`, `/x`, `file:///...`): ищется файл. В CommonJS перебираются расширения и `index.js`; в ESM файл должен быть указан точно.
* «Голые» имена (`lodash`): поиск каталога `node_modules` вверх от файла, затем чтение `package.json` найденного пакета.
* Ошибки: CommonJS - `MODULE_NOT_FOUND`, ESM - `ERR_MODULE_NOT_FOUND`.

**`exports` в package.json.** Если у пакета есть `exports`, пользователь может подключить **только** перечисленные подпути; всё остальное закрыто (`ERR_PACKAGE_PATH_NOT_EXPORTED`). Основные формы:

```json
{
  "name": "pkg",
  "type": "module",
  "exports": {
    ".": { "import": "./esm/index.js", "require": "./cjs/index.cjs" },
    "./util": "./util.js",
    "./features/*": "./src/features/*.js",
    "./package.json": "./package.json"
  }
}
```

* Ключи начинаются с `.`: `"."` - главная точка входа (`import "pkg"`), `"./util"` - подпуть (`import "pkg/util"`).
* Значение - строка, объект **условий** или `null`. Условия проверяются в порядке записи, берётся первое подошедшее: `node`, `import`, `require`, `default` (и пользовательские, например `types`). `default` ставят последним.
* Подпуть со `*` - шаблон: совпавший фрагмент подставляется в цель. При нескольких подходящих шаблонах побеждает самый длинный префикс; цель `null` закрывает путь.
* Поле `main` - старый способ задать точку входа; при наличии `exports` оно игнорируется современными версиями Node.js.
* Пакет с «двойным» форматом (`import` и `require` ведут в разные файлы) может сломаться, если один и тот же пакет загрузят обоими способами: получится два экземпляра с разным состоянием.

**`import.meta`.** `import.meta.url` - `file:`-адрес файла; `import.meta.filename` и `import.meta.dirname` - пути (Node.js 20.11+/21.2+); `import.meta.resolve(spec)` - разрешает строку так же, как `import`. Нужный `require` в ESM получают из `createRequire(import.meta.url)` модуля `node:module`.

**Взаимодействие.** `import` может подключать CommonJS: `module.exports` становится значением `default`. `import()` работает в обоих форматах. В Node.js 22 (с 22.12) `require()` умеет загружать ES-модуль, если в нём **нет** top-level await (иначе `ERR_REQUIRE_ASYNC_MODULE`); результат - объект пространства имён. Это возможность новых версий: для кода, который должен работать на старых, используйте `import()`.

<!-- section: examples -->
## Примеры

**Пример 1. Что даёт CommonJS-обёртка.**

```javascript run
console.log(arguments.length, this === module.exports, require.main === module);
console.log(require("path") === require("node:path"));
console.log(Object.keys(require.cache).length);
try {
  require("./no-such-file");
} catch (error) {
  console.log(error.code);
}
```
```output
5 true true
true
1
MODULE_NOT_FOUND
```

Node.js оборачивает файл в функцию с пятью параметрами (`exports`, `require`, `module`, `__filename`, `__dirname`). Поэтому `arguments.length` равен 5, а `this` совпадает с `module.exports`. Главный модуль - единственный в кеше, пока ничего не подключено.

**Пример 2. ES-модуль: нет `require`, есть `import.meta`.**

```javascript run
import { createRequire } from "node:module";
import path from "node:path";

console.log(typeof module, typeof __dirname, this);
console.log(path.basename(import.meta.filename), import.meta.dirname === path.dirname(import.meta.filename));
console.log(new URL(import.meta.url).protocol, import.meta.resolve("node:fs"));

const require = createRequire(import.meta.url);
console.log(require("node:os").EOL === "\n");
try {
  await import("./missing.mjs");
} catch (error) {
  console.log(error.code);
}
```
```output
undefined undefined undefined
main.js true
file: node:fs
true
ERR_MODULE_NOT_FOUND
```

Файл с `import` на верхнем уровне выполнен как ESM (тип определён по синтаксису). `typeof module` и `typeof __dirname` дают `undefined`, `this` - `undefined`. Свой `require` строит `createRequire`. Ошибка отсутствующего файла в ESM называется `ERR_MODULE_NOT_FOUND`, в CommonJS - `MODULE_NOT_FOUND`.

**Пример 3. Живая привязка против копии.**

```javascript run
import fs from "node:fs";
import { createRequire } from "node:module";

fs.writeFileSync("counter.mjs", "export let count = 0;\nexport function inc() { count++; }\n");
fs.writeFileSync("counter.cjs", "exports.count = 0;\nexports.inc = () => { exports.count++; };\n");

const esm = await import("./counter.mjs");
const cjs = createRequire(import.meta.url)("./counter.cjs");
const { count } = cjs;

esm.inc();
cjs.inc();
console.log("esm:", esm.count);
console.log("cjs, деструктуризация:", count, "cjs, свойство:", cjs.count);
```
```output
esm: 1
cjs, деструктуризация: 0 cjs, свойство: 1
```

Экспорт ESM - ссылка на переменную модуля, поэтому `esm.count` видит изменение. Деструктуризация CommonJS скопировала число и осталась нулём; свойство объекта обновилось.

**Пример 4. `require()` ES-модуля в Node.js 22.**

Пример требует Node.js 22.12 или новее (в ветке 20 - 20.19 или новее): `require(esm)` появился в 22.0.0 за флагом `--experimental-require-module`, а без флага работает с 22.12.0 и 23.0.0.

```javascript run
const fs = require("node:fs");
fs.writeFileSync("lib.mjs", "export const answer = 42;\nexport default 'by default';\n");
fs.writeFileSync("tla.mjs", "await null;\nexport const x = 1;\n");

const lib = require("./lib.mjs");
console.log(lib.answer, lib.default);
try {
  require("./tla.mjs");
} catch (error) {
  console.log(error.code);
}
import("./lib.mjs").then((ns) => console.log(ns.default === lib.default));
```
```output
42 by default
ERR_REQUIRE_ASYNC_MODULE
true
```

Синхронный ES-модуль загрузился через `require`; модуль с `await` на верхнем уровне - нет. `import()` отдаёт то же значение `default`: модуль выполнен один раз и лежит в кеше.

<!-- section: mistakes -->
## Типичные ошибки

* **Опускать расширение в ESM:** `import "./util"` даёт `ERR_MODULE_NOT_FOUND`. Пишите `./util.js`.
* **Использовать `__dirname` в ES-модуле.** Берите `import.meta.dirname` или `fileURLToPath(import.meta.url)`.
* **Вызывать `require` в ES-модуле** (`require is not defined`). Используйте `import` или `createRequire`.
* **Деструктурировать экспорт CommonJS и ждать обновлений.** Значение копируется один раз; обращайтесь через объект.
* **Добавить `exports` и забыть `"./package.json"` или нужные подпути:** пользователи получают `ERR_PACKAGE_PATH_NOT_EXPORTED`. Добавление `exports` в существующий пакет ломает прежние глубокие импорты.
* **Ставить `default` в объекте условий первым:** он перехватит всё, а следующие условия не проверяются.
* **Опираться на определение формата по синтаксису.** Явный `"type"` в `package.json` или расширение `.mjs`/`.cjs` надёжнее и работает на старых версиях.
* **Ждать, что `require` вернёт значение, экспортированное через `export default`:** вы получите пространство имён и поле `default`.
* **Выполнять работу при импорте** (подключение к базе, чтение файла): код модуля выполняется один раз при первой загрузке, и порядок побочных эффектов трудно отследить.

<!-- section: summary -->
## Итоги

* Формат файла задают расширение (`.mjs`, `.cjs`), поле `type` ближайшего `package.json` и, как запасной вариант, синтаксис.
* CommonJS: `require` выполняется на месте, значения копируются, есть `__dirname`. ESM: статические `import`, живые привязки, строгий режим, `import.meta`, top-level await, обязательные расширения.
* Разрешение строки: встроенные модули, пути, пакеты из `node_modules`; ошибки `MODULE_NOT_FOUND` и `ERR_MODULE_NOT_FOUND`.
* `exports` задаёт публичные точки входа пакета: подпути, условия (`import`, `require`, `node`, `default`), шаблоны со `*`, `null` для запрета.
* В Node.js 22.12+ (и 20.19+) `require()` без флага умеет загружать синхронные ES-модули; для остальных случаев используйте `import()`.

Предыдущий урок: [архитектура и цикл событий](lesson:nodejs.fundamentals.event-loop-architecture). Следующий: [асинхронные шаблоны](lesson:nodejs.fundamentals.async-patterns).
