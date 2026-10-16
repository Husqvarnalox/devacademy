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
    const parts = (this.rest + chunk.toString("utf8")).split("\n");
    this.rest = parts.pop();
    for (const part of parts) this.push(part.replace(/\r$/, ""));
    callback();
  }

  _flush(callback) {
    if (this.rest !== "") this.push(this.rest.replace(/\r$/, ""));
    callback();
  }
}

// Нумерует непустые строки и в конце печатает итог.
class Numberer extends Transform {
  constructor() {
    super({ writableObjectMode: true });
    this.count = 0;
  }

  _transform(line, encoding, callback) {
    if (line.trim() !== "") {
      this.count++;
      this.push(`${this.count}: ${line}\n`);
    }
    callback();
  }

  _flush(callback) {
    this.push(`total: ${this.count}\n`);
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
