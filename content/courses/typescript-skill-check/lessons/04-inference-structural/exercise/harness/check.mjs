// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./inference_check.ts");
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

await test("greet-basic", () =>
  expectEach("greet", [[[{ name: "Аня" }], "Здравствуйте, Аня!"], [[{ name: "Пёс", age: 3 }], "Здравствуйте, Пёс!"]]));
await test("describe-basic", () =>
  expectEach("describe", [[[{ name: "Аня", age: 25 }], "Аня (25)"], [[{ name: "Ян" }], "Ян"]]));
await test("adults-basic", () =>
  expectEach("adultNames", [[[[{ name: "а", age: 30 }, { name: "б", age: 12 }, { name: "в", age: 18 }]], ["а", "в"]], [[[]], []]]));
await test("oldest-basic", () => {
  const a = { name: "а", age: 30 };
  const b = { name: "б", age: 45 };
  return m.oldest([a, b]) === b ? null : "oldest должна вернуть тот же объект с наибольшим возрастом";
});

await test("greet-edge", () =>
  expectEach("greet", [[[{ name: "" }], "Здравствуйте, !"], [[{ name: "Ю", email: "y@x.ru", extra: [1] }], "Здравствуйте, Ю!"]]));
await test("describe-edge", () =>
  expectEach("describe", [[[{ name: "Малыш", age: 0 }], "Малыш (0)"], [[{ name: "Х", age: undefined }], "Х"], [[{ name: "Дед", age: 99, email: "d@x.ru" }], "Дед (99)"]]));
await test("adults-edge", () =>
  expectEach("adultNames", [[[[{ name: "а", age: 17 }]], []], [[[{ name: "б", age: 18 }, { name: "в", age: 0 }, { name: "г", age: 100 }]], ["б", "г"]]]));
await test("oldest-edge", () => {
  const a = { name: "а", age: 50 };
  const b = { name: "б", age: 50 };
  const c = { name: "в", age: 0 };
  if (m.oldest([]) !== undefined) return "для пустого списка ожидалось undefined";
  if (m.oldest([c]) !== c) return "список из одного человека с возрастом 0: нужен этот человек";
  return m.oldest([a, b]) === a ? null : "при равном возрасте нужен первый из списка";
});
