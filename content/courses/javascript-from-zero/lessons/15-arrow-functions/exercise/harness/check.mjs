// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./arrow_functions.mjs");
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
const same = (a, b) =>
  Object.is(a, b) ||
  (typeof a === "object" && typeof b === "object" && a !== null && b !== null && JSON.stringify(a) === JSON.stringify(b));
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

const dbl = (n) => n * 2;
const even = (n) => n % 2 === 0;

await test("arrows-basic", () => {
  if (typeof m.double !== "function") return "double должна быть функцией: export const double = (n) => ...";
  if (typeof m.isEven !== "function") return "isEven должна быть функцией: export const isEven = (n) => ...";
  return expectEach("double", [[[4], 8], [[10], 20], [[1], 2]]) ??
    expectEach("isEven", [[[10], true], [[7], false], [[2], true], [[3], false]]);
});
await test("apply-basic", () =>
  expectEach("applyTwice", [[[dbl, 3], 12], [[(n) => n + 1, 5], 7], [[(s) => s + "!", "Да"], "Да!!"]]) ??
  expectEach("applyToEach", [[[[1, 2, 3], dbl], [2, 4, 6]], [[[5, 6], (n) => n + 1], [6, 7]]]) ??
  expectEach("countMatching", [[[[1, 2, 3, 4], even], 2], [[[1, 3, 5], even], 0], [[[2, 4], even], 2]]));

await test("arrows-edge", () =>
  expectEach("double", [[[0], 0], [[-5], -10], [[0.5], 1], [[1000000], 2000000]]) ??
  expectEach("isEven", [[[0], true], [[-3], false], [[-4], true], [[1], false], [[1000001], false]]));
await test("apply-edge", () => {
  const problem =
    expectEach("applyToEach", [
      [[[], dbl], []],
      [[["кот", "ёж"], (w) => w.toUpperCase()], ["КОТ", "ЁЖ"]],
      [[["а", "бв"], (w) => w.length], [1, 2]],
    ]) ??
    expectEach("countMatching", [
      [[[], even], 0],
      [[["да", "нет", "ок"], (w) => w.length === 2], 2],
      [[[0, -2, -3], even], 2],
    ]) ??
    expectEach("applyTwice", [[[(s) => s + s, "аб"], "абабабаб"], [[(n) => -n, 7], 7]]);
  if (problem) return problem;
  const calls = [];
  const source = [3, 1, 2];
  const got = m.applyToEach(source, (x) => { calls.push(x); return x; });
  if (JSON.stringify(calls) !== "[3,1,2]") return `fn нужно вызывать ровно один раз на элемент и по порядку; вызовы были с ${JSON.stringify(calls)}`;
  if (got === source) return "applyToEach должна вернуть новый массив, а не исходный";
  if (JSON.stringify(source) !== "[3,1,2]") return "исходный массив менять нельзя";
  const seen = [];
  m.countMatching([7, 8], (x) => { seen.push(x); return true; });
  if (JSON.stringify(seen) !== "[7,8]") return `test нужно вызывать один раз на элемент; вызовы были с ${JSON.stringify(seen)}`;
  return null;
});
