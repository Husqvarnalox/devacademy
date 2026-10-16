<!-- section: simple -->
## Три поколения асинхронного кода

Всё, что в Node.js ждёт внешнего мира (файл, сеть, таймер), асинхронно: функция сразу возвращает управление, а результат приходит позже. Способов его получить сложилось три, и в реальных проектах вы встретите все:

1. **Колбэки «ошибка первой»** - исходный стиль Node.js: `fs.readFile(path, (err, data) => { ... })`. Первым аргументом всегда идёт ошибка (или `null`), результат - вторым.
2. **Промисы** - объект «результат будет позже» с цепочками `.then`/`.catch`. Решают проблему «лестницы» из вложенных колбэков и единообразно передают ошибки.
3. **`async`/`await`** - запись тех же промисов в виде обычного последовательного кода с `try/catch`.

Язык и устройство промисов разобраны в уроке [«Цикл событий, промисы и async/await»](lesson:javascript.fundamentals.promises-and-async); исключения - в [«Ошибки и исключения»](lesson:javascript.fundamentals.errors-and-exceptions). Этот урок - о приёмах, которые нужны именно в Node.js: переход от колбэков, выбор комбинатора, ошибки, которые завершают процесс, и **отмена** операций.

Те же задачи в других языках: отмена и таймауты в Python ([отмена и таймауты](lesson:python.concurrency.cancellation-and-timeouts)), контекст отмены в Go ([`context`](lesson:go.fundamentals.concurrency-patterns-and-context)), `async`/`await` в C# ([async и await](lesson:csharp.fundamentals.async-await)). В JavaScript отмена выглядит как «сигнал», который вы передаёте операции и который она сама проверяет.

```javascript
// колбэк
fs.readFile("a.txt", "utf8", (err, text) => { /* ... */ });
// промис
const text = await fs.promises.readFile("a.txt", "utf8");
```

<!-- section: precise -->
## Переход между стилями, комбинаторы, ошибки и отмена

**Из колбэка в промис.** `util.promisify(fn)` оборачивает функцию, последним аргументом принимающую колбэк «ошибка первой». Для встроенных модулей готовые промис-версии лежат в `node:fs/promises`, `node:timers/promises`, `node:stream/promises`, `node:readline/promises`. Вручную: `new Promise((resolve, reject) => legacy(x, (err, r) => err ? reject(err) : resolve(r)))`. Обратная сторона: `util.callbackify`.

**Последовательно и параллельно.** `await a(); await b();` запускает `b` только после `a`. Чтобы запустить сразу, сначала создайте промисы, затем ждите: `const [x, y] = await Promise.all([a(), b()])`. `await` внутри `array.map(async ...)` не ждёт ничего: получится массив промисов, оберните его в `Promise.all`. Бесконтрольный `Promise.all` над тысячей элементов запускает тысячу операций сразу; число одновременных операций ограничивают (смотрите вызов в упражнении-вызове).

**Комбинаторы.**

| Комбинатор | Выполняется, когда | Отклоняется, когда |
|---|---|---|
| `Promise.all` | выполнены все; значение - массив результатов в порядке входа | отказал любой (первая ошибка) |
| `Promise.allSettled` | всегда после завершения всех; массив `{status, value}` или `{status, reason}` | никогда |
| `Promise.race` | первый завершившийся выполнился | первый завершившийся отклонился |
| `Promise.any` | первый **успешный** | отказали все: `AggregateError` с полем `errors` |

Остальные промисы комбинатор не отменяет: они продолжают работать. Если нужна настоящая остановка, используйте сигнал отмены.

**Ошибки.** Отказ промиса - это исключение для `await`: его ловят `try/catch`, а `finally` выполняется в любом случае. Исходную ошибку сохраняют в `cause`: `throw new Error("не удалось прочитать конфиг", { cause: error })`. Цепочка `.then(...)` без `.catch` и `await` без `try` пропускают ошибку вверх по цепочке. Если отказ нигде не обработан, Node.js (с версии 15, режим `--unhandled-rejections=throw`) превращает его в необработанное исключение: процесс завершается с ненулевым кодом, если нет слушателя `process.on("unhandledRejection", ...)`. Колбэк в `setTimeout` или `EventEmitter`, внутри которого бросили исключение, ломает процесс так же: `try/catch` вокруг вызова `setTimeout` его не ловит.

