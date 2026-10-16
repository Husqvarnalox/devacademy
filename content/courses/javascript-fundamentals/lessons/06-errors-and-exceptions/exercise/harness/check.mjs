// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./errors.mjs");
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

const thrown = (f) => { try { f(); } catch (e) { return e; } return undefined; };

await test("error-classes", () => {
  const v = new m.ValidationError("плохо", "age");
  const c = new m.ConfigError("конфиг");
  return expect(v instanceof Error, true, "ValidationError наследует Error") ?? expect(v.name, "ValidationError", "v.name") ?? expect(v.field, "age", "v.field")
    ?? expect(v.message, "плохо", "v.message") ?? expect(c instanceof Error && c.name === "ConfigError", true, "ConfigError: наследник Error с name");
});
await test("parse-ok", () => {
  const cfg = m.parseConfig('{"port": 8080, "host": "localhost"}');
  return same(cfg, { port: 8080, host: "localhost" }, "parseConfig");
});
await test("retry-basic", () => {
  const seen = [];
  const out = m.retry((n) => { seen.push(n); if (n < 3) throw new Error("fail " + n); return "ok" + n; }, 5);
  return expect(out, "ok3", "результат") ?? same(seen, [1, 2, 3], "номера попыток");
});
await test("cleanup-basic", () => {
  let cleaned = 0;
  const out = m.withCleanup(() => 42, () => cleaned++);
  return expect(out, 42, "результат") ?? expect(cleaned, 1, "cleanup вызван один раз");
});

await test("parse-invalid-json", () => {
  const e = thrown(() => m.parseConfig("{oops"));
  return expect(e instanceof m.ConfigError, true, "ошибка - ConfigError") ?? expect(e.message, "Некорректный JSON", "message") ?? expect(e.cause instanceof SyntaxError, true, "cause - исходная SyntaxError");
});
await test("parse-validation", () => {
  const bad = ['{"port": 0}', '{"port": 70000}', '{"port": "80"}', '{"port": 80.5}', "{}", "[]", "null", "42"];
  for (const text of bad) {
    const e = thrown(() => m.parseConfig(text));
    if (!(e instanceof m.ValidationError)) return `parseConfig(${text}) должен бросать ValidationError, получено: ${e?.name ?? "ничего"}`;
    if (e.field !== "port") return `parseConfig(${text}): field === ${JSON.stringify(e.field)}, ожидалось "port"`;
  }
  return expect(m.parseConfig('{"port": 65535}').port, 65535, "граничный порт 65535") ?? expect(m.parseConfig('{"port": 1}').port, 1, "граничный порт 1");
});
await test("retry-exhausted", () => {
  let calls = 0;
  const last = new Error("последняя");
  const e = thrown(() => m.retry((n) => { calls++; throw n === 3 ? last : new Error("раньше " + n); }, 3));
  return expect(calls, 3, "число попыток") ?? expect(e, last, "бросается ошибка последней попытки") ?? expect(thrown(() => m.retry(() => 1, 0))?.name, "RangeError", "retry(fn, 0)")
    ?? expect(m.retry(() => undefined, 1), undefined, "успех с результатом undefined") ?? expect(thrown(() => m.retry(() => 1, 1.5))?.name, "RangeError", "retry(fn, 1.5)");
});
await test("cleanup-throws", () => {
  let cleaned = 0;
  const original = new Error("исходная");
  const e1 = thrown(() => m.withCleanup(() => { throw original; }, () => { cleaned++; throw new Error("из cleanup"); }));
  const own = new Error("только cleanup");
  const e2 = thrown(() => m.withCleanup(() => 1, () => { throw own; }));
  return expect(e1, original, "исходная ошибка не должна теряться") ?? expect(cleaned, 1, "cleanup вызван один раз") ?? expect(e2, own, "если fn успешна, пробрасывается ошибка cleanup");
});
