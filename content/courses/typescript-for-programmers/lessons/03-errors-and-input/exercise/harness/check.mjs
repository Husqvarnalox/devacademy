// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./input_task.ts");
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

const show = (v) => {
  if (typeof v === "string") return JSON.stringify(v);
  if (Object.is(v, -0)) return "-0";
  if (Array.isArray(v) || (v && typeof v === "object")) return JSON.stringify(v);
  return String(v);
};
const same = (a, b) => {
  if (Array.isArray(a) && Array.isArray(b)) return a.length === b.length && a.every((x, i) => same(x, b[i]));
  if (a && b && typeof a === "object" && typeof b === "object" && !Array.isArray(a) && !Array.isArray(b)) {
    const ka = Object.keys(a).sort(), kb = Object.keys(b).sort();
    return ka.length === kb.length && ka.every((k, i) => k === kb[i] && same(a[k], b[k]));
  }
  return Object.is(a, b);
};
function expectEach(name, cases) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (!same(got, want)) return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, ожидалось ${show(want)}`;
  }
  return null;
}

function expectThrows(name, args, message) {
  const fn = m[name];
  if (typeof fn !== "function") return `функция ${name} не найдена: проверьте имя и слово export`;
  let got;
  try {
    got = fn(...args);
  } catch (e) {
    if (!(e instanceof Error)) return `${name}(${args.map(show).join(", ")}) выбросило не Error, а ${show(e)}`;
    if (e.message !== message) return `${name}(${args.map(show).join(", ")}) выбросило сообщение ${show(e.message)}, ожидалось ${show(message)}`;
    return null;
  }
  return `${name}(${args.map(show).join(", ")}) вернуло ${show(got)}, а ожидалось исключение с сообщением ${show(message)}`;
}

function expectParseError(args, message, line) {
  const fn = m.parseLine;
  if (typeof fn !== "function") return "функция parseLine не найдена: проверьте имя и слово export";
  let got;
  try {
    got = fn(...args);
  } catch (e) {
    if (typeof m.ParseError !== "function" || !(e instanceof m.ParseError)) return `parseLine(${args.map(show).join(", ")}) выбросило не ParseError, а ${show(e)}`;
    if (e.message !== message) return `parseLine(${args.map(show).join(", ")}) выбросило сообщение ${show(e.message)}, ожидалось ${show(message)}`;
    if (e.line !== line) return `parseLine(${args.map(show).join(", ")}) выбросило ParseError со строкой ${show(e.line)}, ожидалось ${line}`;
    return null;
  }
  return `parseLine(${args.map(show).join(", ")}) вернуло ${show(got)}, а ожидалось исключение ParseError "${message}"`;
}

await test("line-basic", () =>
  expectEach("parseLine", [
    [["яблоко,3", 1], { name: "яблоко", quantity: 3 }],
    [[" хлеб , 12 ", 2], { name: "хлеб", quantity: 12 }],
  ]) ?? expectParseError(["молоко", 5], "ожидалось два поля", 5));
await test("order-basic", () =>
  expectEach("parseOrder", [
    [["яблоко,3\nхлеб,x\nмолоко,2"], {
      items: [{ name: "яблоко", quantity: 3 }, { name: "молоко", quantity: 2 }],
      problems: [{ line: 2, message: "количество должно быть целым числом" }],
    }],
  ]));
await test("list-basic", () =>
  expectEach("parseStringList", [[['["a","b"]'], ["a", "b"]], [["[]"], []], [["{oops"], null]]));
await test("message-basic", () =>
  expectEach("errorMessage", [[[new Error("сбой")], "сбой"], [["строкой"], "строкой"], [[42], "неизвестная ошибка"]]));

await test("line-edge", () =>
  expectParseError(["a,b,c", 3], "ожидалось два поля", 3) ??
  expectParseError([",5", 4], "пустое название", 4) ??
  expectParseError(["   ,5", 4], "пустое название", 4) ??
  expectParseError(["чай,", 6], "количество должно быть целым числом", 6) ??
  expectParseError(["чай,2.5", 6], "количество должно быть целым числом", 6) ??
  expectParseError(["чай,-1", 7], "количество должно быть целым числом", 7) ??
  expectParseError(["чай,1e1", 7], "количество должно быть целым числом", 7) ??
  expectParseError(["чай,0", 8], "количество вне диапазона 1-100", 8) ??
  expectParseError(["чай,101", 8], "количество вне диапазона 1-100", 8) ??
  expectParseError(["a,b,c,", 3], "ожидалось два поля", 3) ??
  expectEach("parseLine", [[["чай,1", 1], { name: "чай", quantity: 1 }], [["чай,100", 1], { name: "чай", quantity: 100 }], [["чай,007", 1], { name: "чай", quantity: 7 }]]));
await test("order-edge", () =>
  expectEach("parseOrder", [
    [[""], { items: [], problems: [] }],
    [["\n\n"], { items: [], problems: [] }],
    [["a,1\r\n\r\nb\r\n   \r\n,5\r\nc,200\r\nd,4\r\n"], {
      items: [{ name: "a", quantity: 1 }, { name: "d", quantity: 4 }],
      problems: [
        { line: 3, message: "ожидалось два поля" },
        { line: 5, message: "пустое название" },
        { line: 6, message: "количество вне диапазона 1-100" },
      ],
    }],
  ]));
await test("list-edge", () =>
  expectEach("parseStringList", [
    [['["a", 1]'], null], [['{"a":"b"}'], null], [['"text"'], null], [["null"], null], [[""], null],
    [['["", "x y"]'], ["", "x y"]], [["[[\"a\"]]"], null],
  ]));
await test("message-edge", () => {
  const custom = new m.ParseError("плохая строка", 3);
  return expectEach("errorMessage", [
    [[custom], "плохая строка"], [[null], "неизвестная ошибка"], [[undefined], "неизвестная ошибка"],
    [[{ message: "подделка" }], "неизвестная ошибка"], [[""], ""], [[new TypeError("тип")], "тип"],
  ]);
});
