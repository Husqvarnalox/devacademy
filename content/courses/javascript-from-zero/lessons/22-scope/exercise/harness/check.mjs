// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./scope.mjs");
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

const show = (v) =>
  typeof v === "string" ? JSON.stringify(v)
  : v === undefined ? "undefined"
  : Object.is(v, -0) ? "-0"
  : typeof v === "object" ? JSON.stringify(v)
  : String(v);
const canon = (v) =>
  Array.isArray(v) ? v.map(canon)
  : v !== null && typeof v === "object" ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, canon(v[k])]))
  : v;
const same = (a, b) =>
  Object.is(a, b) ||
  (typeof a === "object" && typeof b === "object" && a !== null && b !== null &&
    Array.isArray(a) === Array.isArray(b) && JSON.stringify(canon(a)) === JSON.stringify(canon(b)));
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!same(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

await test("count-max-basic", () =>
  expectEach("countEven", [[[[1, 2, 3, 4, 6]], 3], [[[1, 3, 5]], 0], [[[10, 20]], 2]]) ??
  expectEach("maxOf", [[[[3, 9, 4]], 9], [[[1, 2, 3]], 3], [[[8, 2, 5]], 8]]));
await test("grade-index-basic", () =>
  expectEach("grade", [[[95], "отлично"], [[75], "хорошо"], [[55], "зачёт"], [[20], "незачёт"]]) ??
  expectEach("indexOfValue", [[[["a", "b", "c"], "b"], 1], [[[5, 6, 7], 5], 0], [[["x", "y"], "z"], -1]]));

await test("count-max-edge", () =>
  expectEach("countEven", [[[[]], 0], [[[0]], 1], [[[-2, -3, -4]], 2], [[[7]], 0]]) ??
  expectEach("maxOf", [[[[-5, -2, -9]], -2], [[[7]], 7], [[[0, -1]], 0], [[[2, 9, 9, 1]], 9]]));
await test("grade-index-edge", () =>
  expectEach("grade", [[[90], "отлично"], [[89], "хорошо"], [[70], "хорошо"], [[69], "зачёт"], [[50], "зачёт"], [[49], "незачёт"], [[0], "незачёт"]]) ??
  expectEach("indexOfValue", [
    [[[], 1], -1],
    [[["м", "а", "м", "а"], "м"], 0],
    [[[1, 2, 3, 2], 2], 1],
    [[[0, 1], 0], 0],
    [[["Привет", "мир"], "мир"], 1],
    [[[1, 2, 3], 3], 2],
  ]));
