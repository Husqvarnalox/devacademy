// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./functions_task.mjs");
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

await test("greet-basic", () =>
  expectEach("greet", [[["Анна"], "Привет, Анна!"], [["Bob"], "Привет, Bob!"]]));
await test("area-basic", () =>
  expectEach("rectangleArea", [[[3, 4], 12], [[10, 10], 100]]));
await test("average-basic", () =>
  expectEach("average", [[[2, 4], 3], [[10, 20], 15]]));

await test("greet-edge", () =>
  expectEach("greet", [[[""], "Привет, !"], [["Ёжик"], "Привет, Ёжик!"], [["Мария Ивановна"], "Привет, Мария Ивановна!"]]));
await test("area-edge", () =>
  expectEach("rectangleArea", [[[0, 5], 0], [[2.5, 2], 5], [[-2, 3], -6], [[1, 1], 1]]));
await test("average-edge", () =>
  expectEach("average", [[[-4, 4], 0], [[1, 2], 1.5], [[0, 0], 0], [[-10, -20], -15]]));
