// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./loops_task.ts");
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

const show = (v) => (typeof v === "string" ? JSON.stringify(v) : Object.is(v, -0) ? "-0" : String(v));
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!Object.is(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

await test("sum-basic", () =>
  expectEach("sumTo", [[[4], 10], [[10], 55], [[1], 1]]));
await test("digits-basic", () =>
  expectEach("countDigits", [[[7], 1], [[2025], 4], [[100], 3]]));
await test("days-basic", () =>
  expectEach("daysToSave", [[[1000, 300], 4], [[900, 300], 3]]));

await test("sum-edge", () =>
  expectEach("sumTo", [[[0], 0], [[-3], 0], [[100], 5050], [[1000], 500500]]));
await test("digits-edge", () =>
  expectEach("countDigits", [[[0], 1], [[9], 1], [[10], 2], [[99999], 5], [[1000000], 7]]));
await test("days-edge", () =>
  expectEach("daysToSave", [[[0, 300], 0], [[-5, 10], 0], [[300, 300], 1], [[301, 300], 2], [[1, 1000], 1], [[100000, 1], 100000]]));
