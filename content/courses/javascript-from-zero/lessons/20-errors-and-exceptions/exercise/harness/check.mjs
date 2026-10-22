// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./errors.mjs");
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
const canon = (v) =>
  Array.isArray(v) ? v.map(canon)
  : v !== null && typeof v === "object" ? Object.fromEntries(Object.keys(v).sort().map((k) => [k, canon(v[k])]))
  : v;
const same = (a, b) =>
  Object.is(a, b) ||
  (typeof a === "object" && typeof b === "object" && a !== null && b !== null &&
    Array.isArray(a) === Array.isArray(b) && JSON.stringify(canon(a)) === JSON.stringify(canon(b)));
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

await test("throw-basic", () =>
  expectEach("divide", [[[10, 4], 2.5], [[9, 3], 3]]) ??
  expectThrows("divide", [[[1, 0], "Деление на ноль"]]) ??
  expectEach("checkAge", [[[30], 30], [[18], 18]]) ??
  expectThrows("checkAge", [[[-5], "Возраст вне диапазона 0-150"], [[200], "Возраст вне диапазона 0-150"], [["abc"], "Возраст должен быть числом"]]) ??
  expectEach("firstOrThrow", [[[[7, 8]], 7], [[["a"]], "a"]]) ??
  expectThrows("firstOrThrow", [[[[]], "Список пуст"]]));
await test("catch-basic", () =>
  expectEach("safeDivide", [[[6, 3], 2], [[1, 0], "Ошибка: Деление на ноль"], [[9, 2], 4.5]]) ??
  expectEach("isValidAge", [[[30], true], [[-5], false], [[200], false]]));

await test("throw-edge", () =>
  expectEach("checkAge", [[[0], 0], [[150], 150], [[0.5], 0.5]]) ??
  expectThrows("checkAge", [
    [[-1], "Возраст вне диапазона 0-150"],
    [[151], "Возраст вне диапазона 0-150"],
    [["30"], "Возраст должен быть числом"],
    [[true], "Возраст должен быть числом"],
    [[undefined], "Возраст должен быть числом"],
  ]) ??
  expectEach("firstOrThrow", [[[[0]], 0], [[[""]], ""], [[["Привет", "мир"]], "Привет"]]) ??
  expectThrows("divide", [[[0, 0], "Деление на ноль"], [[-5, 0], "Деление на ноль"]]) ??
  expectEach("divide", [[[0, 5], 0], [[-6, 3], -2]]));
await test("catch-edge", () =>
  expectEach("safeDivide", [[[0, 5], 0], [[5, 0], "Ошибка: Деление на ноль"], [[0, 0], "Ошибка: Деление на ноль"], [[-8, 2], -4]]) ??
  expectEach("isValidAge", [[[0], true], [[150], true], [[151], false], [[-1], false], [["abc"], false], [[undefined], false], [["30"], false]]));
