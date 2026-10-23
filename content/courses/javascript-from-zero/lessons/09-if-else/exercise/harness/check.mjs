// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./branches.mjs");
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

await test("sign-basic", () =>
  expectEach("sign", [[[5], "положительное"], [[-3], "отрицательное"], [[0], "ноль"]]));
await test("grade-basic", () =>
  expectEach("grade", [[[95], "отлично"], [[75], "хорошо"], [[55], "удовлетворительно"], [[20], "неудовлетворительно"]]));
await test("price-basic", () =>
  expectEach("entryPrice", [[[3], 0], [[12], 150], [[30], 400], [[70], 200]]));

await test("sign-edge", () =>
  expectEach("sign", [[[0.001], "положительное"], [[-0.001], "отрицательное"], [[1e-9], "положительное"], [[-1000000], "отрицательное"]]));
await test("grade-edge", () =>
  expectEach("grade", [
    [[100], "отлично"], [[90], "отлично"], [[89], "хорошо"], [[70], "хорошо"], [[69], "удовлетворительно"],
    [[50], "удовлетворительно"], [[49], "неудовлетворительно"], [[0], "неудовлетворительно"],
  ]));
await test("price-edge", () =>
  expectEach("entryPrice", [
    [[0], 0], [[6], 0], [[7], 150], [[17], 150], [[18], 400], [[64], 400], [[65], 200], [[120], 200],
  ]));
