// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./conditions_task.ts");
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

await test("ticket-basic", () =>
  expectEach("ticketPrice", [[[30], 300], [[10], 150], [[3], 0], [[70], 100]]));
await test("leap-basic", () =>
  expectEach("isLeapYear", [[[2024], true], [[2023], false]]));
await test("sign-basic", () =>
  expectEach("sign", [[[5], "положительное"], [[-3], "отрицательное"]]));

await test("ticket-edge", () =>
  expectEach("ticketPrice", [[[0], 0], [[6], 0], [[7], 150], [[17], 150], [[18], 300], [[64], 300], [[65], 100], [[100], 100]]));
await test("leap-edge", () =>
  expectEach("isLeapYear", [[[1900], false], [[2000], true], [[2100], false], [[2400], true], [[2019], false], [[2020], true]]));
await test("sign-edge", () =>
  expectEach("sign", [[[0], "ноль"], [[0.5], "положительное"], [[-0.5], "отрицательное"], [[1000000], "положительное"]]));
