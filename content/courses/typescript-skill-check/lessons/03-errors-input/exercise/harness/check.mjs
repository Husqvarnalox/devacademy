// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./errors_check.ts");
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

function expectScoreError(arg) {
  if (typeof m.parseScore !== "function") return "функция parseScore не найдена: проверьте имя и слово export";
  let got;
  try {
    got = m.parseScore(arg);
  } catch (e) {
    if (!(e instanceof Error)) return `parseScore(${show(arg)}) выбросило не Error`;
    return e.message === "оценка должна быть целым числом от 0 до 100" ? null : `parseScore(${show(arg)}) выбросило сообщение ${show(e.message)}`;
  }
  return `parseScore(${show(arg)}) вернуло ${show(got)}, а ожидалось исключение`;
}

await test("parse-basic", () =>
  expectEach("parseScore", [[["85"], 85], [[" 7 "], 7], [["0"], 0], [["100"], 100]]) ?? expectScoreError("abc"));
await test("safe-basic", () =>
  expectEach("safeScore", [[["42"], 42], [["x"], null], [["101"], null]]));
await test("sum-basic", () =>
  expectEach("summarize", [[["5\nabc\n4"], { count: 2, average: 4.5, skipped: 1 }], [["100"], { count: 1, average: 100, skipped: 0 }]]));
await test("fail-basic", () =>
  expectEach("describeFailure", [[[new Error("сбой")], "ошибка: сбой"], [["строка"], "неизвестная ошибка"]]));

await test("parse-edge", () =>
  expectScoreError("") ?? expectScoreError("   ") ?? expectScoreError("4.5") ?? expectScoreError("-1") ?? expectScoreError("101") ??
  expectScoreError("1e1") ?? expectScoreError("0x10") ?? expectScoreError("5 5") ??
  expectEach("parseScore", [[["007"], 7], [["  100  "], 100]]));
await test("safe-edge", () =>
  expectEach("safeScore", [[[""], null], [["   "], null], [["0"], 0], [["4.5"], null], [[" 100"], 100]]));
await test("sum-edge", () =>
  expectEach("summarize", [
    [[""], { count: 0, average: 0, skipped: 0 }],
    [["\n  \n"], { count: 0, average: 0, skipped: 0 }],
    [["x\ny\n"], { count: 0, average: 0, skipped: 2 }],
    [["5\r\n4\r\n4\r\n"], { count: 3, average: 4.33, skipped: 0 }],
    [["0\n2.5\n101\n3"], { count: 2, average: 1.5, skipped: 2 }],
  ]));
await test("fail-edge", () =>
  expectEach("describeFailure", [[[new TypeError("тип")], "ошибка: тип"], [[null], "неизвестная ошибка"], [[{ message: "ложь" }], "неизвестная ошибка"], [[42], "неизвестная ошибка"]]));