**Отмена: `AbortController` и `AbortSignal`.**

* `const controller = new AbortController()`; `controller.signal` передают операции; `controller.abort(reason)` отменяет. После этого `signal.aborted === true`, `signal.reason` хранит причину (по умолчанию `DOMException` с именем `AbortError`), сигнал вызывает событие `abort`.
* Параметр `{ signal }` принимают `fs`-промисы, `timers/promises`, `events.once`, потоки, `fetch`, `child_process` и другие API. Отменённая операция отклоняется ошибкой `AbortError` (`error.name === "AbortError"`, `error.code === "ABORT_ERR"`).
* Готовые сигналы: `AbortSignal.timeout(ms)` (отменится по таймеру с причиной `TimeoutError`), `AbortSignal.abort(reason)` (уже отменённый), `AbortSignal.any([a, b])` (отменится, когда отменится любой из них).
* Отмена **кооперативная**: свою функцию вы пишете так, чтобы она проверяла `signal.throwIfAborted()` между шагами или подписывалась на `abort` и освобождала ресурсы. Если вы приняли `signal`, ваш промис должен отклониться при отмене.

**Ограничение параллелизма.** Типичная схема - пул: запускаете не больше `limit` задач и стартуете следующую при завершении любой; при ошибке останавливаете запуск новых и отменяете общий сигнал. Эту схему вы реализуете в упражнении-вызове.

<!-- section: examples -->
## Примеры

**Пример 1. Колбэк в промис.**

```javascript run
const { promisify } = require("node:util");

function legacyDouble(x, callback) {
  setImmediate(() => {
    if (typeof x !== "number") return callback(new TypeError("нужно число"));
    callback(null, x * 2);
  });
}

const double = promisify(legacyDouble);

async function main() {
  console.log(await double(21));
  try {
    await double("a");
  } catch (error) {
    console.log(error.name, error.message);
  }
}
main();
```
```output
42
TypeError нужно число
```

`promisify` превращает `callback(err, result)` в выполнение или отказ промиса; `await` возвращает значение или бросает исключение.

**Пример 2. Четыре комбинатора на одних данных.**

```javascript run
const ok = (value) => Promise.resolve(value);
const fail = (message) => Promise.reject(new Error(message));

async function main() {
  console.log(await Promise.all([ok(1), ok(2)]));
  const settled = await Promise.allSettled([ok(1), fail("плохо")]);
  console.log(settled.map((r) => (r.status === "fulfilled" ? r.value : r.reason.message)), settled.map((r) => r.status));
  console.log(await Promise.any([fail("a"), ok("победитель"), fail("b")]));
  try {
    await Promise.any([fail("a"), fail("b")]);
  } catch (error) {
    console.log(error.constructor.name, error.errors.map((e) => e.message));
  }
  try {
    await Promise.all([ok(1), fail("первая ошибка"), fail("вторая ошибка")]);
  } catch (error) {
    console.log(error.message);
  }
}
main();
```
```output
[ 1, 2 ]
[ 1, 'плохо' ] [ 'fulfilled', 'rejected' ]
победитель
AggregateError [ 'a', 'b' ]
первая ошибка
```

`allSettled` не отклоняется и показывает исходы всех; `any` игнорирует отказы, пока есть успех; `all` сообщает первую ошибку и теряет остальные.

**Пример 3. `race` без таймеров: быстрее тот, у кого меньше шагов.**

```javascript run
async function steps(name, count) {
  for (let i = 0; i < count; i++) await null;
  return name;
}

async function main() {
  console.log(await Promise.race([steps("медленный", 5), steps("быстрый", 1)]));
  const slow = steps("медленный", 5);
  slow.then(() => console.log("медленный всё равно закончил"));
  await Promise.race([slow, steps("быстрый", 1)]);
  console.log("race вернул управление");
}
main();
```
```output
быстрый
race вернул управление
медленный всё равно закончил
```

`race` не отменяет проигравшего: он доработал до конца. Порядок здесь определён только потому, что шаги - микрозадачи; для таймеров он зависит от времени.

**Пример 4. Ошибки: `cause`, `finally` и необработанный отказ.**

