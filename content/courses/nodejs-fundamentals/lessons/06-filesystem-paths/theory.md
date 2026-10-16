<!-- section: simple -->
## Файлы и пути в Node.js

Почти любая программа читает или пишет файлы: конфигурацию, логи, загрузки пользователей. В Node.js для этого есть модуль **`node:fs`**, а для работы с именами файлов - **`node:path`**. Современный способ - версия с промисами **`node:fs/promises`**: каждая операция возвращает промис, и код читается как обычный последовательный:

```javascript
const fs = require("node:fs/promises");
const text = await fs.readFile("notes.txt", "utf8");
```

Три вещи, которые нужно усвоить сразу:

1. **Относительный путь считается от рабочего каталога процесса** (`process.cwd()`), а не от файла со скриптом. Одна и та же программа, запущенная из разных папок, откроет разные файлы.
2. **Пути нельзя склеивать строками.** Разделители на Windows и Linux различаются, лишние `..` и `//` ломают логику. Для этого есть `path.join`, `path.resolve`, `path.relative`.
3. **Операция может не получиться, и это нормально.** Файла нет, папка уже существует, нет прав. Ошибка - объект с полем `code`: `ENOENT`, `EEXIST`, `EISDIR`. Ветвитесь по коду, а не по тексту сообщения.

Идея знакома по другим языкам: `pathlib` и обработка `FileNotFoundError` в Python ([pathlib, os, shutil, tempfile](lesson:python.stdlib.pathlib-os-shutil-tempfile), [ошибки и файлы](lesson:python.fundamentals.errors-and-files)), потоки и `Path` в Java ([текст и ввод-вывод](lesson:java.fundamentals.text-and-io)). Байты и кодировки файлов - из прошлого урока ([Buffer и кодировки](lesson:nodejs.fundamentals.buffers-encodings)).

<!-- section: precise -->
## fs/promises, path и ошибки

**Три версии API.** В `node:fs` каждая операция есть в трёх видах: колбэчная (`fs.readFile(path, cb)`), синхронная (`fs.readFileSync`) и промисная (`require("node:fs/promises")`). Синхронные блокируют цикл событий: в скрипте запуска это допустимо, в обработчике запросов - нет. Основной выбор - `fs/promises`.

**Основные операции.** `readFile(path, "utf8")` читает файл целиком (без кодировки вернёт `Buffer`); `writeFile` перезаписывает; `appendFile` дописывает; `readdir(path, { withFileTypes: true })` перечисляет каталог и сообщает тип записей; `stat` даёт размер и тип; `mkdir(path, { recursive: true })` создаёт цепочку каталогов; `rename`, `copyFile`, `rm`, `rmdir` перемещают, копируют и удаляют; `mkdtemp(prefix)` создаёт уникальную временную папку. Для больших файлов вместо `readFile` используют потоки ([потоки и обратное давление](lesson:nodejs.fundamentals.streams-backpressure)).

**Коды ошибок.** `ENOENT` - нет файла или каталога; `EEXIST` - уже существует (`mkdir` без `recursive`, `copyFile` с флагом `COPYFILE_EXCL`, открытие с флагом `'wx'`); `EISDIR` - каталог использован как файл; `ENOTDIR` - часть пути не каталог; `ENOTEMPTY` - каталог не пуст (`rmdir`); `EACCES` и `EPERM` - нет прав. Ошибка `fs.rm` для каталога без `recursive` имеет собственный код `ERR_FS_EISDIR`. Поведение отдельных редких случаев зависит от операционной системы, поэтому коды сравнивайте только с теми, что перечислены в документации к операции.

**Пути.** `path.join(...parts)` склеивает и нормализует; `path.resolve(...parts)` строит абсолютный путь, проходя аргументы справа налево, и подставляет `process.cwd()`; `path.relative(from, to)` вычисляет относительный путь; `path.parse`, `path.basename`, `path.extname`, `path.dirname` разбирают имя. Каталог файла со скриптом: `__dirname` в CommonJS и `import.meta.dirname` в ESM.

**Безопасность путей.** Если путь приходит извне (аргумент командной строки, поле запроса), он может содержать `..` или быть абсолютным. Схема защиты: `full = path.resolve(root, input)`, `rel = path.relative(root, full)`; допустимо, если `rel` не равен `..`, не начинается с `..` + `path.sep` и не абсолютный. Это называют защитой от **path traversal**. Символические ссылки внутри папки проверка не учитывает.

**Не проверяйте существование.** Шаблон «`existsSync` - потом открыть» создаёт гонку: между проверкой и операцией файл может измениться. Выполняйте операцию и обрабатывайте ошибку.

**Временные файлы.** `fs.mkdtemp` создаёт уникальную папку; удаляйте её в `finally`: `fs.rm(dir, { recursive: true, force: true })`.

<!-- section: examples -->
## Примеры

Пути: `join`, `resolve`, `relative`, `parse` (относительно корня, без привязки к машине).

