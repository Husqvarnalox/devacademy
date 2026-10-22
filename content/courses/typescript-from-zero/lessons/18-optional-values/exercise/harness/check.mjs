// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./optional_task.ts");
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

await test("negative-basic", () =>
  expectEach("findFirstNegative", [[[[4, -2, 7, -9]], -2], [[[-1]], -1], [[[4, 0, 7]], undefined]]));
await test("price-basic", () =>
  expectEach("describePrice", [[[250], "250 руб."], [[undefined], "нет цены"], [[99.5], "99.5 руб."]]));
await test("item-basic", () =>
  expectEach("itemOrDefault", [[[[4, 5, 6], 1, 0], 5], [[[4, 5, 6], -1, 0], 6], [[[4, 5, 6], 9, 0], 0]]));

await test("negative-edge", () =>
  expectEach("findFirstNegative", [[[[]], undefined], [[[0, 0]], undefined], [[[-0.5, -3]], -0.5], [[[1, 2, 3, -4]], -4]]));
await test("price-edge", () =>
  expectEach("describePrice", [[[0], "0 руб."], [[-5], "-5 руб."]]));
await test("item-edge", () =>
  expectEach("itemOrDefault", [[[[], 0, 7], 7], [[[0, 1], 0, 9], 0], [[[0, 1], -2, 9], 0], [[[5], -2, 9], 9], [[[1, 2, 3], 3, -1], -1]]));
