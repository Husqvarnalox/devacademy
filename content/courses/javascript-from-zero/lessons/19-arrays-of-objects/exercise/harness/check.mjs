// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./records.mjs");
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

const show = (v) =>
  typeof v === "string" ? JSON.stringify(v)
  : v === undefined ? "undefined"
  : Object.is(v, -0) ? "-0"
  : typeof v === "object" ? JSON.stringify(v)
  : String(v);
const canon = (v) =>
  Array.isArray(v) ? v.map(canon)
  : v !== null && typeof v === "object" ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, canon(v[k])]))
  : v;
const same = (a, b) =>
  Object.is(a, b) ||
  (typeof a === "object" && typeof b === "object" && a !== null && b !== null &&
    Array.isArray(a) === Array.isArray(b) && JSON.stringify(canon(a)) === JSON.stringify(canon(b)));
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!same(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}
function expectThrows(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, message] of cases) {
    let thrown = false;
    try {
      const got = fn(...args);
      return `${name}(${args.map(show).join(", ")}) должна бросить ошибку, а вернула ${show(got)}`;
    } catch (e) {
      thrown = true;
      if (!(e instanceof Error)) return `${name}(${args.map(show).join(", ")}) бросила не Error, а ${show(e)}`;
      if (e.message !== message) return `${name}(${args.map(show).join(", ")}) бросила сообщение ${show(e.message)}, ожидалось ${show(message)}`;
    }
  }
  return null;
}

const tea = { id: 1, name: "Чай", price: 120, qty: 2 };
const coffee = { id: 2, name: "Кофе", price: 200, qty: 1 };
const bread = { id: 3, name: "Хлеб", price: 40, qty: 3 };

await test("find-total-basic", () =>
  expectEach("findById", [[[[tea, coffee, bread], 2], coffee], [[[tea, coffee, bread], 3], bread], [[[tea, coffee], 1], tea]]) ??
  expectEach("totalPrice", [[[[{ price: 10, qty: 3 }, { price: 5, qty: 2 }]], 40], [[[tea, coffee, bread]], 560]]));
await test("cheap-group-basic", () =>
  expectEach("cheapest", [[[[tea, coffee, bread]], bread], [[[coffee, tea]], tea]]) ??
  expectEach("groupTotals", [
    [[[{ customer: "Анна", amount: 100 }, { customer: "Борис", amount: 50 }, { customer: "Анна", amount: 70 }]], { "Анна": 170, "Борис": 50 }],
    [[[{ customer: "x", amount: 1 }, { customer: "x", amount: 2 }]], { x: 3 }],
  ]) ??
  expectEach("sumValues", [[[{ a: 1, b: 2 }], 3], [[{ x: 10, y: 20, z: 30 }], 60]]));

await test("records-edge", () =>
  expectEach("findById", [[[[], 1], undefined], [[[tea], 99], undefined], [[[tea], 1], tea]]) ??
  expectEach("totalPrice", [[[[]], 0], [[[{ price: 7, qty: 0 }]], 0], [[[{ price: 7, qty: 1 }]], 7]]) ??
  expectEach("cheapest", [[[[]], undefined], [[[tea]], tea]]) ??
  expectEach("groupTotals", [[[[]], {}]]) ??
  expectEach("sumValues", [[[{}], 0], [[{ a: -5, b: 5 }], 0]]));
await test("group-edge", () => {
  const problem =
    expectEach("cheapest", [
      [[[{ n: "a", price: 5 }, { n: "b", price: 3 }, { n: "c", price: 3 }]], { n: "b", price: 3 }],
      [[[{ n: "x", price: -1 }, { n: "y", price: 0 }]], { n: "x", price: -1 }],
    ]) ??
    expectEach("groupTotals", [
      [[[{ customer: "Вера", amount: 0 }, { customer: "Глеб", amount: 5 }, { customer: "Вера", amount: 0 }]], { "Вера": 0, "Глеб": 5 }],
      [[[{ customer: "Б", amount: 1 }, { customer: "А", amount: 2 }, { customer: "Б", amount: 3 }]], { "Б": 4, "А": 2 }],
    ]);
  if (problem) return problem;
  const got = m.groupTotals([{ customer: "Б", amount: 1 }, { customer: "А", amount: 2 }, { customer: "Б", amount: 3 }]);
  const order = JSON.stringify(Object.keys(got));
  if (order !== JSON.stringify(["Б", "А"])) return `ключи должны идти в порядке первого появления: ["Б","А"], а получилось ${order}`;
  return null;
});
