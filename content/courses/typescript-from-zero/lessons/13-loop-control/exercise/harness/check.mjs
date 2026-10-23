// Проверка DevAcademy: загружает файл ученика и печатает по одной строке на каждый тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./control_task.ts");
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

await test("divisor-basic", () =>
  expectEach("firstDivisor", [[[15], 3], [[49], 7], [[13], 13]]));
await test("skip-basic", () =>
  expectEach("sumSkipping", [[[10, 3], 37], [[5, 2], 9], [[4, 10], 10]]));
await test("triangle-basic", () =>
  expectEach("triangle", [[[3], "*\n**\n***"], [[1], "*"]]));

await test("divisor-edge", () =>
  expectEach("firstDivisor", [[[2], 2], [[4], 2], [[9], 3], [[97], 97], [[91], 7], [[1001], 7]]));
await test("skip-edge", () =>
  expectEach("sumSkipping", [[[0, 3], 0], [[-4, 2], 0], [[1, 1], 0], [[3, 3], 3], [[100, 5], 4000]]));
await test("triangle-edge", () =>
  expectEach("triangle", [[[0], ""], [[-2], ""], [[2], "*\n**"], [[5], "*\n**\n***\n****\n*****"]]));
