// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./unions_check.ts");
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

await test("normalize-basic", () =>
  expectEach("normalizeId", [[[42], "00042"], [["  ab-7 "], "AB-7"]]));
await test("area-basic", () =>
  expectEach("area", [[[{ kind: "square", side: 3 }], 9], [[{ kind: "rect", width: 2, height: 5 }], 10], [[{ kind: "circle", radius: 1 }], 3.14]]));
await test("payment-basic", () =>
  expectEach("describePayment", [[[{ method: "cash", amount: 100 }], "Наличные: 100"], [[{ method: "card", amount: 50, last4: "1234" }], "Карта *1234: 50"]]));
await test("first-string-basic", () =>
  expectEach("firstString", [[[[1, "a", "b"]], "a"], [[[null, 2, "z"]], "z"]]));

await test("normalize-edge", () =>
  expectEach("normalizeId", [[[0], "00000"], [[123456], "123456"], [[""], ""], [["\tx\n"], "X"]]));
await test("area-edge", () =>
  expectEach("area", [[[{ kind: "circle", radius: 0 }], 0], [[{ kind: "circle", radius: 2 }], 12.57], [[{ kind: "rect", width: 0, height: 9 }], 0]]));
await test("payment-edge", () =>
  expectEach("describePayment", [[[{ method: "refund", amount: 10 }], "Возврат: 10"], [[{ method: "refund", amount: 10, reason: "брак" }], "Возврат: 10 (брак)"], [[{ method: "cash", amount: 0 }], "Наличные: 0"], [[{ method: "refund", amount: 5, reason: "" }], "Возврат: 5 ()"]]));
await test("first-string-edge", () =>
  expectEach("firstString", [[[[]], undefined], [[[1, null, 2]], undefined], [[["", "a"]], ""]]));
