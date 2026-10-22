// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./closures_check.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

const eq = (got, want, label) => (Object.is(got, want) ? null : `${label}: получено ${String(got)}, ожидалось ${String(want)}`);

await test("counter-basic", () => {
  const c = m.makeCounter(10, 5);
  return eq(c.inc(), 15, "inc()") ?? eq(c.inc(), 20, "второй inc()") ?? eq(c.dec(), 15, "dec()") ?? eq(c.value(), 15, "value()");
});
await test("once-basic", () => {
  let calls = 0;
  const f = m.once((x) => { calls++; return x * 2; });
  return eq(f(21), 42, "первый вызов") ?? eq(f(1), 42, "второй вызов") ?? eq(calls, 1, "число вызовов fn");
});
await test("bind-basic", () => {
  const obj = { name: "Ада", greet(g) { return `${g}, ${this.name}`; } };
  const f = m.bindMethod(obj, "greet");
  return eq(f("привет"), "привет, Ада", "bindMethod(obj, \"greet\")(\"привет\")");
});

await test("counter-independent", () => {
  const a = m.makeCounter();
  const b = m.makeCounter(100, 10);
  const { inc, value, reset, dec } = a;
  inc(); inc();
  b.inc();
  return (
    eq(value(), 2, "value() первого счётчика") ??
    eq(b.value(), 110, "value() второго счётчика") ??
    eq(dec(), 1, "dec() без this") ??
    eq(reset(), 0, "reset()") ??
    eq(a.value(), 0, "value() после reset") ??
    eq(b.reset(), 100, "reset() второго") ??
    eq(m.makeCounter().value(), 0, "значение по умолчанию") ??
    eq(m.makeCounter(5).inc(), 6, "шаг по умолчанию")
  );
});
await test("once-edge", () => {
  let calls = 0;
  const f = m.once(function (a, b) { calls++; return this?.k === undefined ? undefined : this.k + a + b; });
  const obj = { f, k: 10 };
  const r1 = obj.f(1, 2);
  const r2 = obj.f(100, 200);
  const u = m.once(() => { calls++; });
  u(); u(); u();
  const g1 = m.once(() => "g1");
  const g2 = m.once(() => "g2");
  return eq(r1, 13, "this и аргументы первого вызова") ?? eq(r2, 13, "повторный вызов") ?? eq(calls, 2, "fn с результатом undefined вызвана больше одного раза") ?? eq(g1() + g2(), "g1g2", "независимость once");
});
await test("bind-late", () => {
  const obj = { n: 1, get(k = 0) { return this.n + k; } };
  const f = m.bindMethod(obj, "get");
  const first = f(1);
  obj.get = function (k = 0) { return this.n * 100 + k; };
  const second = f(2);
  const holder = { call: f };
  const third = holder.call(3);
  let err = null;
  try { m.bindMethod({}, "nope")(); } catch (e) { err = e; }
  return eq(first, 2, "до замены") ?? eq(second, 102, "после замены метода") ?? eq(third, 103, "вызов через другой объект") ?? eq(err instanceof TypeError, true, "TypeError для отсутствующего метода");
});