```javascript run
process.on("unhandledRejection", (reason) => {
  console.log("unhandledRejection:", reason.message);
});

async function loadConfig() {
  try {
    JSON.parse("{ не json");
  } catch (error) {
    throw new Error("не удалось прочитать конфиг", { cause: error });
  } finally {
    console.log("finally выполнен");
  }
}

loadConfig().catch((error) => console.log(error.message, "<-", error.cause.name));
Promise.reject(new Error("никто не ждёт"));
```
```output
finally выполнен
не удалось прочитать конфиг <- SyntaxError
unhandledRejection: никто не ждёт
```

`cause` хранит исходную ошибку. Отказ без обработчика попал в `unhandledRejection`; без этого слушателя процесс упал бы с кодом 1.

**Пример 5. Отмена.**

```javascript run
import { setTimeout as sleep } from "node:timers/promises";

const controller = new AbortController();
controller.signal.addEventListener("abort", () => console.log("событие abort"));
console.log(controller.signal.aborted);
controller.abort(new Error("пора остановиться"));
console.log(controller.signal.aborted, controller.signal.reason.message);

try {
  await sleep(10_000, "не дождёмся", { signal: controller.signal });
} catch (error) {
  console.log(error.name, error.code);
}

const timeout = AbortSignal.timeout(50);
const either = AbortSignal.any([timeout, new AbortController().signal]);
console.log(either.aborted);
console.log(AbortSignal.abort().reason.name);
```
```output
false
событие abort
true пора остановиться
AbortError ABORT_ERR
false
AbortError
```

Таймер на 10 секунд не запускается: сигнал уже отменён, и промис отклоняется сразу с `AbortError` и кодом `ABORT_ERR`. У `AbortSignal.abort()` причина по умолчанию - `AbortError`.

<!-- section: mistakes -->
## Типичные ошибки

* **Забытый `await`.** Функция вернёт промис, а ошибка уйдёт в «воздух» и станет необработанным отказом. Включите правило линтера `no-floating-promises` или всегда обрабатывайте результат.
* **`await` в `forEach`/`map` без `Promise.all`.** `forEach` не ждёт колбэков; после `map(async ...)` ждите `Promise.all(...)`.
* **Лишняя последовательность.** Два независимых `await` подряд удваивают время; запускайте оба и ждите `Promise.all`.
* **Неограниченный параллелизм.** `Promise.all` по тысяче файлов исчерпает дескрипторы или пул из 4 потоков; ограничивайте число одновременных операций.
* **Думать, что `Promise.race`/`any` останавливают остальных.** Они лишь выбирают исход; остановка нужна через `AbortSignal`.
* **Смешивать колбэки и промисы в одной функции:** вызывать колбэк и возвращать промис, терять ошибку в `try`, вызывать колбэк дважды. Выберите один стиль.
* **Бросать исключение в колбэке таймера или события и надеяться на внешний `try/catch`.** Он не сработает; оберните тело колбэка.
* **Игнорировать `signal` в своих функциях.** Принятый, но не используемый сигнал создаёт иллюзию отмены; проверяйте `signal.throwIfAborted()` и подписывайтесь на `abort`.
* **Проглатывать причину:** `throw new Error("ошибка")` вместо `{ cause }` теряет трассу.

<!-- section: summary -->
## Итоги

* Колбэки «ошибка первой» превращают в промисы через `util.promisify` или готовые `node:*/promises`.
* Независимые операции запускайте вместе (`Promise.all`), зависимые - по очереди; ограничивайте число одновременных.
* `all` - всё или ничего, `allSettled` - исходы всех, `race` - самый быстрый исход, `any` - первый успех; проигравшие не отменяются.
* Необработанный отказ в Node.js 22 завершает процесс; ошибки обогащайте через `cause`.
* Отмена - `AbortController`/`AbortSignal`: `abort(reason)`, `signal.aborted`, `signal.reason`, `AbortSignal.timeout`, `AbortSignal.any`; обязанность остановиться лежит на получателе сигнала.

Предыдущий урок: [модули](lesson:nodejs.fundamentals.modules-esm-cjs). Следующий: [потоки и обратное давление](lesson:nodejs.fundamentals.streams-backpressure).
