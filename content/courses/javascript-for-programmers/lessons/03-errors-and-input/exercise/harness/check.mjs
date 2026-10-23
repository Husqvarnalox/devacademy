// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./input_task.mjs");
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

const json = (v) => JSON.stringify(v);
const same = (got, want) => json(got) === json(want);
const thrown = (fn) => {
  try {
    fn();
  } catch (e) {
    return e;
  }
  return undefined;
};
const wantValidation = (what, fn, line) => {
  const e = thrown(fn);
  if (!e) return `${what} должно бросать ValidationError`;
  if (!(e instanceof m.ValidationError)) return `${what} бросило ${e?.name}, ожидалось ValidationError`;
  if (line !== undefined && e.line !== line) return `${what}: line = ${e.line}, ожидалось ${line}`;
  return null;
};

await test("line-basic", () => {
  const a = m.parseLine("Ann,30", 1);
  const b = m.parseLine("  Bob , 7 ", 2);
  return same(a, { name: "Ann", age: 30 }) && same(b, { name: "Bob", age: 7 })
    ? null
    : `parseLine вернуло ${json(a)} и ${json(b)}, ожидалось {"name":"Ann","age":30} и {"name":"Bob","age":7}`;
});
await test("records-basic", () => {
  const got = m.parseRecords("Ann,30\nBob,x\nCid,41");
  const want = { records: [{ name: "Ann", age: 30 }, { name: "Cid", age: 41 }], errors: [{ line: 2, message: got.errors?.[0]?.message }] };
  return same(got, want) && typeof got.errors[0].message === "string" && got.errors[0].message !== ""
    ? null
    : `parseRecords вернуло ${json(got)}: ожидались две записи и одна ошибка на строке 2`;
});
await test("json-basic", () => {
  const got = m.parseJsonSafe('{"a":[1,2]}', null);
  return same(got, { a: [1, 2] }) && m.parseJsonSafe("{bad", "F") === "F" ? null : "верный JSON разбирается, неверный даёт fallback";
});
await test("numbers-basic", () => {
  const got = m.parseNumbers("1 2\n 3.5\t-4\n");
  return same(got, [1, 2, 3.5, -4]) ? null : `parseNumbers вернуло ${json(got)}, ожидалось [1,2,3.5,-4]`;
});

await test("line-errors", () =>
  wantValidation('parseLine("a,b,c", 3)', () => m.parseLine("a,b,c", 3), 3) ??
  wantValidation('parseLine("", 4)', () => m.parseLine("", 4), 4) ??
  wantValidation('parseLine(",5", 5)', () => m.parseLine(",5", 5), 5) ??
  wantValidation('parseLine("A,", 6)', () => m.parseLine("A,", 6), 6) ??
  wantValidation('parseLine("A,-1", 7)', () => m.parseLine("A,-1", 7), 7) ??
  wantValidation('parseLine("A,1.5", 8)', () => m.parseLine("A,1.5", 8), 8) ??
  wantValidation('parseLine("A,12x", 9)', () => m.parseLine("A,12x", 9), 9) ??
  wantValidation('parseLine("A,151", 10)', () => m.parseLine("A,151", 10), 10) ??
  (same(m.parseLine("Z,150", 1), { name: "Z", age: 150 }) && same(m.parseLine("Y,0", 1), { name: "Y", age: 0 }) ? null : "возраст 0 и 150 допустим"));
await test("records-edge", () => {
  const got = m.parseRecords("\r\nAnn,30\r\n   \n,5\nBob,200\r\nCid,1,2\nDan, 9 \n");
  const lines = got.errors.map((e) => e.line);
  if (!same(got.records, [{ name: "Ann", age: 30 }, { name: "Dan", age: 9 }])) return `записи: ${json(got.records)}, ожидались Ann и Dan`;
  if (!same(lines, [4, 5, 6])) return `номера строк ошибок: ${json(lines)}, ожидалось [4,5,6] (пустые строки тоже считаются)`;
  if (!got.errors.every((e) => typeof e.message === "string" && e.message !== "")) return "у каждой ошибки должно быть непустое сообщение";
  const empty = m.parseRecords("");
  if (!same(empty, { records: [], errors: [] })) return `пустой текст должен давать пустые списки, получено ${json(empty)}`;
  for (const bad of [null, undefined, 42, ["Ann,3"]]) {
    const e = thrown(() => m.parseRecords(bad));
    if (!(e instanceof TypeError)) return `parseRecords(${json(bad)}) должно бросать TypeError, а не собирать ошибку`;
  }
  return null;
});
await test("json-edge", () => {
  if (m.parseJsonSafe("null", 5) !== null) return 'parseJsonSafe("null", 5) должно вернуть null: это верный JSON';
  if (m.parseJsonSafe("", 5) !== 5) return "пустая строка - неверный JSON, ожидался fallback";
  if (m.parseJsonSafe(undefined, 5) !== 5) return "не строка: ожидался fallback";
  if (m.parseJsonSafe(null, 5) !== 5) return "значение null не строка: ожидался fallback, а не результат JSON.parse(null)";
  if (m.parseJsonSafe(12, 7) !== 7) return "число не строка: ожидался fallback";
  if (m.parseJsonSafe('{"a":1,}', "F") !== "F") return "висящая запятая - ошибка JSON";
  const fb = { x: 1 };
  if (m.parseJsonSafe("[", fb) !== fb) return "fallback нужно вернуть как есть (тот же объект)";
  return m.parseJsonSafe("0", 9) === 0 ? null : 'parseJsonSafe("0", 9) должно вернуть 0';
});
await test("numbers-edge", () => {
  if (!same(m.parseNumbers(""), [])) return "пустой ввод даёт []";
  if (!same(m.parseNumbers("  \n\t "), [])) return "ввод из одних пробелов даёт []";
  if (!same(m.parseNumbers("1  2\r\n3"), [1, 2, 3])) return "несколько пробелов и \\r\\n - разделители";
  if (!same(m.parseNumbers("+4 007 0.5"), [4, 7, 0.5])) return "знак +, ведущие нули и дробь допустимы";
  for (const bad of ["1 x", "0x10", "1e3", "Infinity", "1,5", ".5", "5.", "- 1"]) {
    const msg = wantValidation(`parseNumbers(${json(bad)})`, () => m.parseNumbers(bad));
    if (msg) return msg;
  }
  return null;
});
