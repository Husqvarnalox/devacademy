// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./objects.mjs");
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

await test("make-basic", () => {
  const got = m.makeProduct?.("Чай", 120);
  if (got === undefined) return "makeProduct должна вернуть объект";
  return expectEach("makeProduct", [
    [["Чай", 120], { name: "Чай", price: 120, inStock: true }],
    [["Pen", 5], { name: "Pen", price: 5, inStock: true }],
  ]);
});
await test("get-has-basic", () =>
  expectEach("getField", [[[{ a: 1 }, "a"], 1], [[{ name: "Анна", age: 30 }, "age"], 30], [[{ x: "да" }, "x"], "да"]]) ??
  expectEach("hasField", [[[{ a: 1 }, "a"], true], [[{ a: 1 }, "b"], false], [[{ name: "Анна" }, "age"], false]]));
await test("add-names-basic", () =>
  expectEach("addField", [[[{ a: 1 }, "b", 2], { a: 1, b: 2 }], [[{}, "x", "y"], { x: "y" }]]) ??
  expectEach("fieldNames", [[[{ x: 1, y: 2 }], ["x", "y"]], [[{ имя: "Анна", возраст: 30, город: "Омск" }], ["имя", "возраст", "город"]]]));

await test("objects-edge", () =>
  expectEach("getField", [[[{}, "a"], undefined], [[{ a: 0 }, "a"], 0], [[{ "цена": 50 }, "цена"], 50], [[{ a: undefined }, "a"], undefined]]) ??
  expectEach("hasField", [[[{ a: undefined }, "a"], true], [[{}, "a"], false], [[{ a: 0 }, "a"], true], [[{ "цена": 50 }, "цена"], true], [[{ "цена": 50 }, "Цена"], false]]) ??
  expectEach("fieldNames", [[[{}], []], [[{ a: 1 }], ["a"]]]));
await test("add-edge", () => {
  const problem = expectEach("addField", [
    [[{ a: 1 }, "a", 5], { a: 5 }],
    [[{ a: 1, b: 2 }, "c", [1, 2]], { a: 1, b: 2, c: [1, 2] }],
    [[{}, "слово", "значение"], { слово: "значение" }],
  ]);
  if (problem) return problem;
  const original = { a: 1 };
  const got = m.addField(original, "b", 2);
  if (got !== original) return "addField должна вернуть тот же объект, который получила, а не новый";
  if (original.b !== 2) return "исходный объект должен измениться: в нём должно появиться свойство b";
  return null;
});
