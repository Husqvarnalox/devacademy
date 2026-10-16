// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./lazy_task.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await Promise.race([
      Promise.resolve().then(check),
      new Promise((resolve) => setTimeout(() => resolve("тест не завершился за 3 секунды"), 3000).unref()),
    ]);
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

function expect(actual, want, what) {
  return Object.is(actual, want) ? null : `${what} вернуло ${String(actual)}, ожидалось ${String(want)}`;
}
const same = (a, b, what) => (JSON.stringify(a) === JSON.stringify(b) ? null : `${what}: получено ${JSON.stringify(a)}, ожидалось ${JSON.stringify(b)}`);

function* naturals(log) {
  try {
    for (let i = 1; ; i++) {
      log?.push(`src${i}`);
      yield i;
    }
  } finally {
    log?.push("closed");
  }
}

await test("lazy-basic", () =>
  same(m.lazy([1, 2, 3, 4]).map((x) => x * 10).filter((x) => x > 10).toArray(), [20, 30, 40], "map и filter")
  ?? same(m.lazy([1, 2, 3, 4]).take(2).toArray(), [1, 2], "take(2)"));
await test("lazy-infinite", () =>
  same(m.lazy(naturals()).filter((n) => n % 2 === 0).map((n) => n * n).take(4).toArray(), [4, 16, 36, 64], "первые 4 квадрата чётных из бесконечного источника"));
await test("lazy-iterable", () => {
  const out = [];
  for (const x of m.lazy(naturals()).map((n) => n + 1).take(3)) out.push(x);
  return same(out, [2, 3, 4], "for...of по конвейеру") ?? same([...m.lazy(new Set([1, 2])).map((x) => -x)], [-1, -2], "spread по конвейеру над Set");
});

await test("lazy-order", () => {
  const log = [];
  m.lazy([1, 2, 3])
    .map((x) => { log.push(`m${x}`); return x; })
    .filter((x) => { log.push(`f${x}`); return x !== 2; })
    .toArray();
  return same(log, ["m1", "f1", "m2", "f2", "m3", "f3"], "порядок вызовов (каждый элемент проходит всю цепочку)");
});
await test("lazy-take-stops", () => {
  const log = [];
  const calls = [];
  const res = m.lazy(naturals(log)).map((x) => { calls.push(x); return x; }).take(2).toArray();
  return same(res, [1, 2], "результат") ?? same(calls, [1, 2], "map вызван только для нужных элементов") ?? same(log, ["src1", "src2", "closed"], "источник прочитан на 2 элемента и закрыт");
});
await test("lazy-deferred", () => {
  let touched = false;
  const p = m.lazy({ [Symbol.iterator]() { touched = true; return [1][Symbol.iterator](); } }).map((x) => x).take(1);
  const before = touched;
  p.toArray();
  return expect(before, false, "до обхода источник не трогают") ?? expect(touched, true, "после обхода источник прочитан") ?? same(m.lazy([1, 2]).take(0).toArray(), [], "take(0)");
});
