// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./iterate_task.ts");
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

await test("max-basic", () =>
  expectEach("maxOf", [[[[3, 9, 4]], 9], [[[1, 2, 3]], 3], [[[8]], 8]]));
await test("above-basic", () =>
  expectEach("countAbove", [[[[5, 8, 3, 8], 5], 2], [[[1, 2, 3], 0], 3]]));
await test("index-basic", () =>
  expectEach("indexOfValue", [[[[7, 3, 9, 3], 3], 1], [[[7, 3, 9], 9], 2], [[[7, 3, 9], 4], -1]]));

await test("max-edge", () =>
  expectEach("maxOf", [[[[]], 0], [[[-5, -2, -9]], -2], [[[-1]], -1], [[[0, -3]], 0], [[[2, 2, 2]], 2]]));
await test("above-edge", () =>
  expectEach("countAbove", [[[[], 3], 0], [[[5, 5], 5], 0], [[[-1, -2, -3], -2], 1], [[[10, 20, 30], 100], 0]]));
await test("index-edge", () =>
  expectEach("indexOfValue", [[[[], 1], -1], [[[5], 5], 0], [[[0, 0], 0], 0], [[[1, 2, 3], -1], -1]]));
