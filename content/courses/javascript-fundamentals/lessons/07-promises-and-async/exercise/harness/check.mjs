// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

process.on("unhandledRejection", () => {});
const delay = (ms) => new Promise((r) => setTimeout(r, ms));
let m = null;
let loadError = null;
try {
  m = await import("./async_task.mjs");
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

await test("sleep", async () => {
  const t0 = Date.now();
  const v = await m.sleep(30, "готово");
  const dt = Date.now() - t0;
  return expect(v, "готово", "значение sleep") ?? (dt >= 25 ? null : `sleep(30) завершился слишком рано: ${dt} мс`);
});
await test("parallel-basic", async () => {
  const events = [];
  const out = await m.parallelMap([1, 2, 3], async (x) => {
    events.push(`start${x}`);
    await delay(30 - x * 5);
    events.push(`end${x}`);
    return x * 10;
  });
  return same(out, [10, 20, 30], "результаты в исходном порядке") ?? same(events.slice(0, 3), ["start1", "start2", "start3"], "все вызовы должны начаться до первого завершения");
});
await test("sequential-basic", async () => {
  const events = [];
  const out = await m.sequentialMap(["a", "b", "c"], async (x, i) => {
    events.push(`start${x}${i}`);
    await delay(10);
    events.push(`end${x}${i}`);
    return x + i;
  });
  return same(out, ["a0", "b1", "c2"], "результаты") ?? same(events, ["starta0", "enda0", "startb1", "endb1", "startc2", "endc2"], "порядок событий");
});
await test("timeout-fires", async () => {
  const t0 = Date.now();
  let err;
  try { await m.withTimeout(delay(400), 30); } catch (e) { err = e; }
  const dt = Date.now() - t0;
  return expect(err instanceof Error && err.message, "timeout", "отказ по таймауту") ?? (dt < 300 ? null : `withTimeout не уложился в таймаут: ${dt} мс`);
});

await test("timeout-passthrough", async () => {
  const ok = await m.withTimeout(m.sleep(10, "v"), 500);
  const boom = new Error("boom");
  let err;
  try { await m.withTimeout(Promise.reject(boom), 500); } catch (e) { err = e; }
  return expect(ok, "v", "значение пробрасывается") ?? expect(err, boom, "причина отказа пробрасывается без изменений");
});
await test("sequential-stops", async () => {
  const started = [];
  const boom = new Error("стоп");
  let err;
  try {
    await m.sequentialMap([1, 2, 3, 4], async (x) => { started.push(x); if (x === 2) throw boom; return x; });
  } catch (e) { err = e; }
  await delay(20);
  return expect(err, boom, "ошибка") ?? same(started, [1, 2], "после ошибки следующие элементы не запускаются");
});
await test("parallel-error-and-index", async () => {
  const seen = [];
  const out = await m.parallelMap(["x", "y"], (v, i) => { seen.push([v, i]); return Promise.resolve(i); });
  const boom = new Error("p");
  let err;
  try { await m.parallelMap([1, 2], (x) => (x === 2 ? Promise.reject(boom) : delay(5))); } catch (e) { err = e; }
  return same(out, [0, 1], "fn получает индекс вторым аргументом") ?? same(seen, [["x", 0], ["y", 1]], "вызовы") ?? expect(err, boom, "Promise.all отклоняется первой ошибкой")
    ?? same(await m.parallelMap([], (x) => x), [], "пустой массив");
});
await test("settle-all", async () => {
  const r = await m.settleAll([m.sleep(20, "a"), Promise.reject(new Error("e1")), Promise.resolve("b"), delay(5).then(() => { throw new Error("e2"); })]);
  return same(r, { fulfilled: ["a", "b"], rejected: ["e1", "e2"] }, "settleAll") ?? same(await m.settleAll([]), { fulfilled: [], rejected: [] }, "пустой вход");
});
