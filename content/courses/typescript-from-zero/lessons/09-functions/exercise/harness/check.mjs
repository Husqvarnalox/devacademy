// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./functions_task.ts");
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

await test("farewell-basic", () =>
  expectEach("farewell", [[["Анна"], "До свидания, Анна!"], [["Bob"], "До свидания, Bob!"]]));
await test("total-basic", () =>
  expectEach("lineTotal", [[[40, 3], 120], [[15, 10], 150]]));
await test("discount-basic", () =>
  expectEach("discounted", [[[200, 10], 180], [[1000, 25], 750]]));

await test("farewell-edge", () =>
  expectEach("farewell", [[[""], "До свидания, !"], [["Ёжик"], "До свидания, Ёжик!"], [["Мария Ивановна"], "До свидания, Мария Ивановна!"]]));
await test("total-edge", () =>
  expectEach("lineTotal", [[[0, 5], 0], [[2.5, 2], 5], [[-3, 2], -6], [[7, 1], 7]]));
await test("discount-edge", () =>
  expectEach("discounted", [[[250, 0], 250], [[250, 100], 0], [[80, 50], 40], [[250, 15], 212.5]]));
