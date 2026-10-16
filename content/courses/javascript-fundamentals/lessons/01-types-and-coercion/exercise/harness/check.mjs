// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./types_task.mjs");
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

const show = (v) => (typeof v === "bigint" ? `${v}n` : Object.is(v, -0) ? "-0" : typeof v === "string" ? JSON.stringify(v) : String(v));
function expectEach(fn, name, cases) {
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!Object.is(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

await test("type-basic", () =>
  expectEach(m.typeOf, "typeOf", [
    [[1], "number"], [["a"], "string"], [[undefined], "undefined"], [[true], "boolean"],
    [[() => 1], "function"], [[{}], "object"], [[10n], "bigint"], [[Symbol("x")], "symbol"],
  ]));
await test("parse-basic", () =>
  expectEach(m.toNumberStrict, "toNumberStrict", [[["42"], 42], [[" 3.5 "], 3.5], [["-7"], -7], [["12px"], null]]));
await test("neg-zero", () =>
  expectEach(m.isNegativeZero, "isNegativeZero", [[[-0], true], [[0], false], [[1], false]]));
await test("bigsum-basic", () =>
  expectEach(m.bigSum, "bigSum", [[[["1", "2", "3"]], "6"], [[[]], "0"]]));

await test("type-special", () =>
  expectEach(m.typeOf, "typeOf", [
    [[null], "null"], [[[]], "array"], [[[1, 2]], "array"], [[NaN], "nan"], [[0 / 0], "nan"],
    [[new Date()], "object"], [[Infinity], "number"], [["NaN"], "string"], [[Object(1)], "object"],
  ]));
await test("parse-edge", () =>
  expectEach(m.toNumberStrict, "toNumberStrict", [
    [[""], null], [["   "], null], [["\n\t"], null], [["Infinity"], null], [["abc"], null], [["0"], 0],
    [["1e3"], 1000], [["0x10"], 16], [[null], null], [[5], null], [[undefined], null], [["NaN"], null],
  ]));
await test("neg-zero-edge", () =>
  expectEach(m.isNegativeZero, "isNegativeZero", [
    [[0 * -1], true], [[Math.round(-0.4)], true], [[1 / -Infinity], true], [[-0 + 0], false],
    [[NaN], false], [["-0"], false], [[-0n], false], [[-1e-320], false],
  ]));
await test("bigsum-large", () =>
  expectEach(m.bigSum, "bigSum", [
    [[["9007199254740993", "1"]], "9007199254740994"],
    [[["123456789012345678901234567890", "987654321098765432109876543210"]], "1111111110111111111011111111100"],
    [[["-5", "3"]], "-2"],
  ]));
