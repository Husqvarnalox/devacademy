// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./this_task.mjs");
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

await test("bind-method", () => {
  const user = { name: "Анна", greet(greeting) { return `${greeting}, ${this.name}`; } };
  const g = m.bindMethod(user, "greet");
  const { call } = Function.prototype;
  return expect(g("Привет"), "Привет, Анна", 'bindMethod(user, "greet")("Привет")') ?? expect(call.call(g, { name: "Другой" }, "Эй"), "Эй, Анна", "this подменить нельзя");
});
await test("partial-basic", () => {
  const add3 = m.partial((a, b, c) => a + b + c, 1, 2);
  return expect(add3(3), 6, "partial(f, 1, 2)(3)") ?? expect(m.partial((...xs) => xs.length)(), 0, "partial без предустановленных");
});
await test("logger-basic", () => {
  const log = new m.Logger("app");
  return expect(log.log("старт"), "app: старт", "log('старт')") ?? same(log.logAll(["a", "b"]), ["app: a", "app: b"], "logAll(['a','b'])");
});

await test("bind-late-lookup", () => {
  const obj = { n: 1, get() { return this.n; } };
  const f = m.bindMethod(obj, "get");
  const first = f();
  obj.get = function () { return this.n * 100; };
  const detached = f;
  return expect(first, 1, "первый вызов") ?? expect(detached(), 100, "метод должен искаться в момент вызова");
});
await test("partial-this", () => {
  const obj = {
    k: 10,
    sum: m.partial(function (a, b) { return this.k + a + b; }, 1),
  };
  return expect(obj.sum(2), 13, "obj.sum(2) (this - это obj)") ?? expect(m.partial(function () { return this?.tag; }).call({ tag: "x" }), "x", "partial.call({tag})");
});
await test("logger-all-subclass", () => {
  class Loud extends m.Logger {
    log(msg) { return super.log(msg).toUpperCase(); }
  }
  const l = new Loud("p");
  return same(l.logAll(["ab", "c"]), ["P: AB", "P: C"], "подкласс переопределяет log") ?? same(new m.Logger("x").logAll([]), [], "logAll([])");
});
await test("logger-this-preserved", () => {
  const l = new m.Logger("q");
  const msgs = ["1", "2", "3"];
  const out = l.logAll(msgs);
  return same(out, ["q: 1", "q: 2", "q: 3"], "порядок и содержимое") ?? same(msgs, ["1", "2", "3"], "исходный массив не изменён");
});
