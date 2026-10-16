const { Readable, Transform } = require("node:stream");
const { pipeline } = require("node:stream/promises");
const { text } = require("node:stream/consumers");

// Собирает строки из кусков произвольного размера. Наружу отдаёт строки (объекты).
class LineSplitter extends Transform {
  constructor() {
    super({ readableObjectMode: true });
    this.rest = "";
  }

  _transform(chunk, encoding, callback) {
    // TODO: добавьте chunk к this.rest, отдайте через this.push все законченные строки
    // (без \r в конце), а незаконченный остаток сохраните в this.rest.
    callback();
  }

  _flush(callback) {
    // TODO: когда куски кончились, остаток - это последняя строка (если он не пуст).
    callback();
  }
}

// Нумерует непустые строки (пустые и из одних пробелов пропускает) и в конце печатает итог.
class Numberer extends Transform {
  constructor() {
    super();
    // TODO: на входе объекты-строки, на выходе текст: нужен режим объектов для записи
    this.count = 0;
  }

  _transform(line, encoding, callback) {
    // TODO: this.push(`${номер}: ${строка}\n`)
    callback();
  }

  _flush(callback) {
    // TODO: this.push(`total: ${this.count}\n`)
    callback();
  }
}

function* chunksOf(str, size) {
  for (let i = 0; i < str.length; i += size) yield str.slice(i, i + size);
}

async function main() {
  const input = await text(process.stdin);
  const newline = input.indexOf("\n");
  const size = Number(input.slice(0, newline < 0 ? input.length : newline));
  const body = newline < 0 ? "" : input.slice(newline + 1);

  const source = Readable.from(chunksOf(body, size), { objectMode: false });
  await pipeline(source, new LineSplitter(), new Numberer(), process.stdout);
}

main();
