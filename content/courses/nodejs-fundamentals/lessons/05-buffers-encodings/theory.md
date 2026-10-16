<!-- section: simple -->
## Buffer: байты, а не символы

Строка в JavaScript - последовательность символов. Файл, сетевой пакет, картинка и вывод другой программы - последовательности **байтов**. Чтобы превратить одно в другое, нужна **кодировка**: правило «символ - байты». Самая распространённая - **UTF-8**: латинская буква занимает один байт, кириллическая - два, иероглиф - три, эмодзи - четыре.

В Node.js байты хранит класс **Buffer** - массив байтов фиксированной длины. Он является подклассом `Uint8Array`, поэтому индексы, `length`, итерация и `for...of` работают как обычно, а сверху добавлены кодировки (`utf8`, `hex`, `base64`, `latin1`) и методы чтения чисел.

Из-за этой разницы `'Привет'.length` равно 6 (символов), а `Buffer.byteLength('Привет')` равно 12 (байтов). Путаница между ними - источник большинства ошибок с юникодом.

Buffer вы уже видели: куски `process.stdin` и файлов без `setEncoding` - это буферы ([потоки и обратное давление](lesson:nodejs.fundamentals.streams-backpressure)). Похожие понятия: `bytes` и `str` в Python ([строки](lesson:python.fundamentals.strings)), байтовые потоки Java ([текст и ввод-вывод](lesson:java.fundamentals.text-and-io)).

```javascript
const buf = Buffer.from("Привет", "utf8");
console.log(buf.length); // 12 байтов
```

<!-- section: precise -->
## Создание, кодировки и числа

**Создание.** `Buffer.from(string, encoding)` кодирует строку; `Buffer.from([1, 2, 3])` создаёт буфер из чисел 0-255; `Buffer.alloc(n)` - n нулевых байтов; `Buffer.concat([a, b])` склеивает буферы в новый. Метод `Buffer.allocUnsafe(n)` не очищает память, поэтому в нём могут лежать чужие старые данные; его используют лишь когда буфер сразу полностью перезаписывается.

**Кодировки.** `buf.toString(encoding)` декодирует, `Buffer.from(str, encoding)` кодирует:

* `utf8` - по умолчанию. Неверные последовательности байтов при декодировании превращаются в символ замены U+FFFD, поэтому произвольные байты в utf8-строку и обратно **не проходят без потерь**;
* `hex` - две шестнадцатеричные цифры на байт;
* `base64` и `base64url` - бинарные данные как текст (три байта в четыре символа; `base64url` заменяет `+` и `/` на `-` и `_` и убирает `=`);
* `latin1` - один байт на символ, коды 0-255.

Декодирование hex и base64 **не сообщает об ошибке**: нечётная длина или посторонний символ молча обрезают результат. Внешние данные проверяйте до декодирования.

**Числа.** Многобайтные числа хранятся в двух порядках: **big-endian** (BE, старший байт первым, «сетевой порядок») и **little-endian** (LE, младший первым). Методы называются `readUInt8`, `readUInt16BE`, `readInt16LE`, `readUInt32BE`, `readInt32LE` и так далее, а парные `write...` пишут число по смещению. Выход за границы буфера бросает `RangeError`, поэтому смещение и длину сначала сравнивают с `buf.length`.

**Срезы.** `buf.subarray(start, end)` возвращает **вид на ту же память** без копирования: запись в срез меняет исходный буфер. Копию делают через `Buffer.from(buf)` или `buf.copy(target)`. Метод `buf.slice()` у Buffer ведёт себя так же, как `subarray`, но считается устаревшим. Сравнивать содержимое нужно `a.equals(b)`, а не `a === b`.

**Протоколы.** Типичный двоичный формат - кадры «тип, длина, данные». Разбор: читаем заголовок, проверяем, что данные целиком в буфере, берём `subarray`, переходим к следующему смещению. Длина из заголовка - недоверенный ввод: без проверки она позволяет прочитать за пределами кадра.

<!-- section: examples -->
## Примеры

Строки и байты: длина в символах и в байтах.

```javascript run
for (const s of ["abc", "Привет", "日本語", "😀"]) {
  console.log(s, s.length, Buffer.byteLength(s), Buffer.from(s).toString("hex"));
}
```
```output
abc 3 3 616263
Привет 6 12 d09fd180d0b8d0b2d0b5d182
日本語 3 9 e697a5e69cace8aa9e
😀 2 4 f09f9880
```

