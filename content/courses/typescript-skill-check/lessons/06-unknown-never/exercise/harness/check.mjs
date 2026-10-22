// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./unknown_check.ts");
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

await test("string-array-basic", () =>
  expectEach("isStringArray", [[[["a", "b"]], true], [[[1, "b"]], false], [["ab"], false]]));
await test("parse-user-basic", () =>
  expectEach("parseUser", [[['{"name": "Аня", "age": 25}'], { name: "Аня", age: 25 }], [["не json"], undefined]]));
await test("describe-basic", () =>
  expectEach("describeUnknown", [[["abc"], "строка: abc"], [[5], "число: 5"], [[true], "логическое: true"], [[null], "null"]]));
await test("sum-basic", () =>
  expectEach("sumNumbers", [[[[1, 2, "3", 4]], 7], [[[]], 0]]));

await test("string-array-edge", () =>
  expectEach("isStringArray", [[[[]], true], [[null], false], [[[["a"]]], false], [[{ 0: "a", length: 1 }], false]]));
await test("parse-user-edge", () =>
  expectEach("parseUser", [
    [['{"name": "  Ян ", "age": 0}'], { name: "Ян", age: 0 }],
    [['{"name": "", "age": 5}'], undefined],
    [['{"name": "   ", "age": 5}'], undefined],
    [['{"name": "А", "age": 1.5}'], undefined],
    [['{"name": "А", "age": -1}'], undefined],
    [['{"name": "А", "age": "7"}'], undefined],
    [['{"name": "А"}'], undefined],
    [["null"], undefined],
    [['["name", 1]'], undefined],
    [["42"], undefined],
    [[""], undefined],
  ]));
await test("describe-edge", () =>
  expectEach("describeUnknown", [[[undefined], "undefined"], [[[1, 2, 3]], "массив из 3"], [[[]], "массив из 0"], [[{}], "объект"], [[0], "число: 0"], [[""], "строка: "], [[false], "логическое: false"], [[NaN], "число: NaN"]]));
await test("sum-edge", () =>
  expectEach("sumNumbers", [[[[NaN, 1, Infinity, 2, -Infinity]], 3], [[["5", null, undefined, {}, [7]]], 0], [[[0.5, 0.25, -1]], -0.25]]));
