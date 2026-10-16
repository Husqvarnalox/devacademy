<!-- section: simple -->
## Модули и тесты: как разделять код и проверять его

Программа из одного файла быстро становится тесной. **Модуль** - это файл, который явно говорит, что он отдаёт наружу (`export`), и явно берёт нужное из других файлов (`import`). Внутренние имена остаются приватными.

```javascript
// src/sku.mjs
export function normalizeSku(value) { /* ... */ }

// src/inventory.mjs
import { normalizeSku } from "./sku.mjs";
export class Inventory { /* ... */ }
```

**Тест** - маленькая программа, которая вызывает ваш код и сравнивает результат с ожидаемым. В Node.js есть встроенный раннер `node:test` и модуль проверок `node:assert`: ничего устанавливать не нужно. Запуск - `node --test`.

```javascript
// tests/sku.test.mjs
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { normalizeSku } from "../src/sku.mjs";

describe("normalizeSku", () => {
  it("приводит к верхнему регистру", () => {
    assert.equal(normalizeSku(" abc-0001 "), "ABC-0001");
  });
});
```

Что к чему. `import`/`export` - синтаксис языка (ECMAScript). А то, как Node.js находит файлы, различает ES-модули и CommonJS, и сам `node:test` - возможности среды выполнения.

<!-- section: precise -->
## Семантика ESM, интероперабельность с CommonJS и node:test

### ES-модули: что гарантирует язык

