// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./for_task.ts");
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

await test("factorial-basic", () =>
  expectEach("factorial", [[[4], 24], [[5], 120], [[1], 1]]));
await test("repeat-basic", () =>
  expectEach("repeatText", [[["ab", 3], "ababab"], [["-", 5], "-----"]]));
await test("multiples-basic", () =>
  expectEach("countMultiples", [[[10, 3], 3], [[20, 5], 4], [[7, 1], 7]]));

await test("factorial-edge", () =>
  expectEach("factorial", [[[0], 1], [[-3], 1], [[2], 2], [[10], 3628800], [[12], 479001600]]));
await test("repeat-edge", () =>
  expectEach("repeatText", [[["x", 0], ""], [["x", -2], ""], [["", 4], ""], [["ab", 1], "ab"], [["Привет ", 2], "Привет Привет "]]));
await test("multiples-edge", () =>
  expectEach("countMultiples", [[[0, 3], 0], [[2, 3], 0], [[3, 3], 1], [[100, 7], 14], [[1000, 1], 1000]]));
