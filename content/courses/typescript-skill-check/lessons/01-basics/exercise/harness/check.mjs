// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./basics_check.ts");
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

function expectThrows(name, args, message) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  let got;
  try {
    got = fn(...args);
  } catch (e) {
    if (!(e instanceof Error)) return `${name}(${args.map(show).join(", ")}) выбросило не Error, а ${show(e)}`;
    if (e.message !== message) return `${name}(${args.map(show).join(", ")}) выбросило сообщение ${show(e.message)}, ожидалось ${show(message)}`;
    return null;
  }
  return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, а ожидалось исключение с сообщением ${show(message)}`;
}

await test("leap-basic", () =>
  expectEach("isLeapYear", [[[2024], true], [[2023], false], [[2000], true], [[1900], false]]));
await test("days-basic", () =>
  expectEach("daysInMonth", [[[2023, 1], 31], [[2023, 4], 30], [[2023, 2], 28], [[2024, 2], 29]]));
await test("gcd-basic", () =>
  expectEach("gcd", [[[12, 18], 6], [[17, 5], 1], [[100, 25], 25]]));
await test("count-basic", () =>
  expectEach("countMultiples", [[[10, 3], 3], [[20, 5], 4], [[7, 10], 0]]));

await test("leap-edge", () =>
  expectEach("isLeapYear", [[[2100], false], [[1600], true], [[4], true], [[2001], false], [[1904], true]]));
await test("days-edge", () =>
  expectEach("daysInMonth", [[[2023, 0], 0], [[2023, 13], 0], [[2023, 12], 31], [[2100, 2], 28], [[2000, 2], 29], [[2023, 9], 30], [[2023, 11], 30], [[2023, 6], 30], [[2023, 8], 31]]));
await test("gcd-edge", () =>
  expectEach("gcd", [[[7, 0], 7], [[0, 7], 7], [[0, 0], 0], [[48, 18], 6], [[13, 13], 13], [[1, 1000000], 1]]));
await test("count-edge", () =>
  expectEach("countMultiples", [[[0, 3], 0], [[-5, 2], 0], [[1, 1], 1], [[100, 7], 14], [[6, 6], 1]]));
