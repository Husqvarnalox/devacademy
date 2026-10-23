// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./coercion_check.mjs");
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

await test("parse-basic", () =>
  expectEach(m.parseIntStrict, "parseIntStrict", [[["42"], 42], [[" -7 "], -7], [["+5"], 5], [["12px"], null], [["abc"], null]]));
await test("same-basic", () =>
  expectEach(m.sameValueZero, "sameValueZero", [[[1, 1], true], [["a", "a"], true], [[1, 2], false], [[1, "1"], false], [[null, undefined], false]]));
await test("bool-basic", () =>
  expectEach(m.parseBool, "parseBool", [[[true], true], [[false], false], [["true"], true], [["FALSE"], false], [[" Yes "], true], [["no"], false]]));

await test("parse-edge", () =>
  expectEach(m.parseIntStrict, "parseIntStrict", [
    [[""], null], [["   "], null], [["1.5"], null], [["1e3"], null], [["0x10"], null], [["9007199254740993"], null],
    [["9007199254740991"], 9007199254740991], [["-0"], 0], [["007"], 7], [["- 1"], null], [["Infinity"], null],
    [[null], null], [[5], null], [[undefined], null], [[["1"]], null],
  ]));
await test("same-edge", () => {
  const o = {};
  return expectEach(m.sameValueZero, "sameValueZero", [
    [[NaN, NaN], true], [[0, -0], true], [[-0, 0], true], [[NaN, 0], false], [[NaN, "NaN"], false],
    [[o, o], true], [[{}, {}], false], [[[], []], false], [[0, "0"], false], [[0, false], false], [[undefined, undefined], true],
  ]);
});
await test("bool-edge", () =>
  expectEach(m.parseBool, "parseBool", [
    [[1], true], [[0], false], [[-0], false], [[2], null], [[NaN], null], [[""], null], [["maybe"], null],
    [["  TRUE"], true], [["0 "], false], [[null], null], [[undefined], null], [[[1]], null], [[[]], null], [[{}], null],
    [["2"], null], [[1n], null],
  ]));