Кодировки одних и тех же байтов, включая потери при неверном utf8.

```javascript run
const buf = Buffer.from("héllo");
console.log(buf.toString("hex"));
console.log(buf.toString("base64"));
console.log(buf.toString("latin1"));
console.log(Buffer.from([0xff, 0x61, 0xc3]).toString("utf8"));
console.log(Buffer.from("zz", "hex").length, Buffer.from("abc", "hex").length);
console.log(Buffer.from([251, 255]).toString("base64"), Buffer.from([251, 255]).toString("base64url"));
```
```output
68c3a96c6c6f
aMOpbGxv
hÃ©llo
�a�
0 1
+/8= -_8
```

Числа в разном порядке байтов.

```javascript run
const b = Buffer.from([0x01, 0x02, 0xff, 0xfe]);
console.log(b.readUInt16BE(0), b.readUInt16LE(0));
console.log(b.readInt16BE(2), b.readInt16LE(2));
console.log(b.readUInt32BE(0), b.readUInt32LE(0));
const out = Buffer.alloc(6);
out.writeUInt16BE(258, 0);
out.writeInt32LE(-2, 2);
console.log(out);
try {
  b.readUInt32BE(2);
} catch (err) {
  console.log(err.code);
}
```
```output
258 513
-2 -257
16973822 4278125057
<Buffer 01 02 fe ff ff ff>
ERR_OUT_OF_RANGE
```

Общая память, копии и склейка.

```javascript run
const base = Buffer.from([1, 2, 3, 4]);
const view = base.subarray(1, 3);
const copy = Buffer.from(view);
view[0] = 99;
console.log(base, view, copy);
console.log(Buffer.concat([base.subarray(0, 2), Buffer.from("A")]));
console.log(base.equals(Buffer.from([1, 99, 3, 4])), base === Buffer.from(base));
```
```output
<Buffer 01 63 03 04> <Buffer 63 03> <Buffer 02 03>
<Buffer 01 63 41>
true false
```

Кадр «тип, длина, данные» руками.

```javascript run
const payload = Buffer.from("Привет");
const frame = Buffer.alloc(3 + payload.length);
frame.writeUInt8(1, 0);
frame.writeUInt16BE(payload.length, 1);
payload.copy(frame, 3);
console.log(frame.toString("hex"));
const type = frame.readUInt8(0);
const len = frame.readUInt16BE(1);
console.log(type, len, frame.subarray(3, 3 + len).toString("utf8"));
```
```output
01000cd09fd180d0b8d0b2d0b5d182
1 12 Привет
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать `str.length` числом байтов.** Для размера в utf8 используйте `Buffer.byteLength(str)`; эмодзи и кириллица занимают больше одного байта.
* **Превращать бинарные данные в utf8-строку.** Изображение или архив после `toString()` и обратно испорчены. Держите их в Buffer или кодируйте в base64 и hex.
* **Склеивать куски потока строкой `data += chunk`.** Многобайтный символ может быть разрезан между кусками. Собирайте буферы и декодируйте после `Buffer.concat`, либо вызовите `setEncoding('utf8')`.
* **Доверять `Buffer.from(str, 'hex')`.** Ошибки нет, результат просто короче. Проверяйте строку регулярным выражением.
* **Путать порядок байтов.** Сетевые протоколы обычно big-endian, процессоры x86 и ARM работают в little-endian. Всегда проверяйте спецификацию формата.
* **Забывать, что `subarray` разделяет память.** Изменение среза меняет исходник; для независимой копии используйте `Buffer.from(view)`.
* **Использовать `allocUnsafe` и читать неинициализированные байты.** В буфере могут оказаться чужие данные.

<!-- section: summary -->
## Итоги

* Строка - символы, Buffer - байты; между ними стоит кодировка.
* `length` строки и `Buffer.byteLength` - разные числа; utf8 хранит символы в 1-4 байтах.
* `Buffer.from`, `alloc`, `concat` создают буферы; `subarray` даёт общий вид, `Buffer.from(buf)` - копию.
* `hex`, `base64`, `base64url`, `latin1` - способы записи байтов текстом; hex и base64 не сообщают об ошибках.
* Числа читают методами `readUInt16BE`, `readInt32LE` и т. п.; всегда проверяйте границы и порядок байтов.
* В двоичных протоколах длина из заголовка - недоверенный ввод: сравнивайте её с остатком буфера.
