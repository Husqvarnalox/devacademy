<!-- section: simple -->
## Однопоточный JavaScript и очередь задач

JavaScript выполняет ваш код в **одном потоке**: в каждый момент идёт одна функция. Чтобы не блокироваться на ожидании (таймер, сеть, чтение файла), код **откладывает продолжение**: «когда будет готово, вызови эту функцию». Готовые к запуску функции ждут в очередях, а **цикл событий** (event loop) берёт их по одной, когда стек вызовов пуст.

**Промис** (`Promise`) - объект, представляющий результат, который появится позже: он либо выполнится значением (*fulfilled*), либо отклонится ошибкой (*rejected*). `async`/`await` - более удобная запись тех же промисов.

```javascript run
function wait(ms, value) {
  return new Promise((resolve) => setTimeout(() => resolve(value), ms));
}
async function main() {
  console.log("начало");
  const a = await wait(20, "A");
  const b = await wait(10, "B");
  console.log("получили", a, b);
}
main();
console.log("main вернулся сразу");
```
```output
начало
main вернулся сразу
получили A B
```

Что к чему относится. Промисы, `async`/`await`, `queueMicrotask` - часть языка и платформы; `setTimeout`, `setImmediate`, `process.nextTick` и сам цикл событий - возможности **среды выполнения** (браузер, Node.js): в ECMAScript определены только «задания» (jobs), а порядок вызова очередей определяет среда ([ECMAScript: Jobs and Host Operations](https://tc39.es/ecma262/#sec-jobs)).

<!-- section: precise -->
## Цикл событий, микро- и макрозадачи, async/await

### Модель выполнения

* **Стек вызовов**: выполняется синхронный код до конца, прервать его нельзя.
* **Очередь макрозадач** (macrotasks, «tasks»): таймеры (`setTimeout`), события ввода-вывода, события интерфейса. Цикл берёт **одну** макрозадачу, выполняет её, затем очищает микрозадачи.
* **Очередь микрозадач** (microtasks): обработчики промисов (`then`, `catch`, `finally`, продолжения после `await`) и `queueMicrotask`. Они выполняются **сразу после текущей задачи, до следующей макрозадачи**, причём очередь опустошается полностью, включая задачи, добавленные по ходу дела.
* Следствие: промисы «обгоняют» таймеры, даже с нулевой задержкой. А бесконечная цепочка микрозадач **не даёт выполниться** ни таймеру, ни рендерингу.

### Порядок в Node.js

В Node.js дополнительно есть очередь `process.nextTick`. Это **не часть** ECMAScript: её колбэки выполняются *раньше* промисных микрозадач, как только завершилась текущая операция ([Node.js: The Node.js Event Loop](https://nodejs.org/en/learn/asynchronous-work/event-loop-timers-and-nexttick)). Порядок для CommonJS-скрипта:

1. синхронный код;
2. очередь `process.nextTick`;
3. микрозадачи промисов и `queueMicrotask`;
4. макрозадачи: таймеры, затем остальные фазы цикла (`setImmediate` и др.).

Нюанс: в ES-модуле код верхнего уровня сам выполняется внутри промисной задачи, поэтому промисные колбэки там могут обгонять `process.nextTick`. Поэтому порядок `nextTick` и промисов не стоит использовать как основу программы. Порядок `setTimeout(..., 0)` и `setImmediate` в основном модуле Node.js не гарантирован.

```javascript run
console.log("1 sync");
setTimeout(() => console.log("timeout 0"), 0);
Promise.resolve().then(() => console.log("then 1")).then(() => console.log("then 2"));
queueMicrotask(() => console.log("queueMicrotask"));
process.nextTick(() => console.log("nextTick"));
(async () => {
  console.log("async start");
  await null;
  console.log("after await");
})();
console.log("2 sync");
```
```output
1 sync
async start
2 sync
nextTick
then 1
queueMicrotask
after await
then 2
timeout 0
```

Разберите вывод: сначала синхронная часть (включая начало `async`-функции до первого `await`), затем `nextTick`, затем микрозадачи в порядке постановки (`then 1`, `queueMicrotask`, `after await`), потом `then 2`, добавленный по ходу, и только потом макрозадача `timeout 0`.

### Промисы

* Промис переходит из *pending* в *fulfilled* или *rejected* **один раз**; дальнейшие `resolve`/`reject` игнорируются.
* Колбэк в `new Promise(executor)` выполняется синхронно; обработчики `then` - всегда асинхронно (в микрозадаче), даже если промис уже выполнен.
* `then` возвращает **новый** промис. Значение, возвращённое из обработчика, становится результатом; возвращённый промис «разворачивается»; брошенное исключение отклоняет новый промис.
* `catch(f)` - это `then(undefined, f)`; `finally(f)` выполняется в любом исходе и не меняет результат (если сам не бросит).

### async/await

Функция `async` всегда возвращает промис: `return x` выполняет его значением `x`, `throw e` отклоняет. `await p` приостанавливает *эту функцию* (а не поток!) до завершения `p`, а продолжение запускается как микрозадача; остальной код тем временем работает. Исключение, пришедшее из отклонённого промиса, выбрасывается на месте `await`, поэтому его можно ловить обычным `try/catch`. Эти конструкции описаны в ECMAScript как «async function» и «await» ([MDN: async function](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/async_function)) и по сути являются синтаксисом над промисами и генераторами.

```javascript run
const fail = (ms) => new Promise((_, reject) => setTimeout(() => reject(new Error("сбой")), ms));

async function withAwait() {
  try {
    await fail(5);
  } catch (error) {
    return "await+try: " + error.message;
  }
}
const withThen = () => fail(5).catch((error) => "then+catch: " + error.message);

withAwait().then(console.log);
withThen().then(console.log);
```
```output
await+try: сбой
then+catch: сбой
```

### Необработанные отказы

Промис, отклонённый без обработчика, - **unhandled rejection**. В Node.js начиная с 15-й версии режим по умолчанию - `throw`: отказ превращается в необработанное исключение, и процесс завершается с кодом 1, если нет обработчика `process.on("unhandledRejection")` или `uncaughtException` ([Node.js: --unhandled-rejections](https://nodejs.org/api/cli.html#--unhandled-rejectionsmode)). В браузерах такое событие только пишется в консоль (`unhandledrejection`). Поэтому каждый промис должен быть либо ожидаем (`await`/`return`), либо иметь `catch`.

```javascript run
process.once("unhandledRejection", (reason) => {
  console.log("необработанный отказ:", reason.message);
});
Promise.reject(new Error("никто не поймал"));
console.log("код продолжает работать");
```
```output
код продолжает работать
необработанный отказ: никто не поймал
```

### Комбинаторы

* `Promise.all(list)` - массив значений, когда выполнились **все**; отклоняется *первой* ошибкой (остальные операции не отменяются).
* `Promise.allSettled(list)` - ждёт всех, возвращает `{ status, value | reason }` для каждого, не отклоняется.
* `Promise.race(list)` - исход **первого завершившегося** (успех или ошибка).
* `Promise.any(list)` - первый **успех**; если успехов нет, отклоняется `AggregateError`.

<!-- section: examples -->
## Примеры

**Последовательно или параллельно.** Независимые операции нужно запускать вместе. Ниже порядок событий показывает разницу: в параллельном варианте все старты идут подряд.

```javascript run
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const log = [];
async function task(name, ms) {
  log.push("старт " + name);
  await wait(ms);
  log.push("конец " + name);
}
async function main() {
  await task("a", 20);
  await task("b", 10);
  log.push("---");
  await Promise.all([task("c", 40), task("d", 10)]);
  console.log(log.join("\n"));
}
main();
```
```output
старт a
конец a
старт b
конец b
---
старт c
старт d
конец d
конец c
```

**Комбинаторы.**

```javascript run
const later = (ms, value, fail = false) =>
  new Promise((resolve, reject) => setTimeout(() => (fail ? reject(new Error(value)) : resolve(value)), ms));

async function main() {
  console.log(await Promise.all([later(20, "a"), later(10, "b")]));
  console.log(await Promise.race([later(20, "медленный"), later(10, "быстрый")]));
  console.log(await Promise.any([later(5, "x", true), later(15, "y")]));
  const settled = await Promise.allSettled([later(5, "ok"), later(10, "bad", true)]);
  console.log(settled.map((r) => r.status + ":" + (r.value ?? r.reason.message)));
  try {
    await Promise.all([later(20, "поздно"), later(5, "ранняя ошибка", true)]);
  } catch (error) {
    console.log("Promise.all отклонён:", error.message);
  }
  try {
    await Promise.any([later(5, "e1", true), later(10, "e2", true)]);
  } catch (error) {
    console.log(error.constructor.name, error.errors.map((e) => e.message));
  }
}
main();
```
```output
[ 'a', 'b' ]
быстрый
y
[ 'fulfilled:ok', 'rejected:bad' ]
Promise.all отклонён: ранняя ошибка
AggregateError [ 'e1', 'e2' ]
```

**Ловушка forEach и await.**

```javascript run
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
async function main() {
  const out = [];
  [1, 2, 3].forEach(async (n) => {
    await wait(50);
    out.push("forEach " + n);
  });
  out.push("после forEach");
  for (const n of [1, 2, 3]) {
    await wait(1);
    out.push("for-of " + n);
  }
  await wait(100);
  console.log(out.join(" | "));
}
main();
```
```output
после forEach | for-of 1 | for-of 2 | for-of 3 | forEach 1 | forEach 2 | forEach 3
```

**Ручное управление промисом.**

```javascript run
const { promise, resolve } = Promise.withResolvers();
promise.then((v) => console.log("получено", v));
setTimeout(() => resolve(42), 5);
console.log("ждём");
```
```output
ждём
получено 42
```

`Promise.withResolvers()` - часть ECMAScript 2024 (в Node.js 22 доступна).

<!-- section: mistakes -->
## Типичные ошибки

* **Забытый `await`.** Функция вернёт промис вместо значения, а ошибки уйдут в unhandled rejection.
* **Последовательные `await` для независимых операций.** Время складывается, хотя могло бы перекрываться: используйте `Promise.all`.
* **`forEach(async ...)`.** `forEach` не дожидается промисов. Берите `for...of` с `await` или `Promise.all(arr.map(...))`.
* **`try/catch` без `await`.** `try { return fetchData(); } catch {}` не поймает отказ: промис возвращается раньше, чем отклоняется. Нужно `return await` (или `.catch`).
* **Принятие `Promise.all` за отмену.** Другие операции продолжают выполняться после первой ошибки. Для устойчивого сбора результатов - `allSettled`.
* **Опора на порядок `nextTick`/`setImmediate`/таймеров** как на гарантию. Это детали среды.
* **Блокировка цикла событий.** Длинный синхронный цикл останавливает всё, включая таймеры и ответы на запросы. Тяжёлую работу делят на части или выносят в воркеры.
* **Бесконечный `Promise.resolve().then(...)` в цикле** не даёт отработать таймерам (голодание макрозадач).
* **Создание промиса вокруг промиса** (`new Promise(r => r(fetch()))` без нужды): достаточно вернуть исходный промис.

<!-- section: summary -->
## Итоги

* JavaScript однопоточный; цикл событий выполняет задачи по очереди, после каждой макрозадачи очищая очередь микрозадач.
* Обработчики промисов и продолжения после `await` - микрозадачи, они обгоняют `setTimeout(…, 0)`; `process.nextTick` - особенность Node.js и идёт раньше промисов (в CommonJS).
* `async`-функция возвращает промис; `await` приостанавливает функцию, не поток; ошибки ловятся `try/catch`.
* В Node.js 22 необработанный отказ промиса по умолчанию завершает процесс.
* `Promise.all`/`allSettled`/`race`/`any` - четыре способа ждать несколько операций; независимые операции запускайте параллельно.
