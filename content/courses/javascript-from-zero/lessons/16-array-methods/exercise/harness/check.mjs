// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./array_methods.mjs");
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

await test("filter-map-basic", () =>
  expectEach("squaresOfEven", [[[[1, 2, 3, 4]], [4, 16]], [[[6, 7, 8]], [36, 64]], [[[5, 3]], []]]) ??
  expectEach("knownWords", [[[["кот", "пёс", "кит"], ["кот", "кит"]], ["кот", "кит"]], [[["a", "b", "a"], ["a"]], ["a", "a"]]]));
await test("find-join-basic", () =>
  expectEach("firstOver", [[[[3, 8, 12], 5], 8], [[[1, 2, 3], 0], 1], [[[10, 20, 30], 15], 20]]) ??
  expectEach("commaList", [[[["хлеб", "молоко"]], "хлеб, молоко"], [[["a", "b", "c"]], "a, b, c"]]));
await test("sort-basic", () =>
  expectEach("sortedAscending", [[[[10, 9, 100, 1]], [1, 9, 10, 100]], [[[3, 1, 2]], [1, 2, 3]], [[[25, 5, 125]], [5, 25, 125]]]));

await test("methods-edge", () =>
  expectEach("squaresOfEven", [[[[]], []], [[[0]], [0]], [[[-2, -3, 4]], [4, 16]]]) ??
  expectEach("firstOver", [[[[], 1], undefined], [[[5, 5], 5], undefined], [[[1, 9, 100], 99], 100], [[[-3, -1], -2], -1]]) ??
  expectEach("knownWords", [[[[], ["кот"]], []], [[["кот"], []], []], [[["Кот", "кот"], ["кот"]], ["кот"]], [[["ёж", "лис"], ["лис", "ёж"]], ["ёж", "лис"]]]) ??
  expectEach("commaList", [[[[]], ""], [[["одно"]], "одно"], [[["Анна", "Борис", "Вера"]], "Анна, Борис, Вера"]]));
await test("sort-edge", () => {
  const problem = expectEach("sortedAscending", [
    [[[]], []],
    [[[7]], [7]],
    [[[3, -1, 2, -10]], [-10, -1, 2, 3]],
    [[[2, 2, 1, 1]], [1, 1, 2, 2]],
    [[[0.5, 0.25, 10, 2]], [0.25, 0.5, 2, 10]],
  ]);
  if (problem) return problem;
  const source = [10, 9, 100, 1];
  const got = m.sortedAscending(source);
  if (got === source) return "sortedAscending должна вернуть новый массив, а не исходный";
  if (JSON.stringify(source) !== "[10,9,100,1]") return `исходный массив изменился: ${JSON.stringify(source)}; используйте toSorted, а не sort`;
  return null;
});
