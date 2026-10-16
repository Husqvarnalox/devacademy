// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./closures.mjs");
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

await test("counter-basic", () => {
  const c = m.makeCounter(5);
  return expect(c.inc(), 6, "inc()") ?? expect(c.inc(), 7, "inc()") ?? expect(c.dec(), 6, "dec()") ?? expect(c.value(), 6, "value()")
    ?? expect(m.makeCounter().value(), 0, "makeCounter().value()");
});
await test("adders", () => same(m.makeAdders(4).map((f) => f()), [0, 1, 2, 3], "makeAdders(4).map(f => f())"));
await test("once-basic", () => {
  let calls = 0;
  const f = m.once((a, b) => { calls++; return a + b; });
  return expect(f(1, 2), 3, "первый вызов") ?? expect(f(10, 20), 3, "второй вызов") ?? expect(calls, 1, "число вызовов fn");
});

await test("counter-independent", () => {
  const a = m.makeCounter(10);
  const b = m.makeCounter(100);
  a.inc();
  b.dec();
  return expect(a.value(), 11, "a.value()") ?? expect(b.value(), 99, "b.value()") ?? expect(a.inc(), 12, "a.inc()");
});
await test("adders-edge", () => {
  const fns = m.makeAdders(3);
  return expect(fns.length, 3, "длина") ?? expect(fns[2](), 2, "fns[2]()") ?? expect(fns[2](), 2, "повторный fns[2]()") ?? expect(m.makeAdders(0).length, 0, "makeAdders(0).length");
});
await test("once-undefined-result", () => {
  let calls = 0;
  const f = m.once(() => { calls++; });
  f(); f(); f();
  const g = m.once(function () { return this.v; });
  const o = { v: 7, g };
  return expect(calls, 1, "число вызовов fn, возвращающей undefined") ?? expect(o.g(), 7, "once сохраняет this");
});
await test("memoize", () => {
  let calls = 0;
  const sq = m.memoize((x) => { calls++; return x * x; });
  return expect(sq(4), 16, "sq(4)") ?? expect(sq(4), 16, "sq(4) повторно") ?? expect(sq(5), 25, "sq(5)") ?? expect(calls, 2, "число вызовов");
});
await test("memoize-keys", () => {
  let calls = 0;
  const id = m.memoize((x) => { calls++; return typeof x; });
  const out = [id(1), id("1"), id(undefined), id(undefined)];
  return same(out, ["number", "string", "undefined", "undefined"], "типы") ?? expect(calls, 3, "число вызовов (1 и \"1\" - разные ключи)");
});
