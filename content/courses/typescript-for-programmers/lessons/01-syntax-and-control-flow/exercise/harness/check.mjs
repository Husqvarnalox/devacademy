// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./basics_task.ts");
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

await test("round-basic", () =>
  expectEach("roundMoney", [[[0.1 + 0.2], 0.3], [[19.994], 19.99], [[19.996], 20], [[5], 5]]));
await test("class-basic", () =>
  expectEach("classifyNumber", [[[5], "целое"], [[2.5], "дробное"], [[NaN], "не число"], [[Infinity], "бесконечность"]]));
await test("digits-basic", () =>
  expectEach("digitSum", [[[12345], 15], [[7], 7], [[100], 1]]));
await test("ticket-basic", () =>
  expectEach("ticketPrice", [[[3, false], 0], [[10, false], 300], [[30, false], 500], [[20, true], 350], [[70, false], 250]]));

await test("round-edge", () =>
  expectEach("roundMoney", [[[0], 0], [[-1.234], -1.23], [[0.1 * 3], 0.3], [[1e6 + 0.1 + 0.2], 1000000.3]]));
await test("class-edge", () =>
  expectEach("classifyNumber", [[[0], "целое"], [[-3], "целое"], [[-Infinity], "бесконечность"], [[4.0], "целое"], [[-0.5], "дробное"], [[0 / 0], "не число"]]));
await test("digits-edge", () =>
  expectEach("digitSum", [[[0], 0], [[-907], 16], [[1000000], 1], [[99999], 45]]));
await test("ticket-edge", () =>
  expectEach("ticketPrice", [[[5, false], 0], [[6, false], 300], [[17, true], 300], [[18, false], 500], [[18, true], 350], [[64, true], 350], [[65, true], 250], [[65, false], 250], [[0, true], 0]]));
