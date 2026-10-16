// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./fp_task.mjs");
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

await test("compose-basic", () => {
  const inc = (x) => x + 1;
  const dbl = (x) => x * 2;
  return expect(m.compose(inc, dbl)(5), 11, "compose(inc, dbl)(5)") ?? expect(m.compose(dbl, inc)(5), 12, "compose(dbl, inc)(5)");
});
await test("curry-basic", () => {
  const add3 = m.curry((a, b, c) => a + b + c);
  return expect(add3(1)(2)(3), 6, "add3(1)(2)(3)") ?? expect(add3(1, 2)(3), 6, "add3(1, 2)(3)") ?? expect(add3(1)(2, 3), 6, "add3(1)(2, 3)");
});
await test("store-basic", () => {
  const s = m.createStore(1);
  const seen = [];
  s.subscribe((n, o) => seen.push([n, o]));
  s.set(2);
  s.set((v) => v + 10);
  return expect(s.get(), 12, "get()") ?? same(seen, [[2, 1], [12, 2]], "вызовы подписчика");
});
await test("compose-edge", () => {
  const id = m.compose();
  const f = m.compose((s) => s + "!", (s) => s.trim());
  return expect(id(42), 42, "compose()(42)") ?? expect(f("  hi "), "hi!", "compose с тремя этапами");
});
await test("curry-edge", () => {
  const calls = [];
  const f = m.curry((a, b) => { calls.push([a, b]); return a * b; });
  const times3 = f(3);
  return expect(times3(4), 12, "f(3)(4)") ?? expect(times3(5), 15, "повторное использование f(3)") ?? expect(f(2, 2), 4, "f(2, 2)") ?? same(calls, [[3, 4], [3, 5], [2, 2]], "вызовы исходной функции");
});
await test("store-unsubscribe", () => {
  const s = m.createStore("a");
  let n = 0;
  const off = s.subscribe(() => n++);
  s.set("a");
  s.set("b");
  off();
  s.set("c");
  const s2 = m.createStore(NaN);
  let k = 0;
  s2.subscribe(() => k++);
  s2.set(NaN);
  return expect(n, 1, "число вызовов подписчика (то же значение и после отписки не считаются)") ?? expect(s.get(), "c", "get() после отписки") ?? expect(k, 0, "NaN -> NaN не изменение");
});
