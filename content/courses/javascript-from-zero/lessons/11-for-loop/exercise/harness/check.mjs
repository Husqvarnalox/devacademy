// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./for_loops.mjs");
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

await test("factorial-basic", () =>
  expectEach("factorial", [[[1], 1], [[3], 6], [[5], 120]]));
await test("repeat-basic", () =>
  expectEach("repeatText", [[["ab", 3], "ababab"], [["-", 5], "-----"]]));
await test("multiples-basic", () =>
  expectEach("countMultiples", [[[10, 3], 3], [[20, 5], 4]]));

await test("factorial-edge", () =>
  expectEach("factorial", [[[0], 1], [[2], 2], [[10], 3628800], [[12], 479001600]]));
await test("repeat-edge", () =>
  expectEach("repeatText", [[["ха", 3], "хахаха"], [["abc", 0], ""], [["", 4], ""], [["x", 1], "x"]]));
await test("multiples-edge", () =>
  expectEach("countMultiples", [[[0, 3], 0], [[-5, 2], 0], [[2, 3], 0], [[3, 3], 1], [[100, 1], 100], [[100, 7], 14]]));
