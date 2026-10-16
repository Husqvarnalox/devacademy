const { Writable } = require("node:stream");
const { once } = require("node:events");
const { text } = require("node:stream/consumers");

// Приёмник: принимает кусок асинхронно (на следующей итерации цикла). Готов; менять не нужно.
function createSink(highWaterMark, failAt) {
  const state = { accepted: 0, bytes: 0 };
  const sink = new Writable({
    highWaterMark,
    write(chunk, encoding, callback) {
      setImmediate(() => {
        if (failAt === state.accepted + 1) return callback(new Error("sink failed"));
        state.accepted++;
        state.bytes += chunk.length;
        callback();
      });
    },
  });
  return { sink, state };
}

// Пишет буферы указанных размеров (Buffer.alloc(size, 97)) в writable с учётом обратного давления.
// Возвращает { waits, peak }: сколько раз ждали 'drain' и наибольшее writableLength сразу после write().
async function produce(writable, sizes) {
  // TODO: сейчас всё пишется подряд, write() не проверяется, конец потока не ожидается.
  let peak = 0;
  for (const size of sizes) {
    writable.write(Buffer.alloc(size, 97));
    peak = Math.max(peak, writable.writableLength);
  }
  writable.end();
  return { waits: 0, peak };
}

async function main() {
  const lines = (await text(process.stdin)).split("\n").map((l) => l.trim());
  const highWaterMark = Number(lines[0]);
  const sizes = lines[1].split(/\s+/).filter(Boolean).map(Number);
  const failAt = lines[2] ? Number(lines[2]) : 0;

  const { sink, state } = createSink(highWaterMark, failAt);
  try {
    const { waits, peak } = await produce(sink, sizes);
    console.log(`waits=${waits}`);
    console.log(`peak=${peak}`);
    console.log(`bytes=${state.bytes}`);
    console.log(`written=${state.accepted}`);
  } catch (error) {
    console.log(`error: ${error.message}`);
    console.log(`written=${state.accepted}`);
  }
}

main();
