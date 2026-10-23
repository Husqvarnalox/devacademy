// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./basics_task.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

const show = (v) => (Object.is(v, -0) ? "-0" : typeof v === "string" ? JSON.stringify(v) : Array.isArray(v) ? "[массив]" : String(v));
function expectEach(fn, name, cases) {
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!Object.is(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}
function expectThrows(fn, name, args, ErrorType) {
  try {
    fn(...args);
  } catch (e) {
    return e instanceof ErrorType ? null : `${name}(${args.map(show).join(", ")}) бросило ${e?.name}, ожидалось ${ErrorType.name}`;
  }
  return `${name}(${args.map(show).join(", ")}) должно бросать ${ErrorType.name}`;
}

await test("money-basic", () =>
  expectEach(m.addMoney, "addMoney", [[[0.1, 0.2], 0.3], [[1, 2], 3], [[19.99, 0.01], 20], [[0.7, 0.1], 0.8]]));
await test("blank-basic", () =>
  expectEach(m.isBlank, "isBlank", [[[""], true], [["  "], true], [[null], true], [[undefined], true], [["a"], false], [[0], false]]));
await test("first-basic", () =>
  expectEach(m.firstDefined, "firstDefined", [[[null, undefined, 5, 6], 5], [["x"], "x"], [[], undefined], [[null], undefined]]));
await test("sum-basic", () =>
  expectEach(m.sumNumbers, "sumNumbers", [[[[1, 2, 3]], 6], [[[]], 0], [[[1, "2", 3]], 4]]));

await test("money-edge", () =>
  expectEach(m.addMoney, "addMoney", [[[-0.1, 0.1], 0], [[-1.1, -2.2], -3.3], [[0.07, 0.01], 0.08], [[1.15, 2.3], 3.45], [[0, 0], 0], [[1e6, 0.01], 1000000.01]]) ??
  expectThrows(m.addMoney, "addMoney", ["1", 2], TypeError) ??
  expectThrows(m.addMoney, "addMoney", [1, NaN], TypeError) ??
  expectThrows(m.addMoney, "addMoney", [Infinity, 1], TypeError) ??
  expectThrows(m.addMoney, "addMoney", [null, 1], TypeError));
await test("blank-edge", () =>
  expectEach(m.isBlank, "isBlank", [
    [["\t\n "], true], [[" x "], false], [["0"], false], [[0], false], [[-0], false], [[false], false], [[NaN], false],
    [[[]], false], [[{}], false], [[[""]], false], [["false"], false],
  ]));
await test("first-edge", () =>
  expectEach(m.firstDefined, "firstDefined", [
    [[null, 0, 5], 0], [[undefined, "", "x"], ""], [[null, false, true], false], [[undefined, NaN, 1], NaN], [[undefined, null, undefined], undefined],
  ]));
await test("sum-edge", () =>
  expectEach(m.sumNumbers, "sumNumbers", [
    [[[1, NaN, 2]], 3], [[[Infinity, 1]], 1], [[[null, undefined, "5", true, 1]], 1], [[[0.5, 0.25]], 0.75], [[[-1, -2]], -3],
    [[[[1], 2, {}]], 2],
  ]));
