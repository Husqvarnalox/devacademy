// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./while_loops.mjs");
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
  expectEach("sumTo", [[[1], 1], [[4], 10], [[10], 55]]));
await test("digits-basic", () =>
  expectEach("countDigits", [[[7], 1], [[123], 3], [[2024], 4]]));
await test("days-basic", () =>
  expectEach("daysToSave", [[[100, 30], 4], [[90, 30], 3]]));

await test("sum-edge", () =>
  expectEach("sumTo", [[[0], 0], [[-5], 0], [[100], 5050], [[1000], 500500]]));
await test("digits-edge", () =>
  expectEach("countDigits", [[[0], 1], [[9], 1], [[10], 2], [[99999], 5], [[1000000], 7]]));
await test("days-edge", () =>
  expectEach("daysToSave", [[[0, 5], 0], [[-10, 5], 0], [[1, 100], 1], [[10, 10], 1], [[11, 10], 2], [[7, 2], 4]]));
