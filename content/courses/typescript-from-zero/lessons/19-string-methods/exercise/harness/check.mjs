// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./string_task.ts");
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

await test("capitalize-basic", () =>
  expectEach("capitalize", [[["пРИВЕТ"], "Привет"], [["hello"], "Hello"], [["Мир"], "Мир"]]));
await test("initials-basic", () =>
  expectEach("initials", [[["иван петров"], "ИП"], [["  иван  петрович  сидоров "], "ИПС"]]));
await test("slug-basic", () =>
  expectEach("slugify", [[["  Hello World  "], "hello-world"], [["Один"], "один"]]));
await test("words-basic", () =>
  expectEach("countWords", [[["  один  два три "], 3], [["раз два"], 2]]));

await test("capitalize-edge", () =>
  expectEach("capitalize", [[[""], ""], [["а"], "А"], [["ПАРИЖ"], "Париж"], [["1abc"], "1abc"]]));
await test("initials-edge", () =>
  expectEach("initials", [[[""], ""], [["   "], ""], [["аня"], "А"], [["Анна-Мария Ли"], "АЛ"]]));
await test("slug-words-edge", () =>
  expectEach("slugify", [[[""], ""], [["A B C"], "a-b-c"], [["  ПРИВЕТ Мир "], "привет-мир"]]) ??
  expectEach("countWords", [[[""], 0], [["   "], 0], [["слово"], 1], [[" a b c d "], 4]]));
