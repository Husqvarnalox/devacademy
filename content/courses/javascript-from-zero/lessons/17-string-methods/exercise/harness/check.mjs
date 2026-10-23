// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./string_methods.mjs");
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

await test("capitalize-slug-basic", () =>
  expectEach("capitalize", [[["иВАН"], "Иван"], [["hello"], "Hello"], [["МАРИЯ"], "Мария"]]) ??
  expectEach("slugify", [[["  Мой Первый Пост "], "мой-первый-пост"], [["Hello World"], "hello-world"], [["Один"], "один"]]));
await test("words-basic", () =>
  expectEach("countWords", [[["привет мир"], 2], [["  привет мир "], 2], [["раз два три четыре"], 4]]) ??
  expectEach("mentions", [[["Купить МОЛОКО", "молоко"], true], [["Купить хлеб", "молоко"], false], [["Hello World", "WORLD"], true]]));
await test("shorten-basic", () =>
  expectEach("shorten", [[["Длинный заголовок", 7], "Длинный..."], [["короткий", 20], "короткий"], [["abcdef", 3], "abc..."]]));

await test("strings-edge", () =>
  expectEach("countWords", [[[""], 0], [["   "], 0], [["слово"], 1], [[" слово "], 1]]) ??
  expectEach("capitalize", [[[""], ""], [["я"], "Я"], [["ёж"], "Ёж"], [["аННА"], "Анна"]]) ??
  expectEach("slugify", [[[""], ""], [["   "], ""], [["ДА НЕТ"], "да-нет"]]));
await test("strings-edge-2", () =>
  expectEach("shorten", [[["abc", 3], "abc"], [["abcd", 3], "abc..."], [["", 5], ""], [["привет", 0], "..."], [["Привет, мир", 6], "Привет..."]]) ??
  expectEach("mentions", [[["", "а"], false], [["Привет, МИР", "мир"], true], [["кот", "котик"], false]]));
