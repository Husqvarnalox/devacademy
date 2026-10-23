// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./arrays.mjs");
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

await test("last-basic", () =>
  expectEach("lastItem", [[[[4, 5, 6]], 6], [[["a", "b"]], "b"], [[[10, 20]], 20]]));
await test("squares-basic", () =>
  expectEach("squares", [[[4], [1, 4, 9, 16]], [[5], [1, 4, 9, 16, 25]], [[2], [1, 4]]]));
await test("swap-basic", () =>
  expectEach("swapFirstAndLast", [[[[1, 2, 3]], [3, 2, 1]], [[[1, 2, 3, 4]], [4, 2, 3, 1]], [[[5, 6, 7, 8, 9]], [9, 6, 7, 8, 5]]]));

await test("last-edge", () =>
  expectEach("lastItem", [[[[]], undefined], [[[42]], 42], [[["Яблоко"]], "Яблоко"], [[[0, 0, -1]], -1]]));
await test("squares-edge", () =>
  expectEach("squares", [[[0], []], [[1], [1]], [[-3], []], [[10], [1, 4, 9, 16, 25, 36, 49, 64, 81, 100]]]));
await test("swap-edge", () => {
  const problem = expectEach("swapFirstAndLast", [
    [[[]], []],
    [[["один"]], ["один"]],
    [[["хлеб", "молоко"]], ["молоко", "хлеб"]],
    [[["а", "б", "в", "г"]], ["г", "б", "в", "а"]],
  ]);
  if (problem) return problem;
  const original = [1, 2, 3];
  const got = m.swapFirstAndLast(original);
  if (got !== original) return "функция должна вернуть тот же массив, который получила, а не новый";
  if (original[0] !== 3 || original[2] !== 1) return "исходный массив должен измениться: [1, 2, 3] -> [3, 2, 1]";
  return null;
});
