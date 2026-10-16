// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

process.on("unhandledRejection", () => {});
const delay = (ms) => new Promise((r) => setTimeout(r, ms));
let m = null;
let loadError = null;
try {
  m = await import("./limit_task.mjs");
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

function tracker(durations) {
  const state = { active: 0, max: 0, started: [] };
  const fn = async (x, i) => {
    state.active++;
    state.max = Math.max(state.max, state.active);
    state.started.push(i);
    await delay(durations?.[i] ?? 15);
    state.active--;
    return x * 2;
  };
  return { state, fn };
}

await test("limit-basic", async () => {
  const { state, fn } = tracker();
  const out = await m.mapLimit([1, 2, 3, 4, 5, 6], 2, fn);
  return same(out, [2, 4, 6, 8, 10, 12], "результаты") ?? expect(state.max, 2, "максимум одновременных задач");
});
await test("limit-order", async () => {
  const { fn } = tracker([40, 5, 20, 1]);
  const out = await m.mapLimit([1, 2, 3, 4], 3, fn);
  return same(out, [2, 4, 6, 8], "порядок результатов совпадает с порядком элементов, а не завершения");
});
await test("limit-edge", async () => {
  const { state, fn } = tracker();
  const big = await m.mapLimit([1, 2], 10, fn);
  const empty = await m.mapLimit([], 3, fn);
  const one = tracker();
  await m.mapLimit([1, 2, 3], 1, one.fn);
  return same(big, [2, 4], "limit больше длины") ?? same(empty, [], "пустой массив") ?? expect(one.state.max, 1, "limit 1 - строго по очереди") ?? expect(state.max, 2, "не больше элементов, чем есть");
});
await test("limit-refill", async () => {
  const { state, fn } = tracker([50, 5, 5, 5]);
  await m.mapLimit([1, 2, 3, 4], 2, fn);
  return same(state.started, [0, 1, 2, 3], "порядок запуска") ?? expect(state.max, 2, "освободившийся слот занимается сразу, а не после всей пачки");
});
await test("limit-error", async () => {
  const started = [];
  const boom = new Error("сбой");
  let err;
  try {
    await m.mapLimit([1, 2, 3, 4, 5, 6], 2, async (x, i) => {
      started.push(i);
      await delay(5);
      if (x === 2) throw boom;
      return x;
    });
  } catch (e) { err = e; }
  await delay(50);
  return expect(err, boom, "отказ первой ошибкой") ?? expect(started.length <= 4, true, `после ошибки новые задачи запускаться не должны (запущено ${started.length})`);
});
await test("limit-invalid", async () => {
  const names = [];
  for (const bad of [0, -1, 1.5, "2", undefined, NaN]) {
    try { await m.mapLimit([1], bad, async (x) => x); names.push("нет ошибки"); } catch (e) { names.push(e?.name); }
  }
  return same(names, Array(6).fill("RangeError"), "неверный limit должен отклонять промис RangeError");
});
