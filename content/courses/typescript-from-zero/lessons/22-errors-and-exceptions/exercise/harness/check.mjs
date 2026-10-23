// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./errors_task.ts");
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

await test("divide-basic", () =>
  expectEach("safeDivide", [[[10, 4], 2.5], [[9, 3], 3], [[-6, 2], -3]]) ??
  expectThrows("safeDivide", [1, 0], "деление на ноль"));
await test("age-basic", () =>
  expectEach("checkAge", [[[30], 30], [[0], 0], [[150], 150]]) ??
  expectThrows("checkAge", [-5], "возраст вне диапазона 0-150") ??
  expectThrows("checkAge", [200], "возраст вне диапазона 0-150") ??
  expectThrows("checkAge", [2.5], "возраст должен быть целым числом"));
await test("text-basic", () =>
  expectEach("divideText", [[[10, 4], "результат: 2.5"], [[1, 0], "ошибка: деление на ноль"]]));
await test("valid-basic", () =>
  expectEach("validAges", [[[[20, -1, 40, 200, 7]], [20, 40, 7]], [[[]], []]]));

await test("divide-edge", () =>
  expectEach("safeDivide", [[[0, 5], 0], [[5, 0.5], 10]]) ??
  expectThrows("safeDivide", [0, 0], "деление на ноль") ??
  expectThrows("safeDivide", [-3, 0], "деление на ноль"));
await test("age-edge", () =>
  expectThrows("checkAge", [151], "возраст вне диапазона 0-150") ??
  expectThrows("checkAge", [-1], "возраст вне диапазона 0-150") ??
  expectThrows("checkAge", [0.5], "возраст должен быть целым числом") ??
  expectThrows("checkAge", [NaN], "возраст должен быть целым числом"));
await test("text-edge", () =>
  expectEach("divideText", [[[0, 4], "результат: 0"], [[0, 0], "ошибка: деление на ноль"], [[-9, 3], "результат: -3"]]));
await test("valid-edge", () =>
  expectEach("validAges", [[[[1.5, 2.5]], []], [[[0, 150, 151, -1]], [0, 150]], [[[5, 5, 5]], [5, 5, 5]], [[[NaN, 10]], [10]]]));
