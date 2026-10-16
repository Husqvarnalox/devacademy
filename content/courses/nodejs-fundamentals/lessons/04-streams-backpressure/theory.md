<!-- section: simple -->
## Потоки: данные порциями, а не целиком

Допустим, нужно обработать лог в десять гигабайт. `fs.readFile` загрузит его в память целиком - и программа упадёт. **Поток** (stream) читает и обрабатывает данные **порциями** (chunk): прочитали кусок, обработали, отдали дальше, забыли. Память остаётся почти постоянной, а обработка начинается, не дожидаясь конца данных.

Потоки в Node.js повсюду: `process.stdin` и `process.stdout`, файлы, сокеты, сжатие, HTTP-запросы и ответы. Виды потоков:

* **Readable** - источник: `fs.createReadStream`, `process.stdin`, `Readable.from(...)`;
* **Writable** - приёмник: `fs.createWriteStream`, `process.stdout`;
* **Duplex** - читается и записывается независимо (сокет);
* **Transform** - Duplex, где выход вычисляется из входа (сжатие, разбор строк).

Потоки соединяют цепочкой: источник → преобразования → приёмник. Если приёмник медленнее источника, данные копятся в памяти. Механизм, который заставляет быстрый источник подождать, называется **обратным давлением** (backpressure). Его понимание отличает работающий код от кода, который падает под нагрузкой.

С потоками вы уже встречались: `process.stdin` в первом уроке читали порциями ([процесс, потоки и stdin](lesson:nodejs.first-steps.runtime-process-and-stdin)). Похожие идеи: итераторы и генераторы Python ([итераторы и генераторы](lesson:python.advanced.iterators-and-generators)), потоки ввода-вывода Java ([текст и ввод-вывод](lesson:java.fundamentals.text-and-io)).

```javascript
// Источник -> преобразование -> приёмник
await pipeline(fs.createReadStream("big.log"), parseLines, fs.createWriteStream("errors.txt"));
```

<!-- section: precise -->
## Устройство потоков, обратное давление и pipeline

**Режимы Readable.** Поток может отдавать данные событием `'data'` (режим потока), по запросу `read()` или, что удобнее всего, через **асинхронную итерацию**: `for await (const chunk of readable)`. Куски - `Buffer` (или строка, если вызван `setEncoding`) в обычном режиме и любые значения (кроме `null`) в **режиме объектов** (`objectMode: true`). Граница куска **не связана** со смыслом данных: строка, слово и многобайтный символ могут быть разрезаны между кусками.

**Буфер и `highWaterMark`.** У каждого потока есть внутренний буфер. Порог `highWaterMark` (в байтах, в режиме объектов - в числе объектов; по умолчанию в Node.js 22: 64 КиБ и 16 объектов) - не жёсткий предел, а сигнал.

**Обратное давление для Writable.** Метод `writable.write(chunk)` всегда принимает данные, но возвращает `false`, когда размер буфера (`writableLength`) достиг `highWaterMark`. Правильный источник после `false` прекращает писать и ждёт события `'drain'`:

```javascript
if (!writable.write(chunk)) await once(writable, "drain");
```

Если игнорировать `false`, данные накапливаются в памяти неограниченно. Readable устроен симметрично: если потребитель не читает, поток перестаёт запрашивать данные у источника. Поэтому цепочка потоков в целом использует память постоянного размера.

**Transform.** Вы наследуете `Transform` и реализуете `_transform(chunk, encoding, callback)`: вызываете `this.push(результат)` ноль или несколько раз, затем `callback()` (или `callback(error)`). Метод `_flush(callback)` вызывается, когда вход закончился: здесь отдают накопленный остаток. Режимы объектов задают отдельно для входа и выхода: `readableObjectMode` и `writableObjectMode`.

