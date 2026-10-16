<!-- section: simple -->
## События: «подпишись и получай уведомления»

Многие объекты Node.js работают по схеме **издатель - подписчики**: потоки сообщают `'data'` и `'end'`, процесс - `'exit'`, HTTP-сервер - `'request'`. В основе лежит класс **`EventEmitter`** из `node:events`. Вы регистрируете **слушателя** (функцию) на **событие** (строку), а источник вызывает всех слушателей методом `emit`:

```javascript
const { EventEmitter } = require("node:events");
const bus = new EventEmitter();
bus.on("greet", (name) => console.log(`Привет, ${name}`));
bus.emit("greet", "Анна"); // Привет, Анна
```

Три факта, которые важно запомнить с самого начала:

1. **`emit` синхронный.** Слушатели выполняются внутри вызова `emit`, по порядку регистрации, и только потом `emit` возвращает управление.
2. **Событие `'error'` особое.** Если никто на него не подписан, `emit('error', err)` бросит исключение и обычно уронит процесс.
3. **Объект `process`** - глобальный источник событий и сведений о запущенной программе: аргументы, переменные окружения, код завершения, сигналы.

Это та же идея, что слушатели событий в браузере и обработчики в других языках; механизм потоков из прошлых уроков построен именно на ней ([потоки и обратное давление](lesson:nodejs.fundamentals.streams-backpressure)). О `process.stdin` и `process.argv` вы уже слышали ([процесс, потоки и stdin](lesson:nodejs.first-steps.runtime-process-and-stdin)). Сравните с исключениями и завершением ресурсов в Java ([исключения и try-with-resources](lesson:java.fundamentals.exceptions-try-with-resources)).

<!-- section: precise -->
## EventEmitter, error и process

**Методы подписки.** `on(name, fn)` (синоним `addListener`) добавляет слушателя в конец; `prependListener` - в начало; `once` - слушателя, который удаляется перед первым вызовом; `off(name, fn)` (синоним `removeListener`) удаляет конкретную функцию - поэтому анонимную стрелку удалить нельзя, её нужно сохранить в переменной. `listenerCount(name)` и `eventNames()` позволяют заглянуть внутрь. Слушатель получает аргументы `emit`; `this` внутри обычной функции - сам emitter.

**Синхронность и асинхронность.** `emit` вызывает слушатели последовательно и возвращает `true`, если слушатели были. Асинхронный слушатель (`async`-функция) вернёт промис, который `emit` **не ждёт**; необработанное отклонение такого промиса - отдельная проблема. Если слушатель бросает исключение, оно выходит из `emit`, и следующие слушатели не вызываются.

**Событие `'error'`.** Для событий, которые могут завершиться ошибкой (потоки, сокеты, дочерние процессы), принято сообщать об ошибке через `'error'`. Без слушателя `emit('error', err)` бросает `err`, а если передан не `Error`, - `ERR_UNHANDLED_ERROR`. Поэтому у каждого источника событий, который может сломаться, должен быть `.on('error', ...)`.

**Предупреждение об утечке.** Если на одно событие навешено больше 10 слушателей, Node.js печатает `MaxListenersExceededWarning`. Обычно это значит, что подписка делается в цикле или при каждом запросе и не снимается.

**`process.argv` и `process.env`.** `argv` - `[путь к node, путь к скрипту, ...аргументы]`, поэтому пользовательские аргументы - `process.argv.slice(2)`. `env` - переменные окружения (строки), отсутствующая переменная даёт `undefined`. Числа и булевы значения из `env` приходят строками.

**Код завершения.** `0` - успех, любое другое число - ошибка; по нему вызывающая программа (shell, CI) понимает результат. `process.exitCode = n` задаёт код и позволяет программе спокойно завершиться; `process.exit(n)` завершает немедленно, поэтому буферизованный вывод и незавершённые операции могут потеряться. Необработанное исключение завершает процесс с кодом `1`.

**События процесса.** `'beforeExit'` приходит, когда очередь событий опустела (в нём можно запланировать ещё работу); `'exit'` - прямо перед завершением, и в обработчике допустим только **синхронный** код: таймеры и промисы уже не отработают.

**Сигналы.** SIGINT (Ctrl+C) и SIGTERM (остановка сервиса) можно перехватить через `process.on('SIGTERM', handler)`. После подписки процесс сам не завершится: обработчик должен закрыть серверы и соединения. Корректное завершение (graceful shutdown) строится так: поймали сигнал, перестали принимать работу, дождались текущей, вышли с кодом 0. SIGKILL перехватить нельзя.

<!-- section: examples -->
## Примеры

Порядок слушателей, `once`, `off` и возвращаемое значение `emit`.

