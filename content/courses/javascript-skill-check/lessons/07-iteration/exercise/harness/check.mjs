// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./iteration_check.mjs");
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
const isIterable = (v) => v != null && typeof v[Symbol.iterator] === "function";

await test("range-basic", () =>
  eq([...new m.Range(0, 5)], [0, 1, 2, 3, 4], "Range(0, 5)") ??
  eq([...new m.Range(1, 10, 3)], [1, 4, 7], "Range(1, 10, 3)") ??
  eq([...new m.Range(5, 0, -2)], [5, 3, 1], "Range(5, 0, -2)"));
await test("pipeline-basic", () => {
  const odd = m.filter(m.naturals(), (x) => x % 2 === 1);
  const sq = m.map(odd, (x) => x * x);
  return eq([...m.take(sq, 3)], [1, 9, 25], "take(map(filter(naturals()))) ") ?? eq([...m.take(m.naturals(10), 3)], [10, 11, 12], "take(naturals(10), 3)");
});

await test("range-edge", () => {
  const r = new m.Range(0, 3);
  const first = [...r];
  const second = [...r];
  let err = null;
  try { new m.Range(0, 5, 0); } catch (e) { err = e; }
  return (
    eq(first, [0, 1, 2], "первый обход") ?? eq(second, [0, 1, 2], "второй обход того же объекта") ??
    eq(err instanceof RangeError, true, "Range с шагом 0 бросает RangeError") ??
    eq([...new m.Range(3, 3)], [], "пустой диапазон") ?? eq([...new m.Range(0, 5, -1)], [], "шаг в неверную сторону") ??
    eq(Array.from(new m.Range(0, 2)), [0, 1], "Array.from(Range)") ?? eq(Math.max(...new m.Range(1, 4)), 3, "spread в аргументы")
  );
});
await test("lazy", () => {
  let calls = 0, pulled = 0;
  const source = (function* () { for (let i = 0; ; i++) { pulled++; yield i; } })();
  const mapped = m.map(source, (x) => { calls++; return x * 10; });
  if (!isIterable(mapped)) return "map должен возвращать итерируемый объект";
  const noWork = calls === 0 && pulled === 0;
  const got = [...m.take(mapped, 3)];
  return (
    eq(noWork, true, "вызов map не должен ничего вычислять до обхода") ?? eq(got, [0, 10, 20], "значения") ??
    eq(calls, 3, "число вызовов fn") ?? eq(pulled, 3, "число элементов, запрошенных у источника")
  );
});
await test("close", () => {
  let closed = false;
  const source = (function* () { try { yield 1; yield 2; yield 3; } finally { closed = true; } })();
  const got = [...m.take(source, 2)];
  let started = false;
  const never = (function* () { started = true; yield 1; })();
  const none = [...m.take(never, 0)];
  return eq(got, [1, 2], "take(источник, 2)") ?? eq(closed, true, "источник не закрыт после take") ?? eq(none, [], "take(…, 0)") ?? eq(started, false, "take(…, 0) не должен начинать обход");
});
await test("index", () => {
  const seen = [];
  const f = [...m.filter(["a", "b", "c", "d"], (x, i) => { seen.push(i); return i % 2 === 1; })];
  const g = [...m.map("xyz", (c, i) => c + i)];
  return (
    eq(f, ["b", "d"], "filter по индексу") ?? eq(seen, [0, 1, 2, 3], "индексы filter") ?? eq(g, ["x0", "y1", "z2"], "map с индексом") ??
    eq([...m.take([1, 2], 5)], [1, 2], "take больше длины") ?? eq([...m.take([1, 2], -1)], [], "take(…, -1)") ?? eq([...m.take(new Set([4, 5, 6]), 2)], [4, 5], "take из Set")
  );
});
