// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./objects_task.ts");
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

await test("make-basic", () =>
  expectEach("makeProduct", [[["хлеб", 40, 2], { name: "хлеб", price: 40, quantity: 2 }], [["сыр", 300, 1], { name: "сыр", price: 300, quantity: 1 }]]));
await test("line-total-basic", () =>
  expectEach("lineTotal", [[[{ name: "хлеб", price: 40, quantity: 2 }], 80], [[{ name: "чай", price: 120, quantity: 1 }], 120]]) ??
  expectEach("cartTotal", [[[[{ name: "хлеб", price: 40, quantity: 2 }, { name: "сыр", price: 300, quantity: 1 }]], 380]]));
await test("expensive-basic", () =>
  expectEach("mostExpensive", [[[[{ name: "хлеб", price: 40, quantity: 2 }, { name: "сыр", price: 300, quantity: 1 }, { name: "молоко", price: 90, quantity: 3 }]], "сыр"]]));

await test("make-edge", () =>
  expectEach("makeProduct", [[["", 0, 0], { name: "", price: 0, quantity: 0 }], [["яблоки", 12.5, 4], { name: "яблоки", price: 12.5, quantity: 4 }]]));
await test("total-edge", () =>
  expectEach("lineTotal", [[[{ name: "x", price: 99, quantity: 0 }], 0], [[{ name: "x", price: 0.5, quantity: 4 }], 2]]) ??
  expectEach("cartTotal", [[[[]], 0], [[[{ name: "a", price: 10, quantity: 0 }]], 0], [[[{ name: "a", price: 1, quantity: 1 }, { name: "b", price: 2, quantity: 2 }, { name: "c", price: 3, quantity: 3 }]], 14]]));
await test("expensive-edge", () =>
  expectEach("mostExpensive", [[[[]], ""], [[[{ name: "a", price: 5, quantity: 1 }]], "a"],
    [[[{ name: "a", price: 5, quantity: 9 }, { name: "b", price: 5, quantity: 1 }]], "a"],
    [[[{ name: "a", price: -3, quantity: 1 }, { name: "b", price: -1, quantity: 1 }]], "b"],
    [[[{ name: "дешёвый", price: 1, quantity: 1000 }, { name: "дорогой", price: 2, quantity: 1 }]], "дорогой"]]));
