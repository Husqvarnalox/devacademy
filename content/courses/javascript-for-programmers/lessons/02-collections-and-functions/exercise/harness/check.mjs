// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./collections_task.mjs");
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

const sameArr = (got, want) =>
  Array.isArray(got) && got.length === want.length && want.every((v, i) => Object.is(got[i], v));

await test("sort-basic", () => {
  const got = m.sortedNumbers([10, 9, 1, 100]);
  return sameArr(got, [1, 9, 10, 100]) ? null : `sortedNumbers([10, 9, 1, 100]) вернуло ${JSON.stringify(got)}, ожидалось [1,9,10,100]`;
});
await test("count-basic", () => {
  const got = m.countWords(["a", "b", "a"]);
  if (!(got instanceof Map)) return "countWords должна вернуть Map";
  return got.get("a") === 2 && got.get("b") === 1 && got.size === 2 ? null : "для [\"a\",\"b\",\"a\"] ожидалось a=2, b=1";
});
await test("same-basic", () =>
  expectEach(m.sameItems, "sameItems", [[[[1, 2], [1, 2]], true], [[[1, 2], [2, 1]], false], [[[1], [1, 2]], false], [[[], []], true]]));
await test("avg-basic", () =>
  expectEach(m.average, "average", [[[[1, 2, 3]], 2], [[[10]], 10], [[[2, 4]], 3]]));

await test("sort-edge", () => {
  const input = [3, -1, 20, 3, 0];
  const got = m.sortedNumbers(input);
  if (!sameArr(input, [3, -1, 20, 3, 0])) return "sortedNumbers изменила исходный массив";
  if (got === input) return "sortedNumbers вернула тот же массив, нужен новый";
  if (!sameArr(got, [-1, 0, 3, 3, 20])) return `sortedNumbers([3,-1,20,3,0]) вернуло ${JSON.stringify(got)}`;
  return sameArr(m.sortedNumbers([]), []) ? null : "для пустого массива ожидался пустой массив";
});
await test("count-edge", () => {
  const got = m.countWords(["constructor", "toString", "__proto__", "constructor", "x"]);
  if (!(got instanceof Map)) return "countWords должна вернуть Map";
  if (got.get("constructor") !== 2 || got.get("toString") !== 1 || got.get("__proto__") !== 1 || got.get("x") !== 1)
    return "слова constructor, toString и __proto__ должны считаться как обычные";
  if (got.size !== 4) return `ожидалось 4 разных слова, получено ${got.size}`;
  if ([...got.keys()].join("|") !== "constructor|toString|__proto__|x") return "порядок ключей должен быть порядком первого появления";
  return m.countWords([]).size === 0 ? null : "для пустого списка ожидалась пустая Map";
});
await test("same-edge", () => {
  const inner = [1];
  return expectEach(m.sameItems, "sameItems", [
    [[[NaN, 1], [NaN, 1]], true], [[[0], [-0]], true], [[[[1]], [[1]]], false], [[[inner], [inner]], true],
    [[[1, "1"], [1, 1]], false], [[[1], "1"], false], [[null, null], false], [[undefined, []], false], [[[undefined], [null]], false],
  ]);
});
await test("avg-edge", () =>
  expectEach(m.average, "average", [[[[]], null], [[[-1, 1]], 0], [[[0.5, 1.5]], 1], [[[-3, -5]], -4]]));
