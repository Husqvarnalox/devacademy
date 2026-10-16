<!-- section: simple -->
## Как Node.js подключает код: два модульных формата

Программа редко живёт в одном файле. Чтобы разделить код на файлы, нужна **модульная система**. В Node.js их две, и они существуют одновременно по историческим причинам:

* **CommonJS (CJS)** - появился вместе с Node.js: `const fs = require("node:fs")` и `module.exports = ...`.
* **ES-модули (ESM)** - стандарт языка ECMAScript: `import fs from "node:fs"` и `export`.

Файл `main.js` без каких-либо настроек может оказаться в любой из этих систем. Какая именно, зависит от правил Node.js, а не от «настроения» кода.
Эти правила разобраны ниже. Вторая часть урока - порядок выполнения асинхронного кода: что именно цикл событий выполняет раньше.

```javascript
// CommonJS
const { text } = require("node:stream/consumers");
// ESM
import { text } from "node:stream/consumers";
```

<!-- section: precise -->
## ESM против CommonJS и фазы цикла событий

**Как Node.js выбирает систему для файла.**

1. Расширение `.mjs` - всегда ESM, `.cjs` - всегда CommonJS.
2. Для `.js` смотрят на ближайший `package.json`: `"type": "module"` делает файлы ESM, `"type": "commonjs"` (по умолчанию) - CommonJS.
3. Если `package.json` не задаёт тип, современные версии Node.js анализируют синтаксис: файл с `import` или `export` верхнего уровня выполняется как ESM, иначе как CommonJS.
   Явное указание надёжнее: правило с анализом синтаксиса может вывести предупреждение. Подробности зависят от версии, смотрите документацию вашей версии.

**Отличия.**

| | CommonJS | ESM |
|---|---|---|
| Подключение | `require()` - вызов функции, выполняется на месте | `import` - объявление, разбирается заранее; динамический вариант `import()` возвращает промис |
| Экспорт | `module.exports` / `exports` | `export`, `export default` |
| Связи | `require` возвращает копию значения в момент вызова | импортируемые имена - живые связи с экспортом |
| Режим | нестрогий по умолчанию | всегда строгий (`"use strict"`) |
| `__dirname`, `__filename`, `require`, `module` | есть | нет; вместо них `import.meta.url` и `import.meta.dirname` (в новых версиях) |
| `await` на верхнем уровне | нет, только внутри `async`-функции | есть (top-level await) |

Все импортируемые модули выполняются один раз и кешируются: повторный `require` или `import` того же файла возвращает уже готовый результат.

**Совместимость.** ESM может импортировать CommonJS: `module.exports` становится значением `default`. Подключить ESM из CommonJS-кода традиционно можно только асинхронно, через `import()`.

**Утилиты чтения потока.** Модуль `node:stream/consumers` содержит функции, которые асинхронно собирают весь поток: `text(stream)` возвращает промис со строкой, `json(stream)` - разобранный JSON.
Вместе с top-level await чтение ввода сводится к одной строке: `const input = await text(process.stdin);`.

**Фазы цикла событий.** Один оборот цикла libuv в Node.js проходит по фазам:

1. `timers` - колбэки `setTimeout` и `setInterval`, срок которых наступил;
2. `pending callbacks` - отложенные системные колбэки;
3. `idle, prepare` - внутренние;
4. `poll` - получение событий ввода-вывода и выполнение их колбэков; здесь цикл при необходимости ждёт;
5. `check` - колбэки `setImmediate`;
6. `close callbacks` - например, `socket.on("close")`.

**Очереди между операциями.** Сразу после каждого колбэка (и после основного скрипта), до перехода дальше, Node.js выполняет:
сначала очередь `process.nextTick`, затем **микрозадачи** (колбэки промисов `.then`, продолжения после `await`). Эти очереди прогоняются полностью, поэтому бесконечно перепланируемый `nextTick` или промис не даёт циклу событий дойти до таймеров.

Итоговый порядок для обычного CommonJS-скрипта: синхронный код, `nextTick`, микрозадачи промисов, затем таймеры и остальные макрозадачи.
В ESM-модуле код сам выполняется внутри цепочки промисов, поэтому микрозадачи промисов там обычно идут раньше `nextTick`. Опираться на это различие в программах не стоит.

**Таймеры не точны.** `setTimeout(fn, 10)` означает «не раньше чем через 10 мс». Порядок таймеров с разной задержкой определяется сроками, а сравнение `setTimeout(..., 0)` и `setImmediate` в главном модуле не гарантирован.

