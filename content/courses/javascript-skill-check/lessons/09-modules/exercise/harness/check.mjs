// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
const printedOnLoad = [];
{
  const saved = {};
  for (const name of ["log", "info", "warn", "error", "debug"]) {
    saved[name] = console[name];
    console[name] = (...args) => printedOnLoad.push(args.join(" "));
  }
  try {
    m = await import("./stats_module.mjs");
  } catch (e) {
    loadError = e;
  } finally {
    for (const name of Object.keys(saved)) console[name] = saved[name];
  }
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
const throwsType = (fn) => {
  try { fn(); } catch (e) { return e instanceof TypeError; }
  return false;
};

await test("mean-basic", () =>
  eq(m.mean([1, 2, 3, 4]), 2.5, "mean([1, 2, 3, 4])") ?? eq(m.mean([10]), 10, "mean([10])") ?? eq(m.mean([-2, 2]), 0, "mean([-2, 2])"));
await test("median-basic", () =>
  eq(m.median([3, 1, 2]), 2, "median([3, 1, 2])") ?? eq(m.median([4, 1, 3, 2]), 2.5, "median([4, 1, 3, 2])") ?? eq(m.median([5]), 5, "median([5])"));

await test("structure", () => {
  const names = Object.keys(m).sort();
  return (
    eq(names, ["mean", "median", "summarize"], "экспорты модуля") ??
    eq("default" in m, false, "default-экспорт") ??
    eq(["mean", "median", "summarize"].every((n) => typeof m[n] === "function"), true, "все экспорты - функции") ??
    eq(printedOnLoad, [], "вывод при загрузке модуля")
  );
});
await test("pure", () => {
  const frozen = Object.freeze([5, 1, 4, 2, 3]);
  const copy = [10, 2, 33, 4];
  const before = JSON.stringify(copy);
  const a = m.median(copy);
  const s1 = m.summarize(copy);
  const s2 = m.summarize(copy);
  return (
    eq(m.median(frozen), 3, "median замороженного массива") ?? eq(m.mean(frozen), 3, "mean замороженного массива") ??
    eq(m.summarize(frozen).max, 5, "summarize замороженного массива") ?? eq(JSON.stringify(copy), before, "входной массив изменился") ??
    eq(a, 7, "median([10, 2, 33, 4]) (сортировка по числам, а не по строкам)") ?? eq(s1, s2, "повторный вызов с тем же входом")
  );
});
await test("summarize", () =>
  eq(m.summarize([4, 1, 3, 2]), { count: 4, mean: 2.5, median: 2.5, min: 1, max: 4 }, "summarize([4, 1, 3, 2])") ??
  eq(m.summarize([-5, 5, 0]), { count: 3, mean: 0, median: 0, min: -5, max: 5 }, "summarize([-5, 5, 0])") ??
  eq(m.summarize([]), { count: 0, mean: null, median: null, min: null, max: null }, "summarize([])") ??
  eq(Object.keys(m.summarize([1])).sort(), ["count", "max", "mean", "median", "min"], "поля summarize"));
await test("validation", () =>
  eq([m.mean([]), m.median([])], [null, null], "пустые массивы") ??
  eq(
    [
      throwsType(() => m.mean("123")), throwsType(() => m.median(null)), throwsType(() => m.summarize([1, NaN])),
      throwsType(() => m.mean([1, Infinity])), throwsType(() => m.median([1, "2"])), throwsType(() => m.summarize({ length: 1 })),
    ],
    [true, true, true, true, true, true],
    "TypeError для неверного входа",
  ) ??
  eq(m.median([7, 7, 7, 8]), 7, "median([7, 7, 7, 8])") ?? eq(m.mean([0.5, 1.5]), 1, "mean([0.5, 1.5])"));
