// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./arrays_task.ts");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `файл не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

const show = (v) => {
  if (typeof v === "string") return JSON.stringify(v);
  if (Object.is(v, -0)) return "-0";
  if (Array.isArray(v) || (v && typeof v === "object")) return JSON.stringify(v);
  return String(v);
};
const same = (a, b) => {
  if (Array.isArray(a) && Array.isArray(b)) return a.length === b.length && a.every((x, i) => same(x, b[i]));
  if (a && b && typeof a === "object" && typeof b === "object" && !Array.isArray(a) && !Array.isArray(b)) {
    const ka = Object.keys(a).sort(), kb = Object.keys(b).sort();
    return ka.length === kb.length && ka.every((k, i) => k === kb[i] && same(a[k], b[k]));
  }
  return Object.is(a, b);
};
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!same(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

await test("squares-basic", () =>
  expectEach("squares", [[[4], [1, 4, 9, 16]], [[1], [1]], [[3], [1, 4, 9]]]));
await test("last-basic", () =>
  expectEach("lastValue", [[[[5, 8, 2]], 2], [[[7]], 7], [[[1, 2, 3, 4]], 4]]));
await test("sum-basic", () =>
  expectEach("sumOf", [[[[4, -1, 10]], 13], [[[1, 2, 3]], 6], [[[5]], 5]]));

await test("squares-edge", () =>
  expectEach("squares", [[[0], []], [[-5], []], [[10], [1, 4, 9, 16, 25, 36, 49, 64, 81, 100]]]));
await test("last-edge", () =>
  expectEach("lastValue", [[[[]], 0], [[[0]], 0], [[[3, -9]], -9], [[[-1, 0, 4, 0]], 0]]));
await test("sum-edge", () =>
  expectEach("sumOf", [[[[]], 0], [[[-2, -3]], -5], [[[0.5, 0.25]], 0.75], [[[1000000, 2000000, 3000000]], 6000000]]));