**Сборка строк из кусков.** Типичная задача: на вход приходят куски `"he"`, `"llo\nwor"`, `"ld"`. Нужно хранить остаток после последнего `\n`, и при каждом куске отдавать только законченные строки; последнюю, если нет `\n`, отдают в `_flush`. Если куски - `Buffer`, а в тексте есть кириллица, склеивать их через `chunk.toString()` небезопасно: многобайтный символ может оказаться разрезанным. Используйте `StringDecoder` из `node:string_decoder` или `setEncoding("utf8")` у источника.

**`pipeline`.** `pipeline(source, ...transforms, destination)` из `node:stream/promises` соединяет потоки, обеспечивает обратное давление, возвращает промис и при ошибке в любой стадии уничтожает все потоки. Старая цепочка `a.pipe(b).pipe(c)` ошибки не передаёт и потоки не закрывает: ошибка в `b` оставляет `a` и `c` открытыми. В `pipeline` вместо потоков можно ставить **асинхронные генераторы**: `async function* (source) { for await (const chunk of source) yield f(chunk); }`.

**Построчное чтение.** `node:readline` оборачивает читаемый поток: `const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity }); for await (const line of rl) { ... }`. Строки приходят по одной, память не растёт, а `\r\n` считается одним переводом строки. Для небольшого ввода достаточно `text(process.stdin)` из `node:stream/consumers` (так делали в первых уроках), для больших потоков нужна построчная обработка.

**Ошибки и завершение.** Событие `'error'` без обработчика завершает процесс. Конец записи сигнализирует `end()`, событие `'finish'` приходит после обработки всех данных. `stream.finished`/`pipeline` отслеживают завершение и ошибки; `readable.destroy()` обрывает поток.

<!-- section: examples -->
## Примеры

**Пример 1. Источник, генератор-преобразователь и приёмник.**

```javascript run
const { Readable, Writable } = require("node:stream");
const { pipeline } = require("node:stream/promises");

async function main() {
  const out = [];
  const sink = new Writable({
    write(chunk, encoding, callback) {
      out.push(chunk.toString());
      callback();
    },
  });

  await pipeline(
    Readable.from(["node", "streams", "pipeline"]),
    async function* (source) {
      for await (const chunk of source) yield String(chunk).toUpperCase();
    },
    sink,
  );
  console.log(out);
}
main();
```
```output
[ 'NODE', 'STREAMS', 'PIPELINE' ]
```

Источник отдаёт строки, асинхронный генератор преобразует каждую, приёмник собирает результат. Промис `pipeline` выполняется, когда закончили все стадии.

**Пример 2. Обратное давление: `write()` возвращает `false`.**

```javascript run
const { Writable } = require("node:stream");

const sink = new Writable({
  highWaterMark: 5,
  write(chunk, encoding, callback) {
    setImmediate(callback);
  },
});

console.log(sink.write("abc"), sink.writableLength);
console.log(sink.write("de"), sink.writableLength);
console.log(sink.writableNeedDrain);
sink.on("drain", () => {
  console.log("drain, в буфере", sink.writableLength);
});
```
```output
true 3
false 5
true
drain, в буфере 0
```

Первая запись оставила в буфере 3 байта (меньше порога 5) - `true`. После второй стало 5: `false`, поток просит подождать. Событие `drain` приходит, когда приёмник обработал всё, и буфер пуст.

**Пример 3. Строки из кусков: свой Transform и `_flush`.**

```javascript run
const { Readable, Transform } = require("node:stream");
const { pipeline } = require("node:stream/promises");

class Lines extends Transform {
  constructor() {
    super({ readableObjectMode: true });
    this.rest = "";
  }
  _transform(chunk, encoding, callback) {
    const parts = (this.rest + chunk).split("\n");
    this.rest = parts.pop();
    for (const line of parts) this.push(line);
    callback();
  }
  _flush(callback) {
    if (this.rest) this.push(this.rest);
    callback();
  }
}

async function main() {
  const lines = [];
  const source = Readable.from(["he", "llo\nwor", "ld\nlast"], { objectMode: false, encoding: "utf8" });
  const collect = async function* (stream) {
    for await (const line of stream) lines.push(line);
  };
  await pipeline(source, new Lines(), collect);
  console.log(lines);
}
main();
```
```output
[ 'hello', 'world', 'last' ]
```

