// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

// Собственные таймеры харнесса и учёт таймеров, созданных кодом ученика.
const realSetTimeout = globalThis.setTimeout;
const realClearTimeout = globalThis.clearTimeout;
const pending = new Set();
globalThis.setTimeout = (fn, ms, ...args) => {
  const id = realSetTimeout(() => {
    pending.delete(id);
    fn(...args);
  }, ms);
  pending.add(id);
  return id;
};
globalThis.clearTimeout = (id) => {
  pending.delete(id);
  return realClearTimeout(id);
};
const sleep = (ms) => new Promise((resolve) => realSetTimeout(resolve, ms));
const later = (ms, value) => new Promise((resolve) => realSetTimeout(() => resolve(value), ms));

let m = null;
let loadError = null;
try {
  m = await import("./async_check.mjs");
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

const eq = (got, want, label) => {
  const g = JSON.stringify(got), w = JSON.stringify(want);
  return g === w ? null : `${label}: получено ${g}, ожидалось ${w}`;
};
const settle = (p) => Promise.resolve(p).then((value) => ({ ok: true, value }), (error) => ({ ok: false, error }));

await test("pool-basic", async () => {
  const tasks = [() => later(40, "a"), () => later(5, "b"), () => later(20, "c"), () => "d"];
  return eq(await m.promisePool(tasks, 2), ["a", "b", "c", "d"], "результаты promisePool");
});
await test("timeout-ok", async () => {
  let seen = null;
  const value = await m.withTimeout((signal) => { seen = signal; return later(5, "готово"); }, 5000);
  return (
    eq(value, "готово", "результат withTimeout") ??
    eq(seen instanceof AbortSignal, true, "task получает AbortSignal") ?? eq(seen.aborted, false, "signal не отменён после успеха")
  );
});

await test("pool-limit", async () => {
  let active = 0, max = 0;
  const task = (ms, value) => async () => {
    active++;
    max = Math.max(max, active);
    await later(ms, null);
    active--;
    return value;
  };
  const tasks = Array.from({ length: 6 }, (_, i) => task(15, i));
  const results = await m.promisePool(tasks, 3);
  const log = [];
  const logged = (name, ms) => async () => {
    log.push(`start ${name}`);
    await later(ms, null);
    log.push(`end ${name}`);
    return name;
  };
  await m.promisePool([logged("slow", 150), logged("x", 5), logged("y", 5), logged("z", 5)], 2);
  return (
    eq(results, [0, 1, 2, 3, 4, 5], "результаты") ?? eq(max, 3, "максимум одновременных задач при limit = 3") ??
    eq(log.indexOf("start z") < log.indexOf("end slow"), true, "задача z должна стартовать, не дожидаясь медленной slow (скользящее окно)")
  );
});
await test("pool-error", async () => {
  const started = [];
  const boom = new Error("сбой");
  const tasks = [
    () => { started.push(0); return later(40, "ok"); },
    () => { started.push(1); return later(5, null).then(() => { throw boom; }); },
    () => { started.push(2); return "c"; },
    () => { started.push(3); return "d"; },
  ];
  const r = await settle(m.promisePool(tasks, 2));
  await sleep(150);
  return (
    eq(r.ok, false, "пул должен быть отклонён") ?? eq(r.error === boom, true, "пул отклоняется ошибкой задачи") ??
    eq(started, [0, 1], "после ошибки новые задачи не запускаются")
  );
});
await test("pool-edge", async () => {
  const empty = await m.promisePool([], 3);
  const mixed = await m.promisePool([() => 1, () => Promise.resolve(2), async () => 3], 1);
  const order = [];
  await m.promisePool([() => { order.push("a"); return later(10, 1); }, () => { order.push("b"); return 2; }], 1);
  const wide = await m.promisePool([() => later(5, "x"), () => "y"], 10);
  const bad = [];
  for (const limit of [0, -1, 1.5, "2", undefined]) {
    let p;
    try { p = m.promisePool([() => 1], limit); } catch (e) { bad.push(`синхронное исключение при limit = ${String(limit)}`); continue; }
    const r = await settle(p);
    if (r.ok || !(r.error instanceof RangeError)) bad.push(`limit = ${String(limit)} не отклонил промис RangeError`);
  }
  return eq(empty, [], "пустой список") ?? eq(mixed, [1, 2, 3], "обычные и асинхронные значения") ?? eq(order, ["a", "b"], "limit = 1 запускает по одной") ?? eq(wide, ["x", "y"], "limit больше числа задач") ?? (bad.length ? bad.join("; ") : null);
});
await test("timeout-fires", async () => {
  let signalSeen = null;
  const slow = (signal) => {
    signalSeen = signal;
    return later(400, "поздно");
  };
  const r = await settle(m.withTimeout(slow, 20));
  const r2 = await settle(m.withTimeout(() => later(400, "поздно"), 20));
  return (
    eq(r.ok, false, "withTimeout должен отклониться по тайм-ауту") ?? eq(r.error?.name, "TimeoutError", "name ошибки") ??
    eq(signalSeen?.aborted, true, "signal должен быть отменён") ?? eq(r2.error?.name, "TimeoutError", "name ошибки без использования signal")
  );
});
await test("timeout-clean", async () => {
  pending.clear();
  await m.withTimeout(() => "быстро", 60000);
  const afterOk = pending.size;
  const r = await settle(m.withTimeout(() => Promise.reject(new RangeError("внутренняя")), 60000));
  const afterFail = pending.size;
  const sync = await settle(m.withTimeout(() => { throw new SyntaxError("синхронно"); }, 60000));
  const afterSync = pending.size;
  for (const id of pending) realClearTimeout(id);
  pending.clear();
  return (
    eq(afterOk, 0, "после успешной задачи остались таймеры") ?? eq(r.error?.name, "RangeError", "ошибка задачи пробрасывается как есть") ??
    eq(afterFail, 0, "после отклонённой задачи остались таймеры") ?? eq(sync.error?.name, "SyntaxError", "синхронное исключение задачи становится отказом") ??
    eq(afterSync, 0, "после синхронного исключения остались таймеры")
  );
});

for (const id of pending) realClearTimeout(id);
