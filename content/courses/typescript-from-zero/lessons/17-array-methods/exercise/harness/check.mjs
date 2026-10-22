// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./methods_task.ts");
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

await test("cheap-basic", () =>
  expectEach("cheapItems", [[[[30, 10, 50, 20], 30], [30, 10, 20]], [[[5, 6], 10], [5, 6]]]));
await test("tax-basic", () =>
  expectEach("withTax", [[[[100, 250], 20], [120, 300]], [[[99], 10], [109]]]));
await test("sort-basic", () => {
  const input = [3, 10, 2];
  const got = typeof m.sortedDescending === "function" ? m.sortedDescending(input) : null;
  if (got === null) return "функция sortedDescending не найдена: проверьте имя и слово export";
  if (!same(got, [10, 3, 2])) return `sortedDescending([3, 10, 2]) вернуло ${show(got)}, ожидалось [10,3,2]`;
  if (!same(input, [3, 10, 2])) return "sortedDescending изменила исходный массив: сортируйте копию [...values]";
  return null;
});
await test("join-has-basic", () =>
  expectEach("joinNames", [[[["хлеб", "сыр"]], "хлеб, сыр"], [[["молоко"]], "молоко"]]) ??
  expectEach("hasName", [[[["Анна", "Борис"], "Борис"], true], [[["Анна", "Борис"], "Ольга"], false]]));

await test("cheap-edge", () =>
  expectEach("cheapItems", [[[[], 10], []], [[[11, 12], 10], []], [[[10, 10], 10], [10, 10]], [[[-5, 0, 5], 0], [-5, 0]]]));
await test("tax-edge", () =>
  expectEach("withTax", [[[[], 20], []], [[[0], 20], [0]], [[[100], 0], [100]], [[[10, 15], 5], [11, 16]], [[[7], 50], [11]]]));
await test("sort-edge", () =>
  expectEach("sortedDescending", [[[[]], []], [[[1]], [1]], [[[10, 9, 1, 100]], [100, 10, 9, 1]], [[[2, 2, -1]], [2, 2, -1]]]) ??
  expectEach("joinNames", [[[[]], ""]]) ??
  expectEach("hasName", [[[[], "a"], false], [[["Анна"], "анна"], false], [[["Анна"], "Анна"], true]]));
