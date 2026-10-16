<!-- section: simple -->
## Исключение - это способ прервать обычный ход программы

Когда что-то идёт не так, код может **бросить** исключение оператором `throw`. Выполнение немедленно покидает текущую функцию и поднимается по стеку вызовов, пока не встретит ближайший `try ... catch`. Если такого нет, программа аварийно завершается (в Node.js) или ошибка попадает в консоль (в браузере).

```javascript run
function divide(a, b) {
  if (b === 0) throw new RangeError("деление на ноль");
  return a / b;
}
try {
  console.log(divide(6, 3));
  console.log(divide(1, 0));
  console.log("сюда не дойдём");
} catch (error) {
  console.log(error.name + ": " + error.message);
} finally {
  console.log("finally выполняется всегда");
}
```
```output
2
RangeError: деление на ноль
finally выполняется всегда
```

Бросать можно любое значение (`throw "oops"`, `throw 42`), но делать это не нужно: только объект класса `Error` (или его наследника) хранит **стек вызовов** и стандартные поля `name` и `message`. Выбрасывайте всегда `Error`.

<!-- section: precise -->
## Типы ошибок, cause, try/catch/finally и свои классы

### Встроенные типы

Спецификация ECMAScript определяет базовый `Error` и наследников ([ECMAScript: Error Objects](https://tc39.es/ecma262/#sec-error-objects)):

* `TypeError` - операция применена к значению не того типа (`undefined.x`, вызов не функции);
* `RangeError` - значение вне допустимого диапазона (`new Array(-1)`, `(1).toFixed(101)`);
* `ReferenceError` - обращение к необъявленному имени или к переменной в TDZ;
* `SyntaxError` - ошибка разбора кода или данных (`JSON.parse("{")`, `new RegExp("(")`);
* `URIError`, `EvalError` - редкие;
* `AggregateError` - несколько ошибок сразу (его бросает `Promise.any`).

Поля `name` и `message` стандартны. Свойство `stack` (текст стека) нестандартно по формату, но есть во всех основных движках.

### Любое значение можно бросить

`throw` принимает любое выражение. В `catch (e)` поэтому может оказаться что угодно: проверяйте `e instanceof Error` перед чтением `e.message`. Типизированного `catch`, как в Java, нет: ловят всё и разбирают вручную, а неизвестные ошибки **пробрасывают дальше** (`throw e`).

### Причина ошибки: cause

С ECMAScript 2022 конструктор принимает второй аргумент `{ cause }`: исходная ошибка сохраняется в `error.cause`, и цепочка причин не теряется при «оборачивании» ([MDN: Error cause](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Error/cause)).

```javascript run
function loadConfig(text) {
  try {
    return JSON.parse(text);
  } catch (error) {
    throw new Error("Не удалось прочитать конфигурацию", { cause: error });
  }
}
try {
  loadConfig("{не json");
} catch (error) {
  console.log(error.message);
  console.log(error.cause instanceof SyntaxError, error.cause.name);
}
```
```output
Не удалось прочитать конфигурацию
true SyntaxError
```

### Семантика try/catch/finally

* `finally` выполняется **всегда**: при нормальном завершении, после `catch`, при `return`, `break`, `continue` и при необработанном исключении.
* Если в `finally` есть `return` (или `throw`), он **заменяет** результат или исключение из `try`/`catch`. Исходная ошибка при этом молча теряется. Не пишите `return` в `finally`.
* Исключение из `catch` или `finally` заменяет предыдущее.
* Параметр `catch` необязателен: `catch { ... }`.
* Переменная в `catch` видна только в блоке `catch`.

```javascript run
function tryFinally() {
  try {
    return "из try";
  } finally {
    console.log("finally выполнился до выхода");
  }
}
function finallyOverrides() {
  try {
    throw new Error("потерянная ошибка");
  } finally {
    return "из finally";
  }
}
console.log(tryFinally());
console.log(finallyOverrides());
```
```output
finally выполнился до выхода
из try
из finally
```

### Свои классы ошибок

Наследуйтесь от `Error`, вызывайте `super(message, options)` и задавайте `name`. Тогда работают `instanceof`, `stack` и `cause`, а в логах видно тип.

```javascript run
class HttpError extends Error {
  constructor(status, message, options) {
    super(message, options);
    this.name = "HttpError";
    this.status = status;
  }
}
const e = new HttpError(404, "не найдено");
console.log(e instanceof HttpError, e instanceof Error, String(e), e.status);
console.log(JSON.stringify(e), Object.keys(e));
```
```output
true true HttpError: не найдено 404
{"name":"HttpError","status":404} [ 'name', 'status' ]
```

Обратите внимание: `message` и `stack` неперечисляемы, поэтому `JSON.stringify(e)` содержит только собственные перечисляемые поля (`name`, `status`). Для логов формируйте объект вручную.

<!-- section: examples -->
## Примеры

**Ловим только свои ошибки, остальные пробрасываем.**

```javascript run
class NotFound extends Error {
  name = "NotFound";
}
function find(id) {
  if (id === 1) return { id };
  if (id === 2) throw new NotFound("нет записи " + id);
  throw new TypeError("id должен быть известным");
}
for (const id of [1, 2, 3]) {
  try {
    console.log("нашли", find(id));
  } catch (error) {
    if (error instanceof NotFound) console.log("мягкая обработка:", error.message);
    else console.log("пробросили бы дальше:", error.name);
  }
}
```
```output
нашли { id: 1 }
мягкая обработка: нет записи 2
пробросили бы дальше: TypeError
```

**Throw не-Error: чего лишаемся.**

```javascript run
try { throw "просто строка"; } catch (e) { console.log(typeof e, e instanceof Error, e.stack); }
try { throw new Error("настоящая"); } catch (e) { console.log(typeof e, e instanceof Error, typeof e.stack); }
```
```output
string false undefined
object true string
```

**Порядок выполнения блоков.**

```javascript run
function order() {
  const log = [];
  try {
    log.push("try");
    throw new Error("x");
  } catch (e) {
    log.push("catch");
    return log;
  } finally {
    log.push("finally");
  }
}
console.log(order());
```
```output
[ 'try', 'catch', 'finally' ]
```

Массив `log` один и тот же, поэтому `finally` дописал в него уже после `return`. Для примитивов такого эффекта нет.

**Очистка ресурса.**

```javascript run
function withResource(use) {
  const resource = { open: true };
  try {
    return use(resource);
  } finally {
    resource.open = false;
    console.log("ресурс закрыт");
  }
}
try {
  withResource(() => { throw new Error("сбой в работе"); });
} catch (e) {
  console.log("поймали:", e.message);
}
```
```output
ресурс закрыт
поймали: сбой в работе
```

<!-- section: mistakes -->
## Типичные ошибки

* **`throw "строка"` и `throw {code: 1}`.** Нет стека и `instanceof Error`. Бросайте `Error`.
* **Пустой `catch`.** Проглоченные ошибки превращают баги в загадки. Хотя бы логируйте, лучше пробрасывайте.
* **`return` или `throw` в `finally`.** Затирает реальный результат или ошибку.
* **Потеря причины.** `throw new Error("Ошибка конфигурации")` внутри `catch` без `{ cause }` теряет оригинал и его стек.
* **Забытое `name` в своём классе.** В логах вместо `HttpError` будет `Error`.
* **Чтение `e.message` из `catch` без проверки типа.** Брошено может быть что угодно, `e` может быть `undefined`.
* **`try/catch` вокруг асинхронного кода без `await`.** Исключение из промиса не попадёт в обычный `catch` (подробнее в уроке про промисы).
* **`JSON.stringify(error)` для логов.** Получите `{}` или почти пустой объект.
* **Исключения для обычного потока управления** (например, для выхода из цикла). Это медленнее и запутывает чтение.

<!-- section: summary -->
## Итоги

* `throw` прерывает выполнение и поднимается по стеку до ближайшего `catch`; бросайте только `Error` и наследников.
* Типы `TypeError`, `RangeError`, `SyntaxError` и другие различаются по причине; новый тип создают наследованием и `name`.
* `new Error(message, { cause })` сохраняет первопричину при оборачивании ошибок.
* `finally` выполняется всегда; `return`/`throw` в нём перекрывают результат или ошибку блока `try`.
* В `catch` проверяйте тип ошибки (`instanceof`), а неизвестные ошибки пробрасывайте дальше.
