// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./inventory_task.ts");
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

const bread = { name: "хлеб", price: 40, category: "еда" };
const cheese = { name: "сыр", price: 300, category: "еда", discount: 50 };
const pen = { name: "ручка", price: 25, category: "канцелярия" };
const tea = { name: "чай", price: 120, category: "еда", discount: 20 };

await test("find-basic", () =>
  expectEach("findByName", [[[[bread, cheese, pen], "сыр"], cheese], [[[bread, cheese, pen], "кофе"], undefined], [[[bread, cheese, pen], "хлеб"], bread]]));
await test("final-price-basic", () =>
  expectEach("finalPrice", [[[bread], 40], [[cheese], 250], [[tea], 100]]));
await test("total-basic", () =>
  expectEach("totalInCategory", [[[[bread, cheese, pen], "еда"], 290], [[[bread, cheese, pen], "канцелярия"], 25]]));
await test("names-basic", () =>
  expectEach("namesByPrice", [[[[bread, cheese, pen]], ["ручка", "хлеб", "сыр"]]]));

await test("find-edge", () =>
  expectEach("findByName", [[[[], "хлеб"], undefined],
    [[[{ name: "а", price: 1, category: "x" }, { name: "а", price: 2, category: "y" }], "а"], { name: "а", price: 1, category: "x" }],
    [[[bread], "Хлеб"], undefined]]));
await test("final-price-edge", () =>
  expectEach("finalPrice", [[[{ name: "а", price: 10, category: "x", discount: 0 }], 10], [[{ name: "а", price: 10, category: "x", discount: 10 }], 0], [[{ name: "а", price: 9.5, category: "x", discount: 1.5 }], 8]]));
await test("total-edge", () =>
  expectEach("totalInCategory", [[[[], "еда"], 0], [[[bread, cheese, pen], "кино"], 0], [[[bread, tea, cheese, pen], "еда"], 390], [[[cheese], "еда"], 250]]));
await test("names-edge", () => {
  const first = [cheese, bread, pen, tea];
  const copy = JSON.stringify(first);
  const r = expectEach("namesByPrice", [[[[]], []], [[first], ["ручка", "хлеб", "чай", "сыр"]],
    [[[{ name: "б", price: 5, category: "x" }, { name: "а", price: 5, category: "x" }, { name: "в", price: 6, category: "x", discount: 1 }]], ["б", "а", "в"]]]);
  if (r !== null) return r;
  return JSON.stringify(first) === copy ? null : "исходный массив изменён: сортируйте копию [...items]";
});