```javascript run
const { EventEmitter } = require("node:events");
const bus = new EventEmitter();

const first = () => console.log("first");
bus.on("ping", first);
bus.on("ping", () => console.log("second"));
bus.prependListener("ping", () => console.log("zero"));
bus.once("ping", () => console.log("once"));

console.log(bus.emit("ping"), bus.listenerCount("ping"));
bus.off("ping", first);
console.log(bus.emit("ping"), bus.listenerCount("ping"));
console.log(bus.emit("nobody"), bus.eventNames());
```
```output
zero
first
second
once
true 3
zero
second
true 2
false [ 'ping' ]
```

`emit` синхронный и не ждёт асинхронных слушателей.

```javascript run
const { EventEmitter } = require("node:events");
const bus = new EventEmitter();

bus.on("job", async (id) => {
  await null;
  console.log("async listener resumed", id);
});
bus.on("job", (id) => console.log("sync listener", id));

console.log("before emit");
bus.emit("job", 1);
console.log("after emit");
```
```output
before emit
sync listener 1
after emit
async listener resumed 1
```

Событие `'error'`: с обработчиком и без него.

```javascript run
const { EventEmitter } = require("node:events");

const quiet = new EventEmitter();
try {
  quiet.emit("error", new Error("disk full"));
} catch (err) {
  console.log("thrown:", err.message);
}

const careful = new EventEmitter();
careful.on("error", (err) => console.log("handled:", err.message));
console.log(careful.emit("error", new Error("disk full")));

try {
  quiet.emit("error", "just text");
} catch (err) {
  console.log(err.code);
}
```
```output
thrown: disk full
handled: disk full
true
ERR_UNHANDLED_ERROR
```

Свой класс-издатель на наследовании от `EventEmitter`.

```javascript run
const { EventEmitter } = require("node:events");

class Countdown extends EventEmitter {
  constructor(from) {
    super();
    this.from = from;
  }
  run() {
    for (let i = this.from; i > 0; i--) this.emit("tick", i);
    this.emit("done", this.from);
  }
}

const c = new Countdown(3);
c.on("tick", (n) => console.log("tick", n));
c.once("done", (n) => console.log("done after", n));
c.run();
c.run();
```
```output
tick 3
tick 2
tick 1
done after 3
tick 3
tick 2
tick 1
```

Объект `process`: аргументы, окружение, код завершения и события.

```javascript run
console.log(process.argv.length, process.argv.slice(2));
console.log(process.env.DEVACADEMY_SURELY_UNSET);
console.log(Number(process.env.DEVACADEMY_SURELY_UNSET ?? "8080"));

process.on("beforeExit", () => console.log("beforeExit"));
process.on("exit", (code) => console.log("exit", code, process.exitCode));
process.on("SIGTERM", () => {});
console.log(process.listenerCount("SIGTERM"));
setTimeout(() => console.log("timer done"), 0);
console.log("end of script");
```
```output
2 []
undefined
8080
1
end of script
timer done
beforeExit
exit 0 undefined
```

<!-- section: mistakes -->
## Типичные ошибки

* **Нет слушателя `'error'`.** Первая же сетевая или файловая ошибка роняет процесс. Подписывайтесь на `'error'` у потоков, сокетов, серверов.
* **Анонимный слушатель, который потом нужно снять.** `bus.off("e", () => ...)` ничего не удалит: функция другая. Сохраняйте ссылку.
* **Подписка в цикле или в обработчике запроса без отписки.** Слушатели накапливаются, растёт память, появляется `MaxListenersExceededWarning`.
* **Ожидание, что `emit` дождётся `async`-слушателя.** Он не дожидается; ошибки внутри такого слушателя нужно ловить внутри него.
* **Асинхронная работа в `'exit'`.** Таймеры и промисы в этом обработчике не выполнятся. Асинхронную очистку делайте до выхода (например, по сигналу).
* **`process.exit()` посреди записи в stdout.** Вывод может обрезаться. Задайте `process.exitCode` и дайте программе завершиться.
* **Парсить `process.argv` вручную с индексом 0.** Пользовательские аргументы начинаются с индекса 2.
* **Считать, что значение из `process.env` - число.** Это всегда строка или `undefined`.

<!-- section: summary -->
## Итоги

* `EventEmitter` - схема «издатель - подписчики»: `on`, `once`, `prependListener`, `off`, `emit`.
* `emit` синхронный, вызывает слушателей по порядку и возвращает `true`, если они были.
* У `'error'` без слушателя `emit` бросает исключение: подписывайтесь на `'error'`.
* `process.argv.slice(2)` - аргументы пользователя, `process.env` - строки или `undefined`.
* Код завершения задают через `process.exitCode`; `process.exit()` обрывает работу немедленно.
* В `'exit'` только синхронный код; корректное завершение строят на сигналах SIGINT и SIGTERM.