Куски `"he"`, `"llo\nwor"`, `"ld\nlast"` разрезают строки в произвольных местах. Остаток сохраняется между вызовами, а `last` без перевода строки выходит только в `_flush`.

**Пример 4. Построчное чтение через `readline` и `for await`.**

```javascript run
const readline = require("node:readline");
const { Readable } = require("node:stream");

async function main() {
  const input = Readable.from(["first\r\nsec", "ond\n\nthird"]);
  const rl = readline.createInterface({ input, crlfDelay: Infinity });
  let number = 0;
  for await (const line of rl) {
    number++;
    console.log(number, JSON.stringify(line));
  }
}
main();
```
```output
1 "first"
2 "second"
3 ""
4 "third"
```

`readline` склеивает куски в строки, понимает `\r\n` и отдаёт последнюю строку без перевода строки. С `process.stdin` вместо `Readable.from` программа читает ввод по одной строке.

**Пример 5. Ошибка в стадии уничтожает всю цепочку.**

```javascript run
const { Readable, Transform, Writable } = require("node:stream");
const { pipeline } = require("node:stream/promises");

async function main() {
  const source = Readable.from(["a", "b", "c"]);
  const bad = new Transform({
    transform(chunk, encoding, callback) {
      callback(chunk.toString() === "b" ? new Error("сбой на b") : null, chunk);
    },
  });
  const sink = new Writable({ write: (chunk, encoding, callback) => callback() });
  try {
    await pipeline(source, bad, sink);
  } catch (error) {
    console.log(error.message);
  }
  console.log(source.destroyed, bad.destroyed, sink.destroyed);
}
main();
```
```output
сбой на b
true true true
```

`pipeline` вернул ошибку одной стадии и закрыл все остальные. При ручной цепочке `pipe()` источник и приёмник остались бы открытыми.

<!-- section: mistakes -->
## Типичные ошибки

* **Игнорировать результат `write()`.** Быстрый источник, медленный приёмник - и память растёт без предела. Всегда учитывайте `false` и ждите `'drain'`, либо используйте `pipeline`.
* **Считать кусок равным строке или сообщению.** Границы кусков произвольны. Копите остаток и отдавайте законченные единицы.
* **Забыть `_flush`:** последняя строка без перевода строки пропадает.
* **Склеивать `Buffer` через `+` или `toString()` по кускам:** многобайтные символы ломаются. Используйте `StringDecoder` или `setEncoding("utf8")`.
* **Цепочка `pipe()` вместо `pipeline`:** ошибка не передаётся, потоки не закрываются, возникают утечки дескрипторов.
* **Не вызвать `callback()` в `_transform`:** поток зависает без ошибки. В `_transform` callback обязателен ровно один раз на кусок.
* **Забыть режим объектов:** `this.push("строка")` в потоке без `readableObjectMode` превращается в `Buffer`, а `this.push({})` бросает ошибку.
* **Слушать `'data'` и одновременно итерировать поток:** режимы чтения нельзя смешивать.
* **Использовать потоки там, где данных мало:** `await text(process.stdin)` проще и достаточно для небольшого ввода.

<!-- section: summary -->
## Итоги

* Поток обрабатывает данные порциями с постоянной памятью; виды: Readable, Writable, Duplex, Transform.
* `write()` возвращает `false`, когда буфер достиг `highWaterMark`: источник обязан ждать `'drain'`. Это и есть обратное давление.
* Transform: `_transform` + `push` + `callback`, `_flush` для остатка; режимы объектов задаются отдельно для входа и выхода.
* Куски не совпадают с границами строк и символов: накапливайте остаток, для текста используйте `StringDecoder`.
* `stream/promises.pipeline` соединяет потоки, передаёт ошибки и закрывает всё при сбое; `readline` читает ввод по строкам.

Предыдущий урок: [асинхронные шаблоны](lesson:nodejs.fundamentals.async-patterns). Следующий: [Buffer и кодировки](lesson:nodejs.fundamentals.buffers-encodings).
