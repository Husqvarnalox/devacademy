// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./iterating.mjs");
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

await test("sum-max-basic", () =>
  expectEach("sumOf", [[[[2, 3, 5]], 10], [[[1, 1, 1, 1]], 4], [[[10, -3]], 7]]) ??
  expectEach("maxOf", [[[[3, 9, 4]], 9], [[[1, 2, 3]], 3], [[[8, 2, 5]], 8]]));
await test("index-count-basic", () =>
  expectEach("firstIndex", [[[["a", "b", "a"], "a"], 0], [[[10, 20, 30], 30], 2], [[["x", "y"], "y"], 1]]) ??
  expectEach("countLong", [[[["дом", "ручка", "стол"], 4], 2], [[["кот", "пёс", "ёж"], 3], 2]]));

await test("sum-max-edge", () =>
  expectEach("sumOf", [[[[]], 0], [[[7]], 7], [[[-1, -2, -3]], -6], [[[0.5, 0.25]], 0.75]]) ??
  expectEach("maxOf", [[[[]], undefined], [[[7]], 7], [[[-5, -2, -9]], -2], [[[0, -1]], 0], [[[-3, -3]], -3]]));
await test("index-count-edge", () =>
  expectEach("firstIndex", [[[[], "a"], -1], [[["a"], "z"], -1], [[["Привет", "привет"], "привет"], 1], [[[1, 2, 3], "2"], -1], [[[5, 5, 5], 5], 0]]) ??
  expectEach("countLong", [[[[], 1], 0], [[["я"], 1], 1], [[["я", "мы"], 5], 0], [[["мама", "папа", "дочь"], 4], 3]]));
