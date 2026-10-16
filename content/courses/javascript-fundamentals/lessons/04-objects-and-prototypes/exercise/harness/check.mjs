// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./objects.mjs");
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

await test("readonly-basic", () => {
  const o = m.defineReadOnly({}, "id", 7);
  const d = Object.getOwnPropertyDescriptor(o, "id");
  if (!d) return "свойство id не определено";
  return expect(d.value, 7, "значение") ?? expect(d.writable, false, "writable") ?? expect(d.enumerable, true, "enumerable") ?? expect(d.configurable, false, "configurable");
});
await test("chain-length", () =>
  expect(m.chainLength({}), 1, "chainLength({})") ?? expect(m.chainLength([]), 2, "chainLength([])") ?? expect(m.chainLength(function () {}), 2, "chainLength(function)"));
await test("temperature-basic", () => {
  const t = new m.Temperature(100);
  return expect(t.celsius, 100, "celsius") ?? expect(t.fahrenheit, 212, "fahrenheit") ?? expect(m.Temperature.fromFahrenheit(32).celsius, 0, "fromFahrenheit(32).celsius")
    ?? expect(m.Temperature.fromFahrenheit(212) instanceof m.Temperature, true, "fromFahrenheit возвращает Temperature");
});
await test("copy-basic", () => {
  const a = { user: { tags: ["x"] }, n: 1 };
  const b = m.copyState(a);
  b.user.tags.push("y");
  b.n = 2;
  return same(a, { user: { tags: ["x"] }, n: 1 }, "оригинал после изменения копии") ?? same(b, { user: { tags: ["x", "y"] }, n: 2 }, "копия");
});

await test("readonly-strict", () => {
  const o = m.defineReadOnly({ a: 1 }, "k", "v");
  let assign = "не бросило";
  try { o.k = "other"; } catch (e) { assign = e.name; }
  let del = "не бросило";
  try { delete o.k; } catch (e) { del = e.name; }
  return expect(assign, "TypeError", "присваивание в строгом режиме") ?? expect(del, "TypeError", "delete в строгом режиме")
    ?? same(Object.keys(o), ["a", "k"], "Object.keys") ?? expect(o.k, "v", "значение не изменилось");
});
await test("chain-special", () => {
  const base = { a: 1 };
  const mid = Object.create(base);
  const leaf = Object.create(mid);
  class A {}
  class B extends A {}
  return expect(m.chainLength(Object.create(null)), 0, "chainLength(Object.create(null))") ?? expect(m.chainLength(leaf), 3, "три уровня")
    ?? expect(m.chainLength(new B()), 3, "chainLength(new B()) (B.prototype, A.prototype, Object.prototype)") ?? expect(m.chainLength(Object.create(Object.prototype)), 1, "chainLength(Object.create(Object.prototype))");
});
await test("temperature-validation", () => {
  const bad = (f) => { try { f(); return "не бросило"; } catch (e) { return e.name; } };
  const t = new m.Temperature(20);
  const r1 = bad(() => new m.Temperature(-300));
  const r2 = bad(() => { t.celsius = -273.16; });
  const r3 = bad(() => m.Temperature.fromFahrenheit(-1000));
  t.celsius = -273.15;
  return expect(r1, "RangeError", "new Temperature(-300)") ?? expect(r2, "RangeError", "t.celsius = -273.16") ?? expect(r3, "RangeError", "fromFahrenheit(-1000)")
    ?? expect(t.celsius, -273.15, "граничное значение допустимо") ?? expect(t.celsius !== undefined && new m.Temperature(5).celsius, 5, "celsius нового объекта");
});
await test("temperature-private", () => {
  const t = new m.Temperature(10);
  const desc = Object.getOwnPropertyDescriptor(t, "celsius");
  return same(Object.getOwnPropertyNames(t), [], "собственные свойства экземпляра (значение должно быть в #celsius)") ?? expect(desc, undefined, "celsius - аксессор в прототипе")
    ?? expect(typeof Object.getOwnPropertyDescriptor(m.Temperature.prototype, "fahrenheit")?.get, "function", "геттер fahrenheit в прототипе")
    ?? expect(Object.getOwnPropertyDescriptor(m.Temperature.prototype, "fahrenheit")?.set, undefined, "у fahrenheit нет сеттера");
});
await test("copy-types", () => {
  const src = { when: new Date(0), m: new Map([["a", [1]]]), s: new Set([1, 2]) };
  const c = m.copyState(src);
  c.m.get("a").push(2);
  c.s.add(3);
  return expect(c.when instanceof Date, true, "Date остался Date") ?? expect(c.when === src.when, false, "Date скопирован")
    ?? expect(src.m.get("a").length, 1, "Map в оригинале не изменился") ?? expect(src.s.size, 2, "Set в оригинале не изменился") ?? expect(c.s.size, 3, "Set в копии");
});