```javascript run
const path = require("node:path");
console.log(path.join("a", "b", "..", "c", "file.txt"));
console.log(path.join("/a", "/b"), path.resolve("/a", "/b"));
console.log(path.resolve("/srv/app", "data", "../logs"));
console.log(path.relative("/srv/app", "/srv/app/data/x.txt"));
console.log(path.relative("/srv/app", "/srv/other"));
const p = path.parse("/srv/app/archive.tar.gz");
console.log(p.dir, p.base, p.name, p.ext);
```
```output
a/c/file.txt
/a/b /b
/srv/app/logs
data/x.txt
../other
/srv/app archive.tar.gz archive.tar .gz
```

Запись, дописывание, чтение и сведения о файле во временной папке.

```javascript run
const fs = require("node:fs/promises");
const path = require("node:path");

async function main() {
  const dir = await fs.mkdtemp(path.join(process.cwd(), "demo-"));
  try {
    const file = path.join(dir, "notes.txt");
    await fs.writeFile(file, "Привет\n");
    await fs.appendFile(file, "second line\n");
    const text = await fs.readFile(file, "utf8");
    console.log(JSON.stringify(text));
    const st = await fs.stat(file);
    console.log(st.isFile(), st.size);
    await fs.mkdir(path.join(dir, "sub", "deep"), { recursive: true });
    const entries = await fs.readdir(dir, { withFileTypes: true });
    console.log(entries.map((e) => e.name + (e.isDirectory() ? "/" : "")).sort());
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
  }
}
main();
```
```output
"Привет\nsecond line\n"
true 25
[ 'notes.txt', 'sub/' ]
```

Ошибки определяются по `code`.

```javascript run
const fs = require("node:fs/promises");
const path = require("node:path");

async function codeOf(promise) {
  try {
    await promise;
    return "ok";
  } catch (err) {
    return err.code;
  }
}

async function main() {
  const dir = await fs.mkdtemp(path.join(process.cwd(), "demo-"));
  try {
    const f = path.join(dir, "a.txt");
    const d = path.join(dir, "d");
    await fs.writeFile(f, "x");
    await fs.mkdir(d);
    await fs.writeFile(path.join(d, "inner.txt"), "y");
    console.log(await codeOf(fs.readFile(path.join(dir, "missing.txt"))));
    console.log(await codeOf(fs.mkdir(d)));
    console.log(await codeOf(fs.readFile(d)));
    console.log(await codeOf(fs.rmdir(d)));
    console.log(await codeOf(fs.readdir(f)));
    console.log(await codeOf(fs.copyFile(f, path.join(d, "inner.txt"), fs.constants.COPYFILE_EXCL)));
    console.log(await codeOf(fs.rm(d)));
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
  }
}
main();
```
```output
ENOENT
EEXIST
EISDIR
ENOTEMPTY
ENOTDIR
EEXIST
ERR_FS_EISDIR
```

Защита от выхода за пределы папки.

```javascript run
const path = require("node:path");

function inside(root, input) {
  const rel = path.relative(root, path.resolve(root, input));
  return rel !== ".." && !rel.startsWith(".." + path.sep) && !path.isAbsolute(rel);
}

const root = path.resolve("/srv/files");
for (const input of ["report.txt", "a/../b.txt", ".", "../secret", "a/../../secret", "/etc/passwd", "..hidden/x"]) {
  console.log(input.padEnd(16), inside(root, input));
}
```
```output
report.txt       true
a/../b.txt       true
.                true
../secret        false
a/../../secret   false
/etc/passwd      false
..hidden/x       true
```

<!-- section: mistakes -->
## Типичные ошибки

* **Строить путь конкатенацией.** `dir + "/" + name` ломается на лишних слешах и на Windows. Используйте `path.join`.
* **Считать, что путь относится к файлу скрипта.** Он относится к `process.cwd()`. Для ресурсов рядом со скриптом стройте путь от `__dirname`.
* **Сравнивать текст ошибки.** Сообщения зависят от системы и версии, поле `code` стабильно.
* **Проверять `existsSync` перед чтением.** Лишняя гонка; читайте и обрабатывайте `ENOENT`.
* **Забывать про `finally` у временных данных.** При исключении временная папка останется на диске.
* **Доверять пути из ввода.** Без проверки `..` и абсолютных путей пользователь прочитает любой файл, доступный процессу. Проверка через `startsWith(root)` без разделителя ошибочна: `/srv/files-secret` начинается с `/srv/files`.
* **Читать большие файлы `readFile`.** Весь файл попадёт в память; используйте потоки.
* **Использовать `*Sync` в сервере.** Блокировка цикла событий останавливает все запросы.

<!-- section: summary -->
## Итоги

* Для файлов используйте `node:fs/promises`; синхронные версии подходят только для разовых скриптов.
* Относительные пути считаются от `process.cwd()`, каталог скрипта - `__dirname` или `import.meta.dirname`.
* Пути собирают через `path.join` и `path.resolve`, а проверяют через `path.relative`.
* Ошибки различают по `err.code`: `ENOENT`, `EEXIST`, `EISDIR`, `ENOTDIR`, `ENOTEMPTY`.
* Не проверяйте существование заранее: выполняйте операцию и обрабатывайте ошибку.
* Внешний путь всегда проверяйте на выход за корень; временные папки удаляйте в `finally`.