* **Статическая структура.** `import` и `export` пишутся на верхнем уровне, спецификатор - строковый литерал. Благодаря этому граф зависимостей известен *до выполнения*: все модули находятся и связываются первыми, а выполняются потом (сначала зависимости, затем зависящий модуль). Условный или вычисляемый импорт делается через динамический `import()`, который возвращает промис ([ECMAScript: Modules](https://tc39.es/ecma262/#sec-modules)).
* **Живые привязки (live bindings).** Импортированное имя - не копия значения, а **доступ только для чтения к переменной** в модуле-источнике. Если экспортирующий модуль изменит `export let count`, импортёры увидят новое значение. Присвоить импортированному имени из импортёра нельзя (`TypeError`).
* **Один экземпляр.** Каждый модуль выполняется один раз; все импортёры получают одни и те же привязки. Поэтому `instanceof` между модулями работает, а состояние модуля общее.
* **Строгий режим.** Код модуля всегда строгий; верхний `this` равен `undefined`; своей области видимости у каждого файла.
* **Именованные и default-экспорты.** `export const a`, `export { a as b }`, `export default expr`; `import x, { a } from`, `import * as ns from`. Реэкспорт: `export { a } from "./m.mjs"`. Объект пространства имён `ns` неизменяем.
* **Top-level await.** В ES-модуле `await` допустим на верхнем уровне. Модуль, ожидающий промис, задерживает выполнение зависящих от него модулей, но не соседей.

```javascript run
"use strict";
const counter = "data:text/javascript," + encodeURIComponent(
  "export let count = 0; export function inc() { count++; }"
);
import(counter).then((ns) => {
  console.log(ns.count);
  ns.inc();
  console.log(ns.count);
  try {
    ns.count = 10;
  } catch (e) {
    console.log(e.name + ": " + e.message);
  }
});
```
```output
0
1
TypeError: Cannot assign to read only property 'count' of object '[object Module]'
```

В примере модуль задан строкой `data:` (это возможность Node.js); после `inc()` значение `ns.count` изменилось: это живая привязка, а не копия.

```javascript run
const dep = "data:text/javascript," + encodeURIComponent(
  'export const value = await Promise.resolve(42); console.log("dep выполнен");'
);
const main = "data:text/javascript," + encodeURIComponent(
  `import { value } from ${JSON.stringify(dep)}; console.log("main видит", value); export default value + 1;`
);
console.log("до import()");
import(main).then((ns) => console.log("default:", ns.default, Object.keys(ns)));
```
```output
до import()
dep выполнен
main видит 42
default: 43 [ 'default' ]
```

Здесь зависимость использует top-level `await`, и `main` начинает выполняться только после неё.

### Node.js: ESM и CommonJS

Какой тип модуля у файла, решает **среда**: `.mjs` - всегда ES-модуль, `.cjs` - всегда CommonJS, а для `.js` смотрят поле `"type"` в ближайшем `package.json` (`"module"` или `"commonjs"`) ([Node.js: Modules: Packages](https://nodejs.org/api/packages.html#determining-module-system)). Отличия, о которых надо знать:

* В ESM относительный путь **обязан содержать расширение** (`./sku.mjs`); `index.js` из каталога не подставляется.
* В ESM нет `require`, `module`, `exports`, `__dirname`, `__filename`; вместо них `import.meta.url` (а в современных версиях Node.js и `import.meta.dirname`).
* CommonJS (`require`/`module.exports`) загружает модуль синхронно и копирует **значение** экспорта на момент вызова, живых привязок нет.
* Из ESM можно импортировать CommonJS (`import pkg from "./x.cjs"`, `module.exports` становится default-экспортом). Из CommonJS ES-модуль загружают динамическим `import()` (он возвращает промис); возможность `require()` для синхронных ES-модулей зависит от версии Node.js, сверяйтесь с документацией вашей версии.

```javascript run
const esm = "data:text/javascript," + encodeURIComponent(
  "export const info = [typeof require, typeof module, String(this), typeof import.meta.url];"
);
import(esm).then((ns) => console.log("в ESM:", ns.info.join(", ")));
console.log("в CommonJS:", [typeof require, typeof module, typeof __dirname].join(", "));
```
```output
в CommonJS: function, object, string
в ESM: undefined, undefined, undefined, string
```

### node:test и node:assert

* `describe(name, fn)` группирует тесты, `it(name, fn)` (синоним `test`) описывает один тест. Тест проходит, если функция завершилась без исключения; для асинхронных тестов функция возвращает промис (`async`).
* Хуки: `before`, `after`, `beforeEach`, `afterEach`.
* `node --test` находит файлы тестов по шаблонам, запускает каждый в отдельном процессе и печатает отчёт; код возврата ненулевой, если что-то упало. Шаблоны можно передать явно (`node --test "tests/*.test.mjs"`) ([Node.js: Test runner](https://nodejs.org/api/test.html)).
* `node:assert/strict` - строгая версия проверок: `assert.equal` сравнивает через `Object.is` (как `===`, но `NaN` равен `NaN`), `deepEqual` - глубокое строгое сравнение, `throws`/`rejects` проверяют исключение или отказ промиса, `ok` - истинность.

```javascript run
const assert = require("node:assert/strict");
try {
  assert.equal(1, 2);
} catch (e) {
  console.log(e.name, e.code);
}
assert.deepEqual({ a: [1, 2] }, { a: [1, 2] });
assert.throws(() => { throw new RangeError("bad"); }, RangeError);
assert.throws(() => { throw new RangeError("bad"); }, { name: "RangeError", message: "bad" });
console.log("все проверки прошли");
```
```output
AssertionError ERR_ASSERTION
все проверки прошли
```

### Как писать тесты

Хороший тест проверяет **поведение**, а не реализацию; один тест - одно утверждение смысла. Всегда включайте граничные и ошибочные входы: пустые значения, `0`, `-1`, `NaN`, неверный тип. Тесты не должны зависеть друг от друга и от порядка: общее состояние пересоздавайте в `beforeEach`. Падающий тест надо сначала увидеть красным, и только потом чинить код.

<!-- section: examples -->
## Примеры

**Проект из трёх модулей.** Зависимости идут в одну сторону: `report.mjs` -> `inventory.mjs` -> `sku.mjs`. Циклов нет, это важно: при цикле импортёр может увидеть ещё не инициализированную привязку (TDZ).

```javascript
// src/report.mjs
import { Inventory } from "./inventory.mjs";   // расширение обязательно

export function formatReport(inventory) {
  if (!(inventory instanceof Inventory)) throw new TypeError("Ожидался Inventory");
  /* ... */
}
```

**Тест с хуком и проверкой ошибок.**

```javascript
import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { Inventory } from "../src/inventory.mjs";

describe("Inventory", () => {
  let inv;
  beforeEach(() => { inv = new Inventory(); });   // новый склад для каждого теста

  it("remove не уходит в минус", () => {
    inv.add("ABC-0001", 2);
    assert.throws(() => inv.remove("ABC-0001", 3), RangeError);
    assert.equal(inv.stock("ABC-0001"), 2);
  });
});
```

**Разница assert.equal и deepEqual.** `assert.equal([1], [1])` падает: это два разных объекта. `assert.deepEqual([1], [1])` проходит.

```javascript run
const assert = require("node:assert/strict");
const results = [];
for (const [name, check] of [
  ["equal на массивах", () => assert.equal([1], [1])],
  ["deepEqual на массивах", () => assert.deepEqual([1], [1])],
  ["deepEqual 1 и '1'", () => assert.deepEqual([1], ["1"])],
  ["equal NaN и NaN", () => assert.equal(NaN, NaN)],
]) {
  try { check(); results.push(name + ": ok"); } catch { results.push(name + ": падает"); }
}
console.log(results.join("\n"));
```
```output
equal на массивах: падает
deepEqual на массивах: ok
deepEqual 1 и '1': падает
equal NaN и NaN: ok
```

**Асинхронный тест.**

```javascript
import { it } from "node:test";
import assert from "node:assert/strict";

it("withTimeout отклоняется", async () => {
  await assert.rejects(withTimeout(new Promise(() => {}), 10), { message: "timeout" });
});
```

<!-- section: mistakes -->
## Типичные ошибки

* **Опущенное расширение в импорте** (`from "./sku"`). В ESM это `ERR_MODULE_NOT_FOUND`.
* **Смесь `require` и `import` в одном файле.** В ESM нет `require`, а в CommonJS нет статического `import`.
* **Присваивание импортированному имени** (`counter = 5`). Это `TypeError`: привязка только для чтения. Меняйте состояние через функцию экспортирующего модуля.
* **Циклические импорты.** Они работают, но при обращении к ещё не инициализированной привязке получите `ReferenceError`. Выносите общее в третий модуль.
* **Ожидание копии значения.** Экспортированная переменная - живая; сохраните значение в локальную `const`, если нужен «снимок».
* **Тесты, зависящие друг от друга.** Общий изменяемый объект на уровне файла приводит к порядку-зависимым падениям. Создавайте состояние в `beforeEach`.
* **`assert.throws(f(-1), RangeError)`.** Вы вызвали функцию сами, исключение вылетело раньше проверки. Передавайте функцию: `assert.throws(() => f(-1), RangeError)`.
* **Тест без утверждений** или с `async` без `await`: он «проходит», ничего не проверив.
* **Проверка только счастливого пути.** Ошибочные входы и границы дают больше всего багов.
* **`node --test tests/` на Node.js 22.** Каталог как аргумент не работает так, как ожидается; передавайте шаблон файлов в кавычках.

<!-- section: summary -->
## Итоги

* ES-модули имеют статическую структуру, живые привязки, единственный экземпляр на файл, строгий режим и поддерживают top-level `await`.
* Node.js определяет тип модуля по расширению и `package.json`; в ESM пути пишутся с расширением, `require` и `__dirname` недоступны.
* Из CommonJS ES-модули загружают через `import()`; значения `require` копируются, привязки ESM живые.
* `node:test` (`describe`, `it`, хуки) и `node:assert/strict` дают тесты без сторонних пакетов; запускаются через `node --test`.
* Тестируйте поведение, границы и ошибки; делайте тесты независимыми и держите граф модулей без циклов.
