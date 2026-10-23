// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./loop_control.mjs");
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

const show = (v) => (typeof v === "string" ? JSON.stringify(v) : Object.is(v, -0) ? "-0" : String(v));
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!Object.is(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

await test("prime-basic", () =>
  expectEach("isPrime", [[[2], true], [[7], true], [[9], false], [[10], false], [[13], true]]));
await test("odd-basic", () =>
  expectEach("sumOfOdd", [[[5], 9], [[10], 25]]));
await test("table-basic", () =>
  expectEach("multiplicationTable", [[[3], "1 2 3\n2 4 6\n3 6 9"], [[2], "1 2\n2 4"]]));

await test("prime-edge", () =>
  expectEach("isPrime", [[[0], false], [[1], false], [[-7], false], [[4], false], [[25], false], [[49], false], [[97], true], [[7919], true], [[7917], false]]));
await test("odd-edge", () =>
  expectEach("sumOfOdd", [[[0], 0], [[-4], 0], [[1], 1], [[2], 1], [[100], 2500]]));
await test("table-edge", () =>
  expectEach("multiplicationTable", [[[0], ""], [[1], "1"], [[4], "1 2 3 4\n2 4 6 8\n3 6 9 12\n4 8 12 16"]]));
