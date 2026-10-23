// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./basics_check.mjs");
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

const json = (v) => JSON.stringify(v);
function expectEach(fn, name, cases) {
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (json(got) !== json(want) || !Object.is(typeof got, typeof want)) return `${name}(${args.map(json).join(", ")}) вернуло ${json(got)}, ожидалось ${json(want)}`;
  }
  return null;
}
await test("fizz-basic", () =>
  expectEach(m.fizzBuzz, "fizzBuzz", [[[15], "FizzBuzz"], [[9], "Fizz"], [[10], "Buzz"], [[7], "7"], [[1], "1"]]));
await test("sum-basic", () =>
  expectEach(m.sumOfMultiples, "sumOfMultiples", [[[10], 23], [[4], 3], [[16], 60]]));
await test("leap-basic", () =>
  expectEach(m.isLeapYear, "isLeapYear", [[[2024], true], [[2023], false], [[2022], false]]));
await test("prime-basic", () =>
  expectEach(m.firstPrimeAbove, "firstPrimeAbove", [[[10], 11], [[13], 17], [[20], 23]]));

await test("fizz-edge", () =>
  expectEach(m.fizzBuzz, "fizzBuzz", [[[0], "FizzBuzz"], [[30], "FizzBuzz"], [[99], "Fizz"], [[100], "Buzz"], [[101], "101"], [[1000003], "1000003"]]));
await test("sum-edge", () =>
  expectEach(m.sumOfMultiples, "sumOfMultiples", [[[0], 0], [[1], 0], [[-5], 0], [[3], 0], [[6], 8], [[1000], 233168]]));
await test("leap-edge", () =>
  expectEach(m.isLeapYear, "isLeapYear", [[[1900], false], [[2000], true], [[2100], false], [[2400], true], [[1996], true], [[1], false]]));
await test("prime-edge", () =>
  expectEach(m.firstPrimeAbove, "firstPrimeAbove", [[[-10], 2], [[0], 2], [[1], 2], [[2], 3], [[7], 11], [[89], 97], [[1000], 1009]]));