<!-- section: examples -->
## Примеры

**Пример 1. Что есть в CommonJS и чего нет в ESM.**

```javascript run
console.log(typeof require, typeof module, typeof exports);
console.log(typeof __filename, typeof __dirname);
console.log(require("node:path").basename(__filename));
```
```output
function object object
string string
main.js
```

Это CommonJS: файл не содержит `import`/`export`, типа в `package.json` нет. Node.js оборачивает код файла в функцию и передаёт ей `require`, `module`, `exports`, `__filename`, `__dirname`.

**Пример 2. Тот же файл как ESM.**

```javascript run
import { basename } from "node:path";

console.log(typeof require, typeof module, typeof __dirname);
console.log(basename(import.meta.url));
```
```output
undefined undefined undefined
main.js
```

Появился `import` на верхнем уровне, и Node.js выполнил файл как ESM. Переменных CommonJS нет, имя файла берут из `import.meta.url`.

**Пример 3. Порядок в CommonJS-скрипте.**

```javascript run
setTimeout(() => console.log("таймер"), 10);
Promise.resolve().then(() => console.log("промис"));
process.nextTick(() => console.log("nextTick"));
console.log("синхронно");
```
```output
синхронно
nextTick
промис
таймер
```

Сначала всегда синхронный код. Затем очередь `nextTick`, потом микрозадачи, затем таймер.

**Пример 4. `await` ставит продолжение в очередь.**

```javascript run
async function main() {
  console.log("1 до await");
  await null;
  console.log("3 после await");
}
main();
console.log("2 после вызова main");
```
```output
1 до await
2 после вызова main
3 после await
```

Функция выполняется синхронно до первого `await`, затем уступает управление. Продолжение будет выполнено как микрозадача, после того как закончится текущий синхронный код.

**Пример 5. Чтение всего ввода одной строкой в ESM.**

```javascript
import { text } from "node:stream/consumers";

const input = await text(process.stdin);
console.log(input.length);
```

<!-- section: mistakes -->
## Типичные ошибки

* **`require is not defined in ES module scope`.** Файл выполняется как ESM, а в нём вызван `require`. Используйте `import` или `createRequire` из `node:module`.
* **`Cannot use import statement outside a module`.** Файл выполняется как CommonJS (старая версия Node.js, `.cjs` или `"type": "commonjs"`). Переименуйте в `.mjs` или добавьте `"type": "module"`.
* **`__dirname is not defined` в ESM.** Используйте `import.meta.dirname` (в новых версиях) или `fileURLToPath(import.meta.url)`.
* **Ждать, что `await` на верхнем уровне есть везде.** В CommonJS он вызывает `SyntaxError`; нужна `async`-функция или переход на ESM.
* **Считать, что `setTimeout(fn, 0)` выполнится сразу.** Колбэк идёт после синхронного кода, `nextTick` и промисов.
* **Рассчитывать на точный порядок `setTimeout(..., 0)` и `setImmediate`.** В главном модуле он зависит от загруженности процесса. Если нужен порядок, выстраивайте его явно через промисы.
* **Бесконечно перепланировать `process.nextTick`.** Очередь `nextTick` выполняется целиком до остальных задач, и цикл событий «голодает».
* **Игнорировать ошибки в асинхронном коде.** Необработанный отказ промиса завершает процесс с ошибкой. Ставьте `try/catch` вокруг `await` или `.catch`.

<!-- section: summary -->
## Итоги

* Файл `.js` выполняется как CommonJS или ESM в зависимости от расширения, `package.json` и (если тип не задан) синтаксиса.
* В ESM нет `require`, `__dirname`, `module`; есть `import`, `export`, `import.meta` и `await` на верхнем уровне.
* Цикл событий делится на фазы (`timers`, `poll`, `check`, ...). После каждого колбэка выполняются очередь `nextTick` и микрозадачи промисов.
* Для обычного скрипта порядок такой: синхронный код, `nextTick`, промисы, таймеры.
* `stream/consumers` собирает весь поток одним `await`, не оставляя ручной работы с порциями.

Предыдущий урок: [процесс, потоки и stdin](lesson:nodejs.first-steps.runtime-process-and-stdin).
Язык: [равенство и ссылки в JavaScript](lesson:javascript.first-steps.values-and-equality).
